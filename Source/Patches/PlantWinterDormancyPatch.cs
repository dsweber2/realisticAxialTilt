using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Plant), "TickLong")]
    internal static class PlantWinterDormancyPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Plant __instance, ref int ___unlitTicks)
        {
            // Plants that are cold adapted are also darkness adapted
            if (!__instance.LeaflessNow) return;
            if (!__instance.Spawned) return;
            if (__instance.Position.Roofed(__instance.Map)) return;
            ___unlitTicks = 0;
        }
    }
}
