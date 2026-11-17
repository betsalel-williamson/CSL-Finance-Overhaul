using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: City bonds, the Welcome back dialog and money checks.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator BondsSteps(
            string line,
            string cmd,
            string a,
            string b,
            Context ctx,
            StepResult r
        )
        {
            switch (cmd)
            {
                case "bonds-section":
                {
                    // bonds-section show|auto — shows the Loans tab's City bonds section even
                    // with the Options flag off (auto: back to the flag).
                    CityBondsSection.ForceShow = a == "show";
                    yield return new WaitForSeconds(0.8f);
                    break;
                }
                case "bonds-amount":
                case "bonds-term":
                {
                    // bonds-amount TEXT · bonds-term YEARS — fills the section's amount or term.
                    if (CityBondsSection.Current == null)
                    {
                        r.Error = "no City bonds section";
                        break;
                    }

                    if (cmd == "bonds-amount")
                    {
                        CityBondsSection.Current.SetAmount(a);
                    }
                    else
                    {
                        CityBondsSection.Current.SetTerm((BondTerm)(int)ParseFloat(a, 5f));
                    }

                    yield return new WaitForSeconds(0.3f);
                    break;
                }
                case "bond-finances":
                {
                    // bond-finances DEBT INCOME — fixes the city's debt and annual income (₡)
                    // for bond pricing until the scenario ends; "real" goes back to the city's
                    // own; no argument logs what pricing sees now.
                    if (a == "real")
                    {
                        BondDesk.ClearTestFinances();
                    }
                    else if (!string.IsNullOrEmpty(a))
                    {
                        long setDebt,
                            setIncome;
                        if (
                            !long.TryParse(
                                a.Replace(",", string.Empty),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out setDebt
                            )
                            || !long.TryParse(
                                (b ?? string.Empty).Replace(",", string.Empty),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out setIncome
                            )
                        )
                        {
                            r.Error = "bond-finances DEBT INCOME (whole ₡) or bond-finances real";
                            break;
                        }

                        BondDesk.SetTestFinances(setDebt * 100, setIncome * 100);
                    }

                    long debt,
                        income;
                    BondDesk.CityFinances(out debt, out income);
                    ModLog.Info(
                        "uitest: bond-finances debt="
                            + debt
                            + " annualIncome="
                            + income
                            + " (cents)"
                    );
                    break;
                }
                case "issue-bond":
                {
                    // issue-bond YEARS FACE₡ — sells a bond at the offered rate with the
                    // simulation paused; the city's cash must rise by exactly the face.
                    long face = (long)ParseFloat(b, 0f) * 100;
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f);
                    long before = RawCash(EconomyManager.instance);
                    BondOffer offer;
                    IssuedBond bond = BondDesk.Issue(
                        (BondTerm)(int)ParseFloat(a, 1f),
                        face,
                        out offer
                    );
                    long delta = RawCash(EconomyManager.instance) - before;
                    sim.SimulationPaused = paused;
                    if (bond == null)
                    {
                        r.Error = "bond refused: " + offer.Reason;
                        break;
                    }

                    ctx.Bonds.Add(bond.Id);
                    ModLog.Info(
                        "uitest: issued bond #"
                            + bond.Id
                            + " at "
                            + BondPricing.RateText(offer.RateBasisPoints)
                            + ", cash +"
                            + delta
                            + " cents"
                    );
                    if (delta != face)
                    {
                        r.Error = "cash rose by " + delta + " cents, expected " + face;
                    }

                    break;
                }
                case "bond-backdate":
                {
                    // bond-backdate DAYS — moves this scenario's newest bond's schedule earlier,
                    // so the next coupon falls due without waiting a game month.
                    if (
                        ctx.Bonds.Count == 0
                        || !BondDesk.Backdate(
                            ctx.Bonds[ctx.Bonds.Count - 1],
                            (int)ParseFloat(a, 0f)
                        )
                    )
                    {
                        r.Error = "issue-bond first";
                    }

                    break;
                }
                case "away":
                {
                    // away DAYS — pretends the mod was off this many days: this scenario's bonds
                    // missed their payments and the Welcome back dialog should ask.
                    if (ctx.Bonds.Count == 0)
                    {
                        r.Error = "issue-bond first";
                        break;
                    }

                    BondDesk.SimulateAway((int)ParseFloat(a, 0f), ctx.Bonds);
                    yield return new WaitForSeconds(1f);
                    break;
                }
                case "welcome-back":
                {
                    // welcome-back pay|skip|close — answers the Welcome back dialog (close clicks
                    // ×, which starts fresh).
                    if (WelcomeBackDialog.Current == null || !WelcomeBackDialog.Current.IsShown)
                    {
                        r.Error = "Welcome back dialog not shown";
                        break;
                    }

                    if (a == "close")
                    {
                        WelcomeBackDialog.Current.ClickClose();
                    }
                    else
                    {
                        WelcomeBackDialog.Current.Choose(a == "pay");
                    }

                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "expect-welcome-back":
                {
                    // expect-welcome-back shown|hidden
                    bool shown =
                        WelcomeBackDialog.Current != null && WelcomeBackDialog.Current.IsShown;
                    if (shown != (a == "shown"))
                    {
                        r.Error = "Welcome back dialog " + (shown ? "shown" : "hidden");
                    }

                    break;
                }
                case "expect-bond-coupons":
                {
                    // expect-bond-coupons N — coupons paid on this scenario's newest bond.
                    IssuedBond bond =
                        ctx.Bonds.Count == 0
                            ? null
                            : BondDesk.Snapshot().Find(ctx.Bonds[ctx.Bonds.Count - 1]);
                    int paid = bond == null ? -1 : bond.CouponsPaid;
                    if (paid != (int)ParseFloat(a, 0f))
                    {
                        r.Error = "bond has " + paid + " coupon(s) paid, expected " + a;
                    }

                    break;
                }
                case "cash-set":
                case "cash-restore":
                case "expect-cash":
                {
                    // cash-set ₡ — sets the city's cash outright (the simulation must be paused:
                    // `pause on`); what it took is put back by cash-restore or when the scenario
                    // ends, so payments made meanwhile stay paid. expect-cash ₡ — the cash now,
                    // to the cent.
                    if (!SimulationManager.instance.SimulationPaused)
                    {
                        r.Error = cmd + " needs the simulation paused (pause on)";
                        break;
                    }

                    if (cmd == "cash-restore")
                    {
                        RestoreCash(ctx);
                        break;
                    }

                    long cents;
                    if (!TryCents(a, out cents))
                    {
                        r.Error = cmd + " needs an amount in ₡";
                        break;
                    }

                    long now = RawCash(EconomyManager.instance);
                    if (cmd == "expect-cash")
                    {
                        if (now != cents)
                        {
                            r.Error = "cash is " + now + " cents, expected " + cents;
                        }

                        break;
                    }

                    if (!SetRawCash(cents))
                    {
                        r.Error = "the game's cash field is not available";
                        break;
                    }

                    ctx.CashTaken += now - cents;
                    break;
                }
                case "log-loan-expenses":
                case "expect-loan-expenses":
                {
                    // log-loan-expenses — logs what Economy shows as the loans expense (weekly).
                    // expect-loan-expenses ₡MIN — it must be at least this much.
                    long shown = EconomyManager.instance.GetLoanExpenses();
                    ModLog.Info("uitest: loan expenses shown=" + shown + " cents");
                    long least;
                    if (cmd == "expect-loan-expenses")
                    {
                        if (!TryCents(a, out least))
                        {
                            r.Error = "expect-loan-expenses needs an amount in ₡";
                        }
                        else if (shown < least)
                        {
                            r.Error = "loan expenses show " + shown + " cents, expected " + least;
                        }
                    }

                    break;
                }
                case "bond-pay-due":
                    // Runs the daily bond step now (with the simulation paused), paying what is
                    // due today: after bond-backdate, this scenario's coupon.
                    BondDesk.OnInvestmentsStepped();
                    break;
                case "redeem-new-bonds":
                    // Repays every bond this scenario sold (by the desk or the UI), never the
                    // player's.
                    foreach (IssuedBond owed in BondDesk.Snapshot().Bonds)
                    {
                        if (!ctx.BondsBefore.Contains(owed.Id))
                        {
                            BondDesk.Redeem(owed.Id);
                        }
                    }

                    ctx.Bonds.Clear();
                    break;
                case "money-probe":
                {
                    // money-probe AMOUNT — measures the unit of the game's budget calls: with the
                    // simulation paused, adds AMOUNT through the loan-taken line, reads the city's
                    // cash, then fetches the same AMOUNT back through the loan-payment line. Fails
                    // unless cash returns exactly to where it started.
                    int amount = (int)ParseFloat(a, 100f);
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    EconomyManager em = EconomyManager.instance;
                    long cash0 = RawCash(em),
                        money0 = EconomyManager.playerMoney;
                    int added = em.AddResource(
                        EconomyManager.Resource.LoanAmount,
                        amount,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    );
                    long cash1 = RawCash(em),
                        money1 = EconomyManager.playerMoney;
                    int fetched = em.FetchResource(
                        EconomyManager.Resource.LoanPayment,
                        amount,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    );
                    long cash2 = RawCash(em),
                        money2 = EconomyManager.playerMoney;
                    sim.SimulationPaused = paused;
                    ModLog.Info(
                        "uitest: money-probe amount="
                            + amount
                            + " added="
                            + added
                            + " fetched="
                            + fetched
                            + " raw "
                            + cash0
                            + "→"
                            + cash1
                            + "→"
                            + cash2
                            + " playerMoney "
                            + money0
                            + "→"
                            + money1
                            + "→"
                            + money2
                    );
                    if (cash2 != cash0)
                    {
                        r.Error = "cash did not return: " + cash0 + " → " + cash2;
                    }

                    break;
                }
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}
