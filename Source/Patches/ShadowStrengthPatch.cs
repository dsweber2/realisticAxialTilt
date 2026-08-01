using HarmonyLib;
using RealisticAxialTilt.Api;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(GenCelestial), nameof(GenCelestial.CurShadowStrength))]
    internal static class ShadowStrengthPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map, ref float __result)
        {
            if (RealisticAxialTiltApi.LightingSuppressed)
                return;

            Vector2 lonLat   = Find.WorldGrid.LongLatOf(map.Tile);
            int dayOfYear    = GenDate.DayOfYear(GenTicks.TicksAbs, lonLat.x);
            float dayPct     = GenLocalDate.DayPercent(map);
            Vector3 sun      = SolarGeometry.ComputeSunPosition((float)dayOfYear, dayPct, new Vector3(1f, 0f, 0f));
            float latRad     = lonLat.y * Mathf.Deg2Rad;
            Vector3 up       = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            float sinEl          = Vector3.Dot(sun, up);
            float geometricStrength = Mathf.Sqrt(Mathf.Clamp01(sinEl / SolarGeometry.ShadowFadeThreshold));
            float celestialGlow  = GenCelestial.CurCelestialSunGlow(map);
            float weatherFactor  = celestialGlow > 1e-4f
                ? Mathf.Clamp01(map.skyManager.CurSkyGlow / celestialGlow)
                : 0f;
            __result = geometricStrength * weatherFactor;
        }
    }
}
