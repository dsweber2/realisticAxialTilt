using HarmonyLib;
using RealisticAxialTilt.Api;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(GenCelestial), nameof(GenCelestial.GetLightSourceInfo))]
    internal static class ShadowDirectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Map map, GenCelestial.LightType type, ref GenCelestial.LightInfo __result)
        {
            if (RealisticAxialTiltApi.LightingSuppressed)
                return;

            if (type != GenCelestial.LightType.Shadow)
                return;

            Vector2 lonLat = Find.WorldGrid.LongLatOf(map.Tile);
            int dayOfYear = GenDate.DayOfYear(GenTicks.TicksAbs, lonLat.x);
            float dayPercent = GenLocalDate.DayPercent(map);

            Vector2 shadow = SolarGeometry.ComputeShadowVector(lonLat.y, dayOfYear, dayPercent);
            if (shadow == Vector2.zero)
                return;

            __result.vector = shadow;
        }
    }
}
