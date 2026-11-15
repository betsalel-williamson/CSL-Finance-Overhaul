using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Notices (priority 1). Exact wording for each kind, plus properties over 3,000 seeded game
    /// days: never more than three order chirps a day; when collapsed, the summary's counts add up
    /// to the day's order notices and name every symbol; bond notices always pass through
    /// unchanged and in order; senders are City Brokerage (orders) and City Treasury (bonds).
    /// </summary>
    public class NoticesTests
    {
        [Fact]
        public void Wording()
        {
            Assert.Equal(
                "Limit buy filled: bought 100 IDX at ₡1,452 (limit ₡1,460) for ₡145,200. #orders",
                NoticeText.Filled(OrderKind.LimitBuy, 100, "IDX", 1452f, 1460, 145200).Text
            );
            Assert.Equal(
                "Stop-loss filled: sold 100 GGI at ₡6,012 (stop ₡6,050) for ₡601,200. #orders",
                NoticeText.Filled(OrderKind.StopLossSell, 100, "GGI", 6012f, 6050, 601200).Text
            );
            Assert.Equal(
                "Take-profit filled: sold 100 GGI at ₡6,412 (target ₡6,400) for ₡641,200. #orders",
                NoticeText.Filled(OrderKind.TakeProfitSell, 100, "GGI", 6412f, 6400, 641200).Text
            );
            Assert.Equal(
                "Limit buy for 100 IDX expired after 90 days without filling. #orders",
                NoticeText.Expired(OrderKind.LimitBuy, 100, "IDX", 90).Text
            );
            Assert.Equal(
                "Limit buy for 100 IDX cancelled: not enough cash (needs ₡145,200). #orders",
                NoticeText
                    .Cancelled(OrderKind.LimitBuy, 100, "IDX", OrderRules.ReasonFunds, 145200)
                    .Text
            );
            Assert.Equal(
                "Stop-loss for 100 GGI cancelled: the city no longer holds any GGI. #orders",
                NoticeText
                    .Cancelled(OrderKind.StopLossSell, 100, "GGI", OrderRules.ReasonNoShares, 0)
                    .Text
            );
            Assert.Equal(
                "Bond #5 repaid at maturity: ₡500,000 plus the last ₡1,904.17 coupon. #citybonds",
                NoticeText.BondMatured(5, 50_000_000, 190_417).Text
            );
            Assert.Equal(
                "Bond #5 coupon paid: ₡1,904.17 (59 of 60 left). #citybonds",
                NoticeText.BondCoupon(5, 190_417, 59, 60).Text
            );
            Assert.Equal(
                NoticeText.Brokerage,
                NoticeText.Expired(OrderKind.LimitBuy, 1, "A", 1).Sender
            );
            Assert.Equal(NoticeText.Treasury, NoticeText.BondCoupon(1, 1, 1, 1).Sender);
        }

        [Fact]
        public void SettingsDefaultsAndRoundTrip()
        {
            ModSettings d = SettingsFile.Parse("");
            Assert.True(d.NoticesEnabled);
            Assert.False(d.NoticesBondCoupons);
            var s = new ModSettings { NoticesEnabled = false, NoticesBondCoupons = true };
            ModSettings back = SettingsFile.Parse(SettingsFile.Format(s));
            Assert.False(back.NoticesEnabled);
            Assert.True(back.NoticesBondCoupons);
        }

        [Fact]
        public void SummaryExample()
        {
            var day = new List<Notice>
            {
                NoticeText.Filled(OrderKind.LimitBuy, 1, "IDX", 1, 1, 1),
                NoticeText.Filled(OrderKind.StopLossSell, 1, "GGI", 1, 1, 1),
                NoticeText.BondMatured(5, 100, 0),
                NoticeText.Filled(OrderKind.TakeProfitSell, 1, "Oil", 1, 1, 1),
                NoticeText.Expired(OrderKind.LimitBuy, 1, "TJL", 90),
            };
            List<Notice> out_ = NoticeBatch.Collapse(day);
            Assert.Equal(2, out_.Count);
            Assert.Equal(
                "4 order updates today: 3 filled (IDX, GGI, Oil), 1 expired (TJL). Details in Economy → Investments. #orders",
                out_[0].Text
            );
            Assert.Equal(NoticeKind.BondMatured, out_[1].Kind);
        }

        [Fact]
        public void BatchProperties()
        {
            var rng = new Random(1001);
            string[] syms = { "IDX", "GGI", "Oil", "TJL", "DS", "HS" };
            for (int k = 0; k < 3000; k++)
            {
                var day = new List<Notice>();
                int n = rng.Next(0, 12);
                for (int i = 0; i < n; i++)
                {
                    string s = syms[rng.Next(syms.Length)];
                    switch (rng.Next(5))
                    {
                        case 0:
                            day.Add(NoticeText.Filled(OrderKind.LimitBuy, 10, s, 100, 100, 1000));
                            break;
                        case 1:
                            day.Add(NoticeText.Expired(OrderKind.StopLossSell, 10, s, 30));
                            break;
                        case 2:
                            day.Add(
                                NoticeText.Cancelled(
                                    OrderKind.LimitBuy,
                                    10,
                                    s,
                                    OrderRules.ReasonFunds,
                                    5
                                )
                            );
                            break;
                        case 3:
                            day.Add(NoticeText.BondCoupon(i, 100, 3, 12));
                            break;
                        default:
                            day.Add(NoticeText.BondMatured(i, 10000, 100));
                            break;
                    }
                }

                List<Notice> result = NoticeBatch.Collapse(day);
                List<Notice> orders = day.Where(x => x.IsOrder).ToList();
                List<Notice> bonds = day.Where(x => !x.IsOrder).ToList();

                Assert.True(result.Count(x => x.IsOrder) <= NoticeBatch.MaxOrderNotices);
                Assert.Equal(
                    bonds.Select(b => b.Text),
                    result.Where(x => !x.IsOrder).Select(b => b.Text)
                );
                if (orders.Count <= NoticeBatch.MaxOrderNotices)
                {
                    Assert.Equal(
                        orders.Select(o => o.Text),
                        result.Where(x => x.IsOrder).Select(o => o.Text)
                    );
                }
                else
                {
                    Notice summary = result.Single(x => x.IsOrder);
                    Assert.Equal(NoticeKind.Summary, summary.Kind);
                    Assert.StartsWith(orders.Count + " order updates today: ", summary.Text);
                    int counted = 0;
                    foreach (string word in new[] { "filled", "expired", "cancelled" })
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(
                            summary.Text,
                            @"(\d+) " + word + " "
                        );
                        if (m.Success)
                        {
                            counted += int.Parse(m.Groups[1].Value);
                        }
                    }

                    Assert.Equal(orders.Count, counted);
                    foreach (Notice o in orders)
                    {
                        Assert.Contains(o.Symbol, summary.Text);
                    }
                }

                foreach (Notice r in result)
                {
                    Assert.Equal(r.IsOrder ? NoticeText.Brokerage : NoticeText.Treasury, r.Sender);
                }
            }
        }
    }
}
