using System; // Exception
using ColossalFramework.UI; // UIScrollablePanel
using UnityEngine; // MonoBehaviour, Time

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// Economy → Investments: the mouse wheel moves the two lists (Purchasable Investments, Your
    /// Portfolio) by half a stock card a notch (82 pixels). The game's own step is 52 pixels
    /// (measured), under a third of a card, so a list of thirteen stocks takes many notches and
    /// more stocks make it worse. Lives on the overlay root; the game's step is put back when
    /// the mod unloads.
    /// </summary>
    public class InvestmentsWheel : MonoBehaviour
    {
        /// <summary>Pixels a notch: half of the game's 165-pixel stock card.</summary>
        public const int Step = 82;

        private static readonly string[] Lists =
        {
            "InvestmentsScrollablePanel", // not shown: a game control name
            "OwnedInvestmentsScrollablePanel", // not shown: a game control name
        };

        private EconomyPanel economy;
        private readonly int[] before = { -1, -1 };
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 1f;
            try
            {
                if (economy == null)
                {
                    economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                }

                if (economy == null)
                {
                    return;
                }

                for (int i = 0; i < Lists.Length; i++)
                {
                    UIScrollablePanel list = economy.component.Find<UIScrollablePanel>(Lists[i]);
                    if (list != null && list.scrollWheelAmount != Step)
                    {
                        if (before[i] < 0)
                        {
                            before[i] = list.scrollWheelAmount;
                            ModLog.Info(
                                "investments: wheel step of "
                                    + Lists[i]
                                    + " "
                                    + before[i]
                                    + " → "
                                    + Step
                            );
                        }

                        list.scrollWheelAmount = Step;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("investments: wheel step failed: " + ex.Message);
                nextCheck = Time.unscaledTime + 30f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                for (int i = 0; economy != null && i < Lists.Length; i++)
                {
                    UIScrollablePanel list = economy.component.Find<UIScrollablePanel>(Lists[i]);
                    if (list != null && before[i] >= 0)
                    {
                        list.scrollWheelAmount = before[i];
                    }
                }
            }
            catch
            {
                // fail soft on unload
            }
        }
    }
}
