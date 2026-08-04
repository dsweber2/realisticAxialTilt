using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RealisticAxialTilt.Patches;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class WorldbuilderCompat
    {
        private static bool? _isActive;
        private static FieldInfo _currentTabField;
        private static FieldInfo _worldGenerationDataField;
        private static FieldInfo _wgdAxialTiltField;
        private static FieldInfo _wgdRiverDensityField;
        private static FieldInfo _wgdRainfallField;
        private static FieldInfo _wgdTemperatureField;
        private static FieldInfo _pageRainfallField;
        private static FieldInfo _pageTemperatureField;

        private const float EarthTilt = 23.45f;
        private const float LabelH   = 22f;
        private const float SliderH  = 30f;
        private const float RowGap   = 8f;
        private const float LatRowH  = 20f;

        // River(30+30) + tilt(LabelH+SliderH+TickAreaH+RowGap) + rainfall(30+30) + temp(30+30)
        // + k(LabelH+SliderH+RowGap) + lat table(LabelH + 5×LatRowH)
        private static float ClimateTabContentH =>
              60f
            + LabelH + SliderH + WorldParamsPatch.TickAreaH + RowGap
            + 60f
            + 60f
            + LabelH + SliderH + RowGap
            + LabelH + 5f * LatRowH;

        internal static bool IsActive
        {
            get
            {
                if (!_isActive.HasValue)
                    _isActive = LoadedModManager.RunningMods.Any(
                        m => m.PackageId.Equals("ferny.Worldbuilder", StringComparison.OrdinalIgnoreCase));
                return _isActive.Value;
            }
        }

        internal static void TryPatch(Harmony harmony)
        {
            if (!IsActive) return;

            Type patchClass = AccessTools.TypeByName("Worldbuilder.Page_CreateWorldParams_DoWindowContents_Patch");
            if (patchClass == null)
            {
                Log.Warning("[RAT] WorldbuilderCompat: Could not find Worldbuilder's DoWindowContents patch class.");
                return;
            }

            _currentTabField = AccessTools.Field(patchClass, "currentTab");

            Type exposeDataClass = AccessTools.TypeByName("Worldbuilder.World_ExposeData_Patch");
            if (exposeDataClass != null)
                _worldGenerationDataField = AccessTools.Field(exposeDataClass, "worldGenerationData");

            Type wgdType = AccessTools.TypeByName("Worldbuilder.WorldGenerationData");
            if (wgdType != null)
            {
                _wgdAxialTiltField    = AccessTools.Field(wgdType, "axialTilt");
                _wgdRiverDensityField = AccessTools.Field(wgdType, "riverDensity");
                _wgdRainfallField     = AccessTools.Field(wgdType, "rainfall");
                _wgdTemperatureField  = AccessTools.Field(wgdType, "temperature");
            }

            _pageRainfallField    = AccessTools.Field(typeof(Page_CreateWorldParams), "rainfall");
            _pageTemperatureField = AccessTools.Field(typeof(Page_CreateWorldParams), "temperature");

            MethodInfo drawClimate = AccessTools.Method(patchClass, "DrawClimateTab");
            if (drawClimate != null)
                harmony.Patch(drawClimate, prefix: new HarmonyMethod(typeof(WorldbuilderCompat), nameof(DrawClimateTab_Prefix)));
            else
                Log.Warning("[RAT] WorldbuilderCompat: Could not find DrawClimateTab.");

            MethodInfo calcHeight = AccessTools.Method(patchClass, "CalculateClimateTabHeight");
            if (calcHeight != null)
                harmony.Patch(calcHeight, postfix: new HarmonyMethod(typeof(WorldbuilderCompat), nameof(CalculateClimateTabHeight_Postfix)));
            else
                Log.Warning("[RAT] WorldbuilderCompat: Could not find CalculateClimateTabHeight.");
        }

        private static void CalculateClimateTabHeight_Postfix(ref float __result)
        {
            if (_currentTabField != null && (int)_currentTabField.GetValue(null) == 2)
                __result = ClimateTabContentH;
        }

        // __0/__1 avoid Harmony's special-casing of __instance for non-static patches.
        // DrawClimateTab(Page_CreateWorldParams __instance, Rect rect) is a private static,
        // so positional injection is unambiguous.
        private static bool DrawClimateTab_Prefix(Page_CreateWorldParams __0, Rect __1)
        {
            var page = __0;
            var rect = __1;
            float y  = rect.y;
            float w  = rect.width;

            object wgd = _worldGenerationDataField?.GetValue(null);
            if (wgd == null) return true; // fall back to WB's original

            // --- River density ---
            float river = (float)(_wgdRiverDensityField?.GetValue(wgd) ?? 1f);
            Widgets.Label(new Rect(rect.x, y, w, SliderH), "WB_RiverDensity".Translate());
            y += SliderH;
            river = Widgets.HorizontalSlider(new Rect(rect.x, y, w, SliderH), river, 0f, 2f,
                middleAlignment: true, "PlanetRainfall_Normal".Translate(), "None".Translate(), "PlanetRainfall_High".Translate(), 0.1f);
            _wgdRiverDensityField?.SetValue(wgd, river);
            y += SliderH;

            // --- Axial tilt (replaces WB's 5-step enum) ---
            float pending = AxialTiltWorldComp.PendingAxialTiltDeg;
            string tiltLabel = Mathf.Abs(pending - EarthTilt) < 0.01f
                ? pending.ToString("F2") + "° (Vanilla)"
                : pending.ToString("F1") + "°";

            Widgets.Label(new Rect(rect.x, y, w, LabelH), "AxialTilt".Translate());
            y += LabelH;
            float rawTilt = Widgets.HorizontalSlider(new Rect(rect.x, y, w, SliderH),
                pending, 0f, 90f, middleAlignment: true, tiltLabel, null, null);
            float snapped = Mathf.Round(rawTilt * 2f) / 2f;
            foreach ((string _, float tickVal) in WorldParamsPatch.GetTicks())
            {
                if (Mathf.Abs(rawTilt - tickVal) < WorldParamsPatch.StickyRadius)
                {
                    snapped = tickVal;
                    break;
                }
            }
            AxialTiltWorldComp.PendingAxialTiltDeg = snapped;
            TooltipHandler.TipRegion(new Rect(rect.x, y - LabelH, w, LabelH + SliderH), "AxialTiltTip".Translate());
            // Keep WB's enum in sync so its amplitude curves use a reasonable value.
            if (_wgdAxialTiltField != null)
            {
                int wbEnum = Mathf.Clamp(Mathf.RoundToInt(snapped / 11.25f), 0, 4);
                _wgdAxialTiltField.SetValue(wgd, Enum.ToObject(_wgdAxialTiltField.FieldType, wbEnum));
            }
            y += SliderH;
            WorldParamsPatch.DrawTicks(y, rect.x, w);
            y += WorldParamsPatch.TickAreaH + RowGap;

            // --- Rainfall ---
            var rainfall = (OverallRainfall)(_pageRainfallField?.GetValue(page) ?? OverallRainfall.Normal);
            Widgets.Label(new Rect(rect.x, y, w, SliderH), "PlanetRainfall".Translate());
            y += SliderH;
            rainfall = (OverallRainfall)Mathf.RoundToInt(Widgets.HorizontalSlider(
                new Rect(rect.x, y, w, SliderH), (float)rainfall,
                0f, OverallRainfallUtility.EnumValuesCount - 1,
                middleAlignment: true,
                "PlanetRainfall_Normal".Translate(), "PlanetRainfall_Low".Translate(), "PlanetRainfall_High".Translate(), 1f));
            _pageRainfallField?.SetValue(page, rainfall);
            _wgdRainfallField?.SetValue(wgd, rainfall);
            y += SliderH;

            // --- Temperature ---
            var temperature = (OverallTemperature)(_pageTemperatureField?.GetValue(page) ?? OverallTemperature.Normal);
            Widgets.Label(new Rect(rect.x, y, w, SliderH), "PlanetTemperature".Translate());
            y += SliderH;
            temperature = (OverallTemperature)Mathf.RoundToInt(Widgets.HorizontalSlider(
                new Rect(rect.x, y, w, SliderH), (float)temperature,
                0f, OverallTemperatureUtility.EnumValuesCount - 1,
                middleAlignment: true,
                "PlanetTemperature_Normal".Translate(), "PlanetTemperature_Low".Translate(), "PlanetTemperature_High".Translate(), 1f));
            _pageTemperatureField?.SetValue(page, temperature);
            _wgdTemperatureField?.SetValue(wgd, temperature);
            y += SliderH;

            // --- Seasonal damping ---
            Widgets.Label(new Rect(rect.x, y, w, LabelH), "SeasonalDamping".Translate());
            y += LabelH;
            float rawK = Widgets.HorizontalSlider(new Rect(rect.x, y, w, SliderH),
                AxialTiltWorldComp.PendingK, 0f, 1f,
                middleAlignment: true, "k = " + AxialTiltWorldComp.PendingK.ToString("F2"),
                null, null, roundTo: 0.05f);
            AxialTiltWorldComp.PendingK = Mathf.Abs(rawK - 0.5f) < 0.05f ? 0.5f : rawK;
            TooltipHandler.TipRegion(new Rect(rect.x, y - LabelH, w, LabelH + SliderH), "SeasonalDampingTip".Translate());
            y += SliderH + RowGap;

            // --- Temperature by latitude (vertical list) ---
            float tilt = AxialTiltWorldComp.PendingAxialTiltDeg;
            float k    = AxialTiltWorldComp.PendingK;
            SimpleCurve tempCurve = temperature.GetTemperatureCurve();

            GameFont savedFont   = Text.Font;
            TextAnchor savedAnch = Text.Anchor;
            Text.Font = GameFont.Tiny;

            Widgets.Label(new Rect(rect.x, y, w, LabelH), "TempRangeTableLabel".Translate());
            y += LabelH;

            string[] latLabels = { "Equator", "22°", "45°", "68°", "Pole" };
            float[]  latDeg    = { 0f, 22.5f, 45f, 67.5f, 90f };
            for (int ii = 0; ii < latLabels.Length; ii++)
            {
                (float tMin, float tMax) = AxialSeasonalTemperature.ApproxTempRange(latDeg[ii], tilt, k);
                if (tempCurve != null)
                {
                    tMin = tempCurve.Evaluate(tMin);
                    tMax = tempCurve.Evaluate(tMax);
                }
                Widgets.Label(new Rect(rect.x, y, w, LatRowH),
                    $"{latLabels[ii]}: {tMin:F0} / {tMax:F0}°C");
                y += LatRowH;
            }

            Text.Font   = savedFont;
            Text.Anchor = savedAnch;

            return false;
        }
    }
}
