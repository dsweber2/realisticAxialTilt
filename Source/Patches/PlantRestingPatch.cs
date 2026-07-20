using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Plant), "Resting", MethodType.Getter)]
    internal static class PlantRestingPatch
    {
        // 4-hour rest window centered on midnight: 22:00–02:00
        private const float RestStart = 11f / 12f;
        private const float RestEnd   =  1f / 12f;

        [HarmonyPrefix]
        private static bool Prefix(Plant __instance, ref bool __result)
        {
            if (!RealisticAxialTiltMod.Settings.realisticPlantRest)
                return true;

            float dayPercent = GenLocalDate.DayPercent(__instance);
            __result = dayPercent > RestStart || dayPercent < RestEnd;
            return false;
        }
    }
}
