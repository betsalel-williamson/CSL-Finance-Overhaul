using System; // Math

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// How far the camera stands from a building it is sent to look at, so the whole building
    /// is in view with some of its street around it (Cities-free). The game's camera "size"
    /// is roughly the distance to what it looks at, in metres.
    /// </summary>
    public static class BuildingFraming
    {
        /// <summary>
        /// Never closer than this: a small shop still shows its street, and the fireworks that
        /// climb from its roof stay in view (at 70 they left the top of the screen).
        /// </summary>
        public const float Nearest = 180f;

        /// <summary>Never farther: the building must stay the subject of the view.</summary>
        public const float Farthest = 520f;

        /// <summary>The lot's larger side three times over, or twice the height: whichever needs more room.</summary>
        public static float Distance(float widthMeters, float lengthMeters, float heightMeters)
        {
            float footprint = Math.Max(Positive(widthMeters), Positive(lengthMeters)) * 3f;
            float tall = Positive(heightMeters) * 2f;
            return Math.Max(Nearest, Math.Min(Farthest, Math.Max(footprint, tall)));
        }

        /// <summary>Camera pitch in degrees, low enough to show the sky above the roof; lower still for a tower.</summary>
        public static float Pitch(float heightMeters)
        {
            return Positive(heightMeters) > 100f ? 25f : 32f;
        }

        private static float Positive(float v)
        {
            return float.IsNaN(v) || v < 0f ? 0f : v;
        }
    }
}
