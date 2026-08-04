using HarmonyLib;
using RealisticAxialTilt.Api;
using RimWorld;
using UnityEngine;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(GenCelestial), "CelestialSunGlowPercent")]
    internal static class GlowCurvePatch
    {
        internal const float GlowLower = -0.42f;
        internal const float GlowUpper =  0.70f;

        internal static float PeakDailyGlow(float lat, int dayOfYear)
        {
            float latRad = lat * Mathf.Deg2Rad;
            Vector3 up  = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 sun = SolarGeometry.ComputeSunPosition((float)dayOfYear, 0.5f, new Vector3(1f, 0f, 0f));
            float sinEl = Vector3.Dot(sun, up);
            return Mathf.Clamp01(Mathf.InverseLerp(GlowLower, GlowUpper, sinEl));
        }

        [HarmonyPostfix]
        private static void Postfix(float latitude, int dayOfYear, float dayPercent, ref float __result)
        {
            if (RealisticAxialTiltApi.LightingSuppressed)
                return;

            Vector3 sun = SolarGeometry.ComputeSunPosition((float)dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));
            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up   = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            float sinEl  = Vector3.Dot(sun, up);

            __result = Mathf.Clamp01(Mathf.InverseLerp(GlowLower, GlowUpper, sinEl));
        }
    }
}
