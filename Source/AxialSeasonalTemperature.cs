using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    internal static class AxialSeasonalTemperature
    {
        // x = |latitude| / 90  (0 = equator, 1 = pole),  y = seasonal temperature swing (°C, half-amplitude)
        // Source: TemperatureTuning.SeasonalTempVariationCurve
        // used to normalize at earth-like axial tilt
        private static readonly SimpleCurve VanillaSeasonalAmpByLat = new SimpleCurve
        {
            new CurvePoint(0f,   3f),   // equator
            new CurvePoint(0.1f, 4f),   // ~9°
            new CurvePoint(1f,  28f),   // pole
        };

        // core function, patches SeasonalShiftAmplitudeAt
        // reproduces standard rimworld temperatures at an inclination of 23
        internal static float SeasonalAmplitudeScale(float latDeg)
        {
            float phi = Mathf.Abs(latDeg) * Mathf.Deg2Rad;
            float num = DailyInsolation(phi, SolarGeometry.sinTilt) - DailyInsolation(phi, -SolarGeometry.sinTilt);
            float den = DailyInsolation(phi, SolarGeometry.SinEarthTilt) - DailyInsolation(phi, -SolarGeometry.SinEarthTilt);
            float ratio = den > 1e-6f ? num / den : SolarGeometry.sinTilt / SolarGeometry.SinEarthTilt;
            return Mathf.Pow(ratio, SolarGeometry.dampingK);
        }

        // total solar energy for a given latitude and inclination
        private static float DailyInsolation(float phi, float sinDecl)
        {
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            float tanPhi = cosPhi > 1e-6f ? sinPhi / cosPhi : Mathf.Sign(sinPhi) * 1e6f;
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;
            float cosH0 = -tanPhi * tanDecl;

            if (cosH0 <= -1f)
                return sinPhi * sinDecl;
            if (cosH0 >= 1f)
                return 0f;

            float h0 = Mathf.Acos(cosH0);
            return (1f / Mathf.PI) * (h0 * sinPhi * sinDecl + cosPhi * cosDecl * Mathf.Sin(h0));
        }

        // Takes explicit tiltDeg/k rather than reading SolarGeometry state so the Create World
        // UI can quickly evaluate arbitrary slider values before they're committed.
        internal static (float min, float max) ApproxTempRange(float latDeg, float tiltDeg, float k)
        {
            float avg = AxialAnnualTemperature.ApproxCorrectedTemp(latDeg, tiltDeg, k);
            float x = Mathf.Abs(latDeg) / 90f;
            float vanillaAmp = VanillaSeasonalAmpByLat.Evaluate(x);
            float sinT = Mathf.Sin(tiltDeg * Mathf.Deg2Rad);
            float phi = Mathf.Abs(latDeg) * Mathf.Deg2Rad;
            float num = DailyInsolation(phi, sinT) - DailyInsolation(phi, -sinT);
            float den = DailyInsolation(phi, SolarGeometry.SinEarthTilt) - DailyInsolation(phi, -SolarGeometry.SinEarthTilt);
            float ampScale = den > 1e-6f ? Mathf.Pow(num / den, k) : Mathf.Pow(sinT / SolarGeometry.SinEarthTilt, k);
            float amp = vanillaAmp * ampScale;
            return (avg - amp, avg + amp);
        }

    }
}
