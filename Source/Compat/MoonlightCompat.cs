using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    // Soft compatibility with Moonlight (Owlchemist.Moonlight).
    //
    // Moonlight drives sky colour brightness from a fixed 15-step array indexed by
    // GenDate.DayOfSeason. We prefix UpdateMoonlight to overwrite the current day's
    // brightnessMid/brightnessEdge entries with values derived from our continuous
    // lunar phase before the original method reads them.
    internal static class MoonlightCompat
    {
        internal static readonly bool IsActive =
            AccessTools.TypeByName("Moonlight.MoonlightUtility") != null;

        private static FieldInfo _brightnessMid;
        private static FieldInfo _brightnessEdge;
        private static FieldInfo _day;
        private static FieldInfo _darkest;
        private static FieldInfo _brightest;

        internal static void TryPatch(Harmony harmony)
        {
            if (!IsActive) return;

            Type utilType     = AccessTools.TypeByName("Moonlight.MoonlightUtility");
            Type settingsType = AccessTools.TypeByName("Moonlight.ModSettings_Moonlight");
            if (utilType == null || settingsType == null) return;

            _brightnessMid = AccessTools.Field(utilType,     "brightnessMid");
            _brightnessEdge = AccessTools.Field(utilType,    "brightnessEdge");
            _day            = AccessTools.Field(utilType,    "day");
            _darkest        = AccessTools.Field(settingsType, "darkest");
            _brightest      = AccessTools.Field(settingsType, "brightest");

            if (_brightnessMid == null || _day == null || _darkest == null) return;

            MethodInfo target = AccessTools.Method(utilType, "UpdateMoonlight");
            if (target == null) return;

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(MoonlightCompat), nameof(Prefix)));
            Log.Message("[RAT] Patched Moonlight.MoonlightUtility.UpdateMoonlight for phase-based brightness.");
        }

        private static void Prefix()
        {
            if (Current.ProgramState != ProgramState.Playing) return;

            float[] midArray  = (float[])_brightnessMid.GetValue(null);
            float[] edgeArray = (float[])_brightnessEdge.GetValue(null);
            int     day       = (int)_day.GetValue(null);
            float   darkest   = (float)_darkest.GetValue(null);
            float   brightest = (float)_brightest.GetValue(null);

            if (midArray == null || edgeArray == null || day < 0 || day >= midArray.Length) return;

            float phase = (GenTicks.TicksAbs / (RealisticAxialTiltMod.Settings.moonOrbitalDays * GenDate.TicksPerDay)) % 1f;
            float t     = (1f - Mathf.Cos(phase * 2f * Mathf.PI)) / 2f;

            float midBrightness  = Mathf.Lerp(darkest, brightest, t);
            float edgeBrightness = (brightest + midBrightness) / 2f;

            midArray[day]  = midBrightness;
            edgeArray[day] = edgeBrightness;
        }
    }
}
