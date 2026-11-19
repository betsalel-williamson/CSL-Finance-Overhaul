using System; // Exception
using System.Collections; // IEnumerator
using UnityEngine; // Mathf, MonoBehaviour, Time, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): AudioManager, Building,
//   BuildingManager, EffectCollection, EffectInfo, EffectManager, InstanceID, SimulationManager

namespace FinanceOverhaul
{
    /// <summary>
    /// Fireworks over a company's headquarters when it goes public and on its first trading
    /// day, with the game's own firework effects (the ones its events use). They wait while
    /// the game is paused (the IPO dialog pauses it), so they go off once the player is back
    /// in the city. Lives on the overlay root.
    /// </summary>
    public class IpoFireworks : MonoBehaviour
    {
        /// <summary>The game's effects, in the order tried; a city without them shows none.</summary>
        private static readonly string[] Effects =
        {
            "Event Firework Particles 1", // not shown: a game effect name
            "Event Firework Particles 2", // not shown: a game effect name
            "Event Firework Particles 3", // not shown: a game effect name
            "FireworksComplexIngame", // not shown: a game effect name
        };

        private const int Bursts = 8;
        private const float Gap = 0.8f;
        private const float MaxWait = 600f;

        private static IpoFireworks current;

        /// <summary>Bursts launched since the mod loaded (for the game UI tests).</summary>
        public static int Launched { get; private set; }

        private void Awake()
        {
            current = this;
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }

        /// <summary>Any thread: fireworks over that building, if it is still the same building.</summary>
        public static void Over(ushort building, uint buildIndex)
        {
            CompanyListing.OnMainThread(() =>
            {
                if (current != null)
                {
                    current.StartCoroutine(current.Show(building, buildIndex));
                }
            });
        }

        private IEnumerator Show(ushort building, uint buildIndex)
        {
            float waited = 0f;
            while (SimulationManager.instance.SimulationPaused && waited < MaxWait)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int i = 0; i < Bursts; i++)
            {
                try
                {
                    Burst(building, buildIndex, i);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("ipo fireworks: " + ex.Message);
                    yield break;
                }

                yield return new WaitForSeconds(Gap);
            }
        }

        private static void Burst(ushort building, uint buildIndex, int n)
        {
            Building b = BuildingManager.instance.m_buildings.m_buffer[building];
            if (b.m_buildIndex != buildIndex || b.Info == null)
            {
                return;
            }

            EffectInfo effect = null;
            for (int i = 0; i < Effects.Length && effect == null; i++)
            {
                effect = EffectCollection.FindEffect(Effects[(n + i) % Effects.Length]);
            }

            if (effect == null)
            {
                if (n == 0)
                {
                    ModLog.Info("ipo fireworks: the game has no firework effect loaded");
                }

                return;
            }

            // From the roof, a little to one side each time: the rockets climb from there.
            float spread = Mathf.Max(b.Width, b.Length) * 4f;
            var at = new Vector3(
                b.m_position.x + UnityEngine.Random.Range(-spread, spread),
                b.m_position.y + b.Info.m_size.y,
                b.m_position.z + UnityEngine.Random.Range(-spread, spread)
            );
            // No instance: the game's effect only fires for a building holding an event (it
            // checks a flag on the building it is given), and this one is not.
            InstanceID id = InstanceID.Empty;
            var area = new EffectInfo.SpawnArea(at, Vector3.up, 4f);
            EffectInfo chosen = effect;
            SimulationManager.instance.AddAction(() =>
                EffectManager.instance.DispatchEffect(
                    chosen,
                    id,
                    area,
                    Vector3.zero,
                    0f,
                    1f,
                    AudioManager.instance.DefaultGroup
                )
            );
            Launched++;
            if (n == 0)
            {
                ModLog.Info("ipo fireworks: over building " + building + " with " + chosen.name);
            }
        }
    }
}
