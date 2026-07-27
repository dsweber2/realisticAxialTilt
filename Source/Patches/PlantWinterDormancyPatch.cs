using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticAxialTilt.Patches
{
[HarmonyPatch(typeof(Plant), "TickLong")]
    internal static class PlantWinterDormancyPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Plant __instance, ref int ___unlitTicks)
        {
            if (!__instance.Spawned) return;
            if (__instance.Position.Roofed(__instance.Map)) return;
            // Plants that are cold adapted are also darkness adapted.
            // LeaflessNow covers hard-freeze dormancy; the temperature check covers the
            // broader window where the plant is too cold to grow but not yet leafless.
            bool dormant = __instance.LeaflessNow
                || __instance.AmbientTemperature < __instance.def.plant.minGrowthTemperature;
            if (!dormant) return;
            ___unlitTicks = 0;
        }
    }
}
