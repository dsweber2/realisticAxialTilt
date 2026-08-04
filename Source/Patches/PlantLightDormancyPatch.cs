using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Plant), "TickLong")]
    internal static class PlantLightDormancyPatch
    {
        private const int DarkDayWindow = 3;

        [HarmonyPostfix]
        private static void Postfix(Plant __instance, ref int ___madeLeaflessTick, int ___unlitTicks)
        {
            if (!__instance.Spawned) return;
            if (__instance.def.plant.dieIfLeafless) return;
            if (__instance.LeaflessNow) return;
            if (___unlitTicks < DarkDayWindow * GenDate.TicksPerDay) return;
            ___madeLeaflessTick = Find.TickManager.TicksGame;
        }
    }
}
