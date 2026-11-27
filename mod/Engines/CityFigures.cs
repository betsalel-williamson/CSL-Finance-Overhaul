using System; // Exception
using System.Collections.Generic; // List
using FinanceOverhaul.Core; // CityDriver, CityFigure, IndexDay, LocalCompany, MonitorQuote

// Game classes used without a using (global namespace, Assembly-CSharp): AirplaneInvestmentAI,
//   DeathCareSectorAI, District, DistrictManager, EconomyManager, FuelCompanyAI,
//   HealthCareSectorAI, HealthCareSectorRandomAI, IndustrySectorAI, Investment, InvestmentAI,
//   ShipInvestmentAI, TrafficJellyAI, TransportManager, TransportPassengerData, VehicleManager,
//   VeryLegitCompanyAI

namespace FinanceOverhaul
{
    /// <summary>
    /// The city figure behind each stock, read as the game's own stock classes read it
    /// (city-wide, District 0; docs/developer/game-code-notes.md), for the City overview. Read
    /// only when that view draws; nothing kept, nothing written.
    /// </summary>
    internal static class CityFigures
    {
        /// <summary>Fills each quote's <see cref="MonitorQuote.Figure"/> (main thread).</summary>
        public static void Fill(List<MonitorQuote> quotes)
        {
            if (quotes == null)
            {
                return;
            }

            int members = 0;
            foreach (MonitorQuote q in quotes)
            {
                if (!q.Locked && !q.Coin && IndexDay.IsMemberName(q.Prefab))
                {
                    members++;
                }
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            foreach (MonitorQuote q in quotes)
            {
                try
                {
                    InvestmentAI ai =
                        q.Id >= 0 && q.Id < buffer.Length && buffer[q.Id].Info != null
                            ? buffer[q.Id].Info.m_investmentAI
                            : null;
                    q.Figure = Read(ai, members);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("city figures: " + q.Symbol + ": " + ex.Message);
                    q.Figure = new CityFigure();
                }
            }
        }

        private static CityFigure Read(InvestmentAI ai, int members)
        {
            var f = new CityFigure();
            if (ai == null)
            {
                return f;
            }

            District city = DistrictManager.instance.m_districts.m_buffer[0];
            int company;
            if (IndexListing.IsIndex(ai))
            {
                f.Driver = CityDriver.Index;
                f.A = members;
            }
            else if (CompanyListing.IsCompany(ai, out company))
            {
                f.Driver = CityDriver.Company;
                LocalCompany c = CompanyDesk.CompanyOf(company);
                if (c != null)
                {
                    f.Kind = c.Kind;
                    f.A = CompanyDesk.AnnualEarningsCents(c.Home.Id);
                }
            }
            else if (ai is IndustrySectorAI)
            {
                Industry((IndustrySectorAI)ai, city, f);
            }
            else if (ai is HealthCareSectorRandomAI)
            {
                f.Driver = CityDriver.HealthRisk;
                f.A = city.GetSickCount();
                f.B = city.m_populationData.m_finalCount;
                f.C = city.GetHealCapacity();
            }
            else if (ai is HealthCareSectorAI)
            {
                f.Driver = CityDriver.Health;
                f.A = city.m_residentialData.m_finalHealth;
                f.B = city.GetHealthCareProductivity();
                f.C = city.GetHealCapacity();
            }
            else if (ai is DeathCareSectorAI)
            {
                f.Driver = CityDriver.DeathCare;
                f.A = city.GetDeathCareProductivity();
                f.B = city.GetDeadCapacity();
                f.C = city.GetCremateCapacity();
            }
            else if (ai is FuelCompanyAI)
            {
                f.Driver = CityDriver.Vehicles;
                f.A = VehicleManager.instance.m_vehicleCount;
            }
            else if (ai is TrafficJellyAI)
            {
                f.Driver = CityDriver.Traffic;
                f.A = VehicleManager.instance.m_lastTrafficFlow;
            }
            else if (ai is VeryLegitCompanyAI)
            {
                f.Driver = CityDriver.Crime;
                f.A = city.m_finalCrimeRate;
            }
            else if (ai is AirplaneInvestmentAI)
            {
                f.Driver = CityDriver.Flights;
                f.A = Passengers(4);
            }
            else if (ai is ShipInvestmentAI)
            {
                f.Driver = CityDriver.Ships;
                f.A = Passengers(3);
            }
            else if (ai.m_unit == InvestmentAI.Unit.Coin)
            {
                f.Driver = CityDriver.Coin;
            }

            return f;
        }

        private static void Industry(IndustrySectorAI ai, District city, CityFigure f)
        {
            switch (ai.m_type)
            {
                case IndustrySectorAI.Type.Farming:
                    f.Driver = CityDriver.Farming;
                    f.A = city.GetFarmingIndustryProductivity();
                    f.B = city.GetFarmingIndustryArea();
                    break;
                case IndustrySectorAI.Type.Forestry:
                    f.Driver = CityDriver.Forestry;
                    f.A = city.GetForestryIndustryProductivity();
                    f.B = city.GetForestryIndustryArea();
                    break;
                case IndustrySectorAI.Type.Ore:
                    f.Driver = CityDriver.Ore;
                    f.A = city.GetOreIndustryProductivity();
                    f.B = city.GetOreIndustryArea();
                    break;
                case IndustrySectorAI.Type.Oil:
                    f.Driver = CityDriver.Oil;
                    f.A = city.GetOilIndustryProductivity();
                    f.B = city.GetOilIndustryArea();
                    break;
                default:
                    f.Driver = CityDriver.GenericIndustry;
                    f.A = city.GetGenericIndustryProductivity();
                    f.B = city.GetGenericIndustryArea();
                    break;
            }
        }

        /// <summary>Residents and tourists who rode this transport lately (the game's average).</summary>
        private static double Passengers(int type)
        {
            TransportPassengerData data = TransportManager.instance.m_passengers[type];
            return (double)data.m_residentPassengers.m_averageCount
                + data.m_touristPassengers.m_averageCount;
        }
    }
}
