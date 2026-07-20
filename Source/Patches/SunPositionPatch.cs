using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(GenCelestial), "SunPosition",
        new[] { typeof(float), typeof(int), typeof(float) })]
    internal static class SunPositionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(float latitude, int dayOfYear, float dayPercent, ref Vector3 __result)
        {
            __result = SolarGeometry.ComputeSunPosition((float)dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));
            return false;
        }
    }

    [HarmonyPatch(typeof(GenCelestial), "CurSunPositionInWorldSpace")]
    internal static class CurSunPositionInWorldSpacePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref Vector3 __result)
        {
            int ticks;
            if (Current.ProgramState != ProgramState.Entry)
            {
                ticks = GenTicks.TicksAbs;
            }
            else
            {
                PlanetTile startingTile = Find.GameInitData.startingTile;
                float longitude = startingTile.Valid ? Find.WorldGrid.LongLatOf(startingTile).x : 0f;
                ticks = Mathf.RoundToInt(2500f * (12f - GenDate.TimeZoneFloatAt(longitude)));
            }
            __result = SolarGeometry.ComputeSunPosition(
                GenDate.DayOfYear(ticks, 0f),
                GenDate.DayPercent(ticks, 0f),
                new Vector3(0f, 0f, -1f));
            return false;
        }
    }
}
