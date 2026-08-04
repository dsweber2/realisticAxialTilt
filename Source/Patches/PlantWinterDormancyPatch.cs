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
            // Skip if the cell is darker than the current outdoor sky — it's in a genuinely
            // dark location (cave, sealed room), not just experiencing a dark winter.
            float skyGlow = __instance.Map.skyManager.CurSkyGlow;
            float cellGlow = __instance.Map.glowGrid.GroundGlowAt(__instance.Position);
            if (cellGlow < skyGlow) return;
            if (__instance.def.plant.dieIfLeafless) return;
            ___unlitTicks = 0;
        }
    }
}
