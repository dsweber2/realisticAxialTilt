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
            if (RealisticPlanets2Compat.IsActive || WorldbuilderCompat.IsActive) return;
            float lat = Find.WorldGrid.LongLatOf(tile).y;
            __result *= AxialSeasonalTemperature.SeasonalAmplitudeScale(lat);
        }
    }
}
