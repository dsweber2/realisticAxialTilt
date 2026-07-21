using HarmonyLib;
using RealisticAxialTilt.Compat;
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
            if (RealisticPlanets2Compat.IsActive)
            {
                SolarGeometry.ApplyAxialTilt(RealisticPlanets2Compat.GetTiltDegrees(), 1.0f);
                return;
            }
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
            if (RealisticPlanets2Compat.IsActive) return;
            __result += SolarGeometry.AnnualTemperatureCorrection(lat);
        }
    }
}
