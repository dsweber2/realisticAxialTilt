using HarmonyLib;
using RimWorld;
using RimWorld.Planet;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GenerateWorld))]
    internal static class WorldGeneratorPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            SolarGeometry.ApplyAxialTilt(
                AxialTiltWorldComp.PendingAxialTiltDeg,
                AxialTiltWorldComp.PendingK);
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "BaseTemperatureAtLatitude")]
    internal static class BaseTemperaturePatch
    {
        [HarmonyPostfix]
        private static void Postfix(float lat, ref float __result)
        {
            __result += SolarGeometry.AnnualTemperatureCorrection(lat);
        }
    }
}
