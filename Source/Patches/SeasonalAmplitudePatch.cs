using HarmonyLib;
using RealisticAxialTilt.Compat;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(GenTemperature), "SeasonalShiftAmplitudeAt")]
    internal static class SeasonalShiftAmplitudePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PlanetTile tile, ref float __result)
        {
            if (RealisticPlanets2Compat.IsActive) return;
            float lat = Find.WorldGrid.LongLatOf(tile).y;
            __result *= SolarGeometry.SeasonalAmplitudeScale(lat);
        }
    }
}
