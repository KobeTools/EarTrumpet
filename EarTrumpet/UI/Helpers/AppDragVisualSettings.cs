using System;

namespace EarTrumpet.UI.Helpers
{
    public static class AppDragVisualSettings
    {
        public const int DefaultIntensity = 12;
        public const int MaxIntensity = 100;

        public static int GetIntensity()
        {
            var settings = App.Settings;
            if (settings == null)
            {
                return DefaultIntensity;
            }

            return Clamp(settings.AppDragVisualIntensity, 0, MaxIntensity);
        }

        public static bool IsEnabled => GetIntensity() > 0;

        public static void GetVisualParameters(int intensity, out double haloOpacity, out double blurRadius, out double glowOpacity, out double glowBlurRadius, out double sourceIconOpacity)
        {
            intensity = Clamp(intensity, 0, MaxIntensity);
            if (intensity <= 0)
            {
                haloOpacity = blurRadius = glowOpacity = glowBlurRadius = 0;
                sourceIconOpacity = 1;
                return;
            }

            var amount = intensity / (double)MaxIntensity;

            haloOpacity = 0.3 * amount;
            blurRadius = 4 * amount;
            glowOpacity = 0.5 * amount;
            glowBlurRadius = 1 + (8 * amount);
            sourceIconOpacity = 1 - (0.35 * amount);
        }

        public static double GetGlowPaddingDip(int intensity)
        {
            if (intensity <= 0)
            {
                return 0;
            }

            var amount = intensity / (double)MaxIntensity;
            return 2 + (6 * amount);
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
