using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        // Captured by the transpiler just before vanilla's EndGroup — holds the final
        // value of num2 (the running y-offset) after all mods have added their rows.
        private static float _lastRowBottom = 5 * 40f; // fallback: Population row


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

        // Runs after all other transpilers (Priority.Last = 100) so num2 already
        // includes any rows injected by third-party mods (e.g. Geological Landforms).
        [HarmonyPatch(nameof(Page_CreateWorldParams.DoWindowContents))]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Last)]
        static IEnumerable<CodeInstruction> CaptureRowBottom(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var endGroup     = AccessTools.Method(typeof(Widgets), nameof(Widgets.EndGroup));
            var lastRowField = AccessTools.Field(typeof(WorldParamsPatch), nameof(_lastRowBottom));

            int num2Idx = FindNum2Local(list);
            for (int ii = 0; ii < list.Count; ii++)
            {
                if (list[ii].Calls(endGroup))
                {
                    if (num2Idx >= 0)
                    {
                        // Transfer labels so any branch targeting EndGroup still lands here.
                        var ldloc = MakeLdloc(list, num2Idx);
                        ldloc.labels.AddRange(list[ii].labels);
                        list[ii].labels.Clear();
                        list.Insert(ii, new CodeInstruction(OpCodes.Stsfld, lastRowField));
                        list.Insert(ii, ldloc);
                    }
                    break;
                }
            }
            return list;
        }

        // Finds the local variable index for num2 by looking for the first
        // `ldloc X; ldc.r4 40f; add; stloc X` pattern (a row-height increment).
        private static int FindNum2Local(List<CodeInstruction> list)
        {
            for (int ii = 0; ii + 3 < list.Count; ii++)
            {
                if (!IsLdloc(list[ii])) continue;
                if (list[ii + 1].opcode != OpCodes.Ldc_R4 || (float)list[ii + 1].operand != 40f) continue;
                if (list[ii + 2].opcode != OpCodes.Add) continue;
                if (!IsStloc(list[ii + 3])) continue;
                int loadIdx  = LocalIndex(list[ii]);
                int storeIdx = LocalIndex(list[ii + 3]);
                if (loadIdx == storeIdx && loadIdx >= 0)
                    return loadIdx;
            }
            Log.Warning("[RealisticAxialTilt] CaptureRowBottom: could not locate row-position local; falling back to hardcoded offset.");
            return -1;
        }

        private static int LocalIndex(CodeInstruction ci)
        {
            if (ci.opcode == OpCodes.Ldloc_0 || ci.opcode == OpCodes.Stloc_0) return 0;
            if (ci.opcode == OpCodes.Ldloc_1 || ci.opcode == OpCodes.Stloc_1) return 1;
            if (ci.opcode == OpCodes.Ldloc_2 || ci.opcode == OpCodes.Stloc_2) return 2;
            if (ci.opcode == OpCodes.Ldloc_3 || ci.opcode == OpCodes.Stloc_3) return 3;
            return ci.operand switch
            {
                LocalBuilder lb => lb.LocalIndex,
                int n           => n,
                byte b          => b,
                _               => -1
            };
        }

        private static bool IsStloc(CodeInstruction ci) =>
            ci.opcode == OpCodes.Stloc   || ci.opcode == OpCodes.Stloc_S  ||
            ci.opcode == OpCodes.Stloc_0 || ci.opcode == OpCodes.Stloc_1  ||
            ci.opcode == OpCodes.Stloc_2 || ci.opcode == OpCodes.Stloc_3;

        private static bool IsLdloc(CodeInstruction ci) =>
            ci.opcode == OpCodes.Ldloc   || ci.opcode == OpCodes.Ldloc_S  ||
            ci.opcode == OpCodes.Ldloc_0 || ci.opcode == OpCodes.Ldloc_1  ||
            ci.opcode == OpCodes.Ldloc_2 || ci.opcode == OpCodes.Ldloc_3;

        // Clones an existing ldloc instruction for the given local so the opcode
        // and operand type exactly match what the IL already uses for that slot.
        private static CodeInstruction MakeLdloc(List<CodeInstruction> list, int index)
        {
            foreach (var ci in list)
                if (IsLdloc(ci) && LocalIndex(ci) == index)
                    return new CodeInstruction(ci.opcode, ci.operand);
            return index switch
            {
                0 => new CodeInstruction(OpCodes.Ldloc_0),
                1 => new CodeInstruction(OpCodes.Ldloc_1),
                2 => new CodeInstruction(OpCodes.Ldloc_2),
                3 => new CodeInstruction(OpCodes.Ldloc_3),
                _ when index <= 255 => new CodeInstruction(OpCodes.Ldloc_S, (byte)index),
                _ => new CodeInstruction(OpCodes.Ldloc, index)
            };
        }

        [HarmonyPatch(nameof(Page_CreateWorldParams.DoWindowContents))]
        [HarmonyPostfix]
        static void DrawSlider(Page_CreateWorldParams __instance, Rect rect)
        {
            Rect mainRect = (Rect)GetMainRect.Invoke(__instance, new object[] { rect, 0f, false });
            float colWidth = (mainRect.width - 18f) * 0.5f;
            float sliderWidth = colWidth - 200f;

            float yPos = _lastRowBottom + 40f;

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
