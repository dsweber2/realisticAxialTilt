using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Plant), "TickLong")]
    internal static class PlantLightDormancyPatch
    {
        private const int DarkDayWindow = 3;
        private const float GrowMinGlow = 0.51f;

        [HarmonyPostfix]
        private static void Postfix(Plant __instance, ref int ___madeLeaflessTick)
        {
            if (!__instance.Spawned) return;
            if (__instance.def.plant.dieIfLeafless) return;
            if (__instance.LeaflessNow) return;

            // Only apply to plants exposed to the outdoor sky, not artificially lit/darkened cells.
            float skyGlow  = __instance.Map.skyManager.CurSkyGlow;
            float cellGlow = __instance.Map.glowGrid.GroundGlowAt(__instance.Position);
            if (cellGlow < skyGlow) return;

            Vector2 lonLat = Find.WorldGrid.LongLatOf(__instance.Map.Tile);
            float   lat    = lonLat.y;
            int     today  = GenDate.DayOfYear(GenTicks.TicksAbs, lonLat.x);

            if (HasSufficientPeakLight(lat, today)) return;

            ___madeLeaflessTick = Find.TickManager.TicksGame;
        }

        private static bool HasSufficientPeakLight(float lat, int dayOfYear)
        {
            int daysPerYear = GenDate.DaysPerYear;
            for (int ii = 0; ii < DarkDayWindow; ii++)
            {
                int day = (dayOfYear - ii + daysPerYear) % daysPerYear;
                if (GlowCurvePatch.PeakDailyGlow(lat, day) >= GrowMinGlow)
                    return true;
            }
            return false;
        }
    }
}
