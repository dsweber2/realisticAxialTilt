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

    // Fires after RP2's GenerateFresh prefix (which returns false, replacing the method,
    // but Harmony still runs postfixes). Applies RAT's axial-tilt correction to RP2's
    // precomputed climate arrays and tile temperatures.
    [HarmonyPatch(typeof(WorldGenStep_Terrain), "GenerateFresh")]
    internal static class RP2AnnualTempCorrectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!RealisticPlanets2Compat.IsActive) return;
            RealisticPlanets2Compat.ApplyAnnualTempCorrection();
        }
    }
}
