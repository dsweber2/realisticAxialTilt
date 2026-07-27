using System;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class NicePlantsMenuCompat
    {
        internal static void TryPatch(Harmony harmony)
        {
            Type growUtility = AccessTools.TypeByName("NicePlantsMenu.GrowUtility");
            if (growUtility == null) return;

            MethodInfo target = AccessTools.Method(growUtility, "CalculateLongitudeTuning");
            if (target == null) return;

            MethodInfo postfix = AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(CalculateLongitudeTuning_Postfix));
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Log.Message("[RAT] Patched NicePlantsMenu.GrowUtility.CalculateLongitudeTuning for axial tilt.");
        }

        private static void CalculateLongitudeTuning_Postfix(int tile, ref float __result)
        {
            if (tile == -1) return;
            float lat = Find.WorldGrid.LongLatOf((PlanetTile)tile).y;
            __result *= SolarGeometry.SeasonalAmplitudeScale(lat);
        }
    }
}
