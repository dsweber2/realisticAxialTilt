using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    // Computes an adjustment to the yearly average temperature based on how
    // much sun a given latitude gets. Primary output is AnnualTemperatureCorrection, everything else supports that.
    // Annual-mean insolation at a latitude as a fraction of the global mean, via a
    // truncated Legendre polynomial expansion to 6th order (Nadeau & McGehee 2017, Icarus 291:46-50):
    //   Q(sinLat, obliquity) = 1 − (5/8)P₂(cos obliquity)P₂(sinLat) − (9/64)P₄(cos obliquity)P₄(sinLat) − (65/1024)P₆(cos obliquity)P₆(sinLat)
    // Used to scale temperature corrections: (insolation ratio − 1) × scale ≈ Δ°C
    internal static class AxialAnnualTemperature
    {
        // Empirical factor mapping fractional insolation change to °C in
        // RimWorld's temperature model, tuned so the Legendre curve matches the
        // vanilla AvgTempByLatitudeCurve (see analysis/annual_temperature_plots.py).
        private const float TemperatureInsolationScale = 70f;

        // x = |latitude| / 90  (0 = equator, 1 = pole),  y = annual mean temperature (°C)
        // Source: WorldGenStep_Terrain.AvgTempByLatitudeCurve
        // used to normalize at earth-like axial tilt
        private static readonly SimpleCurve VanillaAvgTempByLat = new SimpleCurve
        {
            new CurvePoint(0f,  30f),   // equator
            new CurvePoint(0.1f, 29f),  // ~9°
            new CurvePoint(0.5f,  7f),  // ~45°
            new CurvePoint(1f,  -37f),  // pole
        };

        // the core function, this replaces BaseTemperatureAtLatitude
        internal static float AnnualTemperatureCorrection(float latDeg)
        {
            float sinLat = Mathf.Sin(latDeg * Mathf.Deg2Rad);
            float earthInsol = AnnualMeanInsolation(sinLat, SolarGeometry.CosEarthTilt);
            float ratio = earthInsol > 1e-6f ? AnnualMeanInsolation(sinLat, SolarGeometry.cosTilt) / earthInsol : 1f;
            return (Mathf.Pow(ratio, SolarGeometry.dampingK) - 1f) * earthInsol * TemperatureInsolationScale;
        }

        // First few terms in the approximation to the integral giving total insolation over a year at a given latitude and obliquity, this is from (Nadeau & McGehee 2017, Icarus 291:46-50)
        internal static float AnnualMeanInsolation(float sinLat, float cosObliquity) =>
            1f - (5f / 8f)    * Legendre2(cosObliquity) * Legendre2(sinLat)
               - (9f / 64f)   * Legendre4(cosObliquity) * Legendre4(sinLat)
               - (65f / 1024f) * Legendre6(cosObliquity) * Legendre6(sinLat);

        private static float Legendre2(float y) => (3f * y * y - 1f) * 0.5f;
        private static float Legendre4(float y) { float y2 = y * y; return (35f * y2 * y2 - 30f * y2 + 3f) / 8f; }
        private static float Legendre6(float y) { float y2 = y * y; float y4 = y2 * y2; return (231f * y4 * y2 - 315f * y4 + 105f * y2 - 5f) / 16f; }


        // Takes explicit tiltDeg/k rather than reading SolarGeometry state so the Create World
        // UI can quickly evaluate arbitrary slider values before they're committed.
        internal static float ApproxCorrectedTemp(float latDeg, float tiltDeg, float k)
        {
            float x = Mathf.Abs(latDeg) / 90f;
            float vanillaBase = VanillaAvgTempByLat.Evaluate(x);
            float sinLat = Mathf.Sin(latDeg * Mathf.Deg2Rad);
            float cosTiltRef = Mathf.Cos(tiltDeg * Mathf.Deg2Rad);
            float earthInsol = AnnualMeanInsolation(sinLat, SolarGeometry.CosEarthTilt);
            float ratio = earthInsol > 1e-6f ? AnnualMeanInsolation(sinLat, cosTiltRef) / earthInsol : 1f;
            return vanillaBase + (Mathf.Pow(ratio, k) - 1f) * earthInsol * TemperatureInsolationScale;
        }
    }
}
