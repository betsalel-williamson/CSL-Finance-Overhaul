using System;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Camera framing for dev captures of a building facade (Cities-free maths).
    /// Building frame: local +Z is the lot front (street side), angle in radians as in
    /// <c>Building.m_angle</c>; vanilla rotates local offsets about −Y by that angle.
    /// </summary>
    public static class FacadeFraming
    {
        /// <summary>Building-local offset → world offset (x, z); y is unchanged.</summary>
        public static void LocalToWorld(
            float angle,
            float localX,
            float localZ,
            out float worldX,
            out float worldZ
        )
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            worldX = localX * c - localZ * s;
            worldZ = localX * s + localZ * c;
        }

        /// <summary>
        /// Camera yaw (degrees, 0..360, Unity convention: 0 looks along +Z) that looks at
        /// the facade from the street, i.e. along the building's −front direction.
        /// </summary>
        public static float FacadeYawDegrees(float angle)
        {
            double yaw = 180.0 - angle * 180.0 / Math.PI;
            yaw %= 360.0;
            if (yaw < 0)
            {
                yaw += 360.0;
            }

            return (float)yaw;
        }

        /// <summary>
        /// Parses a focus trigger's text: an optional 1-based index ("2") and the word
        /// "capture". Missing or invalid index means 1.
        /// </summary>
        public static void ParseRequest(string text, out int index, out bool capture)
        {
            index = 1;
            capture = false;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (
                string word in text.Split(
                    new[] { ' ', '\t', '\r', '\n', ',' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                int n;
                if (
                    int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                    && n >= 1
                )
                {
                    index = n;
                }
                else if (string.Equals(word, "capture", StringComparison.OrdinalIgnoreCase))
                {
                    capture = true;
                }
            }
        }
    }
}
