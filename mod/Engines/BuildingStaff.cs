namespace FinanceOverhaul
{
    /// <summary>
    /// A building's jobs and the people working there today, counted as the game's own info
    /// panels count them: a zoned business's workplaces after its district's policies (Industry
    /// 4.0 trims them), a player building's from its prefab; workers alive and not still moving
    /// in (ZonedBuildingWorldInfoPanel, the service panels).
    /// </summary>
    internal static class BuildingStaff
    {
        /// <summary>The workplaces the building offers now (0 when it has none).</summary>
        public static int Jobs(ushort id)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            BuildingInfo info = buffer[id].Info;
            if (info == null || info.m_buildingAI == null)
            {
                return 0;
            }

            int level0,
                level1,
                level2,
                level3;
            PrivateBuildingAI zoned = info.m_buildingAI as PrivateBuildingAI;
            if (zoned != null)
            {
                zoned.CalculateWorkplaceCount(
                    (ItemClass.Level)buffer[id].m_level,
                    new ColossalFramework.Math.Randomizer(id),
                    buffer[id].Width,
                    buffer[id].Length,
                    out level0,
                    out level1,
                    out level2,
                    out level3
                );
                zoned.AdjustWorkplaceCount(
                    id,
                    ref buffer[id],
                    ref level0,
                    ref level1,
                    ref level2,
                    ref level3
                );
            }
            else
            {
                PlayerBuildingAI player = info.m_buildingAI as PlayerBuildingAI;
                if (player == null)
                {
                    return 0;
                }

                player.CountWorkPlaces(out level0, out level1, out level2, out level3);
            }

            return level0 + level1 + level2 + level3;
        }

        /// <summary>The people working in the building today.</summary>
        public static int Workers(ushort id)
        {
            CitizenManager citizens = CitizenManager.instance;
            CitizenUnit[] units = citizens.m_units.m_buffer;
            Citizen[] people = citizens.m_citizens.m_buffer;
            uint unit = BuildingManager.instance.m_buildings.m_buffer[id].m_citizenUnits;
            int count = 0;
            int guard = 0;
            while (unit != 0 && guard++ < 524288)
            {
                if ((units[unit].m_flags & CitizenUnit.Flags.Work) != 0)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        uint c = units[unit].GetCitizen(k);
                        if (
                            c != 0
                            && !people[c].Dead
                            && (people[c].m_flags & Citizen.Flags.MovingIn) == 0
                        )
                        {
                            count++;
                        }
                    }
                }

                unit = units[unit].m_nextUnit;
            }

            return count;
        }
    }
}
