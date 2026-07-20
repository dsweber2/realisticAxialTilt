using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Page_CreateWorldParams))]
    internal static class WorldParamsPatch
    {
        private static readonly MethodInfo GetMainRect =
            AccessTools.Method(typeof(Page), "GetMainRect",
                new[] { typeof(Rect), typeof(float), typeof(bool) });

        private static readonly FieldInfo TemperatureField =
            AccessTools.Field(typeof(Page_CreateWorldParams), "temperature");

        private static readonly string[] ColHeaders = { "Equator", "22°", "45°", "68°", "Pole" };
        private static readonly float[] ColLats    = { 0f, 22.5f, 45f, 67.5f, 90f };

        private const float RowH = 22f;
        private const float EarthTilt = 23.45f;
        private const float StickyRadius = 1.0f;
        private const float TickLineH = 6f;
        private const float TickLabelH = 16f;
        private const float TickAreaH = TickLineH + TickLabelH;

        private static List<(string Label, float Value)> _cachedTicks;

        private static List<(string Label, float Value)> GetTicks()
        {
            if (_cachedTicks != null)
                return _cachedTicks;

            _cachedTicks = new List<(string, float)>();
            foreach (string entry in "AxialTiltTicks".Translate().ToString().Split(','))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts.Length == 2 && float.TryParse(parts[1].Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                    _cachedTicks.Add((parts[0].Trim(), val));
            }
            return _cachedTicks;
        }

        [HarmonyPatch(nameof(Page_CreateWorldParams.DoWindowContents))]
        [HarmonyPostfix]
        static void DrawSlider(Page_CreateWorldParams __instance, Rect rect)
        {
            Rect mainRect = (Rect)GetMainRect.Invoke(__instance, new object[] { rect, 0f, false });
            float colWidth = (mainRect.width - 18f) * 0.5f;
            float sliderWidth = colWidth - 200f;

            int rows = 6;
            if (ModsConfig.OdysseyActive) rows++;
            if (ModsConfig.BiotechActive) rows++;
            if (!TutorSystem.TutorialMode) rows++;
            float yPos = rows * 40f + 40f;

            Widgets.BeginGroup(new Rect(mainRect.x, mainRect.y, colWidth, mainRect.height));

            float pending = AxialTiltWorldComp.PendingAxialTiltDeg;
            string tiltLabel = Mathf.Abs(pending - EarthTilt) < 0.01f
                ? pending.ToString("F2") + "° (Vanilla)"
                : pending.ToString("F1") + "°";

            Rect tiltRow = new Rect(0f, yPos, 200f + sliderWidth, 30f);
            Widgets.Label(new Rect(0f, yPos, 200f, 30f), "AxialTilt".Translate());
            float rawTilt = Widgets.HorizontalSlider(
                new Rect(200f, yPos, sliderWidth, 30f),
                pending, 0f, 90f,
                middleAlignment: true,
                tiltLabel, null, null);
            float snapped = Mathf.Round(rawTilt * 2f) / 2f;
            foreach ((string _, float tickVal) in GetTicks())
            {
                if (Mathf.Abs(rawTilt - tickVal) < StickyRadius)
                {
                    snapped = tickVal;
                    break;
                }
            }
            AxialTiltWorldComp.PendingAxialTiltDeg = snapped;
            TooltipHandler.TipRegion(tiltRow, "AxialTiltTip".Translate());

            DrawTicks(yPos + 30f, 200f, sliderWidth);

            float dampingY = yPos + 40f + TickAreaH;
            Rect dampingRow = new Rect(0f, dampingY, 200f + sliderWidth, 30f);
            Widgets.Label(new Rect(0f, dampingY, 200f, 30f), "SeasonalDamping".Translate());
            AxialTiltWorldComp.PendingK = Widgets.HorizontalSlider(
                new Rect(200f, dampingY, sliderWidth, 30f),
                AxialTiltWorldComp.PendingK,
                0f, 1f,
                middleAlignment: true,
                "k = " + AxialTiltWorldComp.PendingK.ToString("F2"),
                null, null,
                roundTo: 0.05f);
            TooltipHandler.TipRegion(dampingRow, "SeasonalDampingTip".Translate());

            var overallTemp = (OverallTemperature)TemperatureField.GetValue(__instance);
            SimpleCurve tempCurve = overallTemp.GetTemperatureCurve();
            float tilt = AxialTiltWorldComp.PendingAxialTiltDeg;
            float k    = AxialTiltWorldComp.PendingK;

            float tableY = yPos + 76f + TickAreaH;
            float rowW   = 200f + sliderWidth;
            float cellW  = rowW / ColHeaders.Length;

            Widgets.Label(new Rect(0f, tableY, rowW, RowH), "TempRangeTableLabel".Translate());
            tableY += RowH;

            for (int ii = 0; ii < ColHeaders.Length; ii++)
                Widgets.Label(new Rect(ii * cellW, tableY, cellW, RowH), ColHeaders[ii]);
            tableY += RowH;

            for (int ii = 0; ii < ColHeaders.Length; ii++)
            {
                (float tMin, float tMax) = SolarGeometry.ApproxTempRange(ColLats[ii], tilt, k);
                if (tempCurve != null)
                {
                    tMin = tempCurve.Evaluate(tMin);
                    tMax = tempCurve.Evaluate(tMax);
                }
                string rangeStr = tMin.ToString("F0") + " / " + tMax.ToString("F0") + "°C";
                Widgets.Label(new Rect(ii * cellW, tableY, cellW, RowH), rangeStr);
            }

            Widgets.EndGroup();
        }

        private static void DrawTicks(float y, float sliderX, float sliderWidth)
        {
            var ticks = GetTicks();
            if (ticks.Count == 0)
                return;

            GameFont savedFont = Text.Font;
            var savedAnchor = Text.Anchor;
            Color savedColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);

            foreach ((string label, float value) in ticks)
            {
                float xCenter = sliderX + (value / 90f) * sliderWidth;
                Widgets.DrawLineVertical(xCenter, y, TickLineH);
                float labelW = 70f;
                float labelX = Mathf.Clamp(xCenter - labelW * 0.5f, 0f, sliderX + sliderWidth - labelW);
                Widgets.Label(new Rect(labelX, y + TickLineH, labelW, TickLabelH), label);
            }

            Text.Font = savedFont;
            Text.Anchor = savedAnchor;
            GUI.color = savedColor;
        }

        [HarmonyPatch("Reset")]
        [HarmonyPostfix]
        static void ResetTilt()
        {
            AxialTiltWorldComp.PendingAxialTiltDeg = 23.45f;
            AxialTiltWorldComp.PendingK = 1.0f;
        }
    }
}
