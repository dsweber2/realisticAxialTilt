using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    internal static class SolarGeometry
    {
        private static float sinTilt;
        private static float cosTilt;
        private static float dampingK;

        internal static void ApplyAxialTilt(float tiltDeg, float k)
        {
            float rad = tiltDeg * Mathf.Deg2Rad;
            sinTilt = Mathf.Sin(rad);
            cosTilt = Mathf.Cos(rad);
            dampingK = k;
        }

        private static readonly float SinEarthTilt = Mathf.Sin(23.45f * Mathf.Deg2Rad);
        private static readonly float CosEarthTilt = Mathf.Cos(23.45f * Mathf.Deg2Rad);

        // σ₆(η,β) = 1 − (5/8)P₂(cosβ)P₂(η) − (9/64)P₄(cosβ)P₄(η) − (65/1024)P₆(cosβ)P₆(η)
        // Nadeau & McGehee (2017), Icarus 291:46-50; η = sin(lat), β = obliquity
        private const float TemperatureInsolationScale = 70f;

        // AvgTempByLatitudeCurve from WorldGenStep_Terrain: (0,30),(0.1,29),(0.5,7),(1,-37)
        private static readonly float[] VanillaX = { 0f, 0.1f, 0.5f, 1f };
        private static readonly float[] VanillaY = { 30f, 29f,  7f, -37f };

        // TemperatureTuning.SeasonalTempVariationCurve: (0,3),(0.1,4),(1,28)
        private static readonly float[] VanillaAmpX = { 0f, 0.1f, 1f };
        private static readonly float[] VanillaAmpY = { 3f,  4f, 28f };

        internal static float AnnualTemperatureCorrection(float latDeg)
        {
            float eta = Mathf.Sin(latDeg * Mathf.Deg2Rad);
            float earthInsol = Sigma6(eta, CosEarthTilt);
            float ratio = earthInsol > 1e-6f ? Sigma6(eta, cosTilt) / earthInsol : 1f;
            return (Mathf.Pow(ratio, dampingK) - 1f) * earthInsol * TemperatureInsolationScale;
        }

        internal static float ApproxCorrectedTemp(float latDeg, float tiltDeg, float k)
        {
            float x = Mathf.Abs(latDeg) / 90f;
            float vanillaBase = Interp(VanillaX, VanillaY, x);
            float eta = Mathf.Sin(latDeg * Mathf.Deg2Rad);
            float cosTiltRef = Mathf.Cos(tiltDeg * Mathf.Deg2Rad);
            float earthInsol = Sigma6(eta, CosEarthTilt);
            float ratio = earthInsol > 1e-6f ? Sigma6(eta, cosTiltRef) / earthInsol : 1f;
            return vanillaBase + (Mathf.Pow(ratio, k) - 1f) * earthInsol * TemperatureInsolationScale;
        }

        internal static (float min, float max) ApproxTempRange(float latDeg, float tiltDeg, float k)
        {
            float avg = ApproxCorrectedTemp(latDeg, tiltDeg, k);
            float x = Mathf.Abs(latDeg) / 90f;
            float vanillaAmp = Interp(VanillaAmpX, VanillaAmpY, x);
            float sinT = Mathf.Sin(tiltDeg * Mathf.Deg2Rad);
            float phi = latDeg * Mathf.Deg2Rad;
            float num = DailyInsolation(phi, sinT) - DailyInsolation(phi, -sinT);
            float den = DailyInsolation(phi, SinEarthTilt) - DailyInsolation(phi, -SinEarthTilt);
            float ampScale = den > 1e-6f ? Mathf.Pow(num / den, k) : Mathf.Pow(sinT / SinEarthTilt, k);
            float amp = vanillaAmp * ampScale;
            return (avg - amp, avg + amp);
        }

        private static float Interp(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) return ys[0];
            for (int ii = 1; ii < xs.Length; ii++)
            {
                if (x <= xs[ii])
                {
                    float t = (x - xs[ii - 1]) / (xs[ii] - xs[ii - 1]);
                    return ys[ii - 1] + t * (ys[ii] - ys[ii - 1]);
                }
            }
            return ys[ys.Length - 1];
        }

        private static float Sigma6(float eta, float cosBeta) =>
            1f - (5f / 8f)    * P2(cosBeta) * P2(eta)
               - (9f / 64f)   * P4(cosBeta) * P4(eta)
               - (65f / 1024f) * P6(cosBeta) * P6(eta);

        private static float P2(float y) => (3f * y * y - 1f) * 0.5f;
        private static float P4(float y) { float y2 = y * y; return (35f * y2 * y2 - 30f * y2 + 3f) / 8f; }
        private static float P6(float y) { float y2 = y * y; float y4 = y2 * y2; return (231f * y4 * y2 - 315f * y4 + 105f * y2 - 5f) / 16f; }

        internal static float SeasonalAmplitudeScale(float latDeg)
        {
            float phi = latDeg * Mathf.Deg2Rad;
            float num = DailyInsolation(phi, sinTilt) - DailyInsolation(phi, -sinTilt);
            float den = DailyInsolation(phi, SinEarthTilt) - DailyInsolation(phi, -SinEarthTilt);
            float ratio = den > 1e-6f ? num / den : sinTilt / SinEarthTilt;
            return Mathf.Pow(ratio, dampingK);
        }

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

        internal static Vector3 ComputeSunPosition(float dayOfYear, float dayPercent, Vector3 initialSunPos)
        {
            float S = Mathf.Sin(dayOfYear / 60f * Mathf.PI * 2f);
            float sinDecl = sinTilt * S;
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;

            Vector3 vector = initialSunPos * 100f;
            vector.y += tanDecl * 100f;
            return (Quaternion.AngleAxis((dayPercent - 0.5f) * 360f, Vector3.up) * vector).normalized;
        }
    }
}
