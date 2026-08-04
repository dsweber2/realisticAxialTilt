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
            // there was some logic here to kill plants under roofs relying on
            // comparing light levels, but since they only get there by player
            // placement, players can deal with the fact that they don't grow
            // and just sit dormant
            if (__instance.def.plant.dieIfLeafless) return;
            if (!__instance.LeaflessNow) return;
            ___unlitTicks = 0;
        }
    }
}
