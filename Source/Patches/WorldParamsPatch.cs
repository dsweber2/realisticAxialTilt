using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RealisticAxialTilt.Compat;
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
        internal const float StickyRadius = 1.0f;
        private const float TickLineH = 6f;
        private const float TickLabelH = 16f;
        internal const float TickAreaH = TickLineH + TickLabelH;
        private const float ScrollBarW = 17f;

        // Updated every frame by DrawRATAndEndScroll; used by BeginColumnScrollView
        // the following frame to size the scroll view's viewRect correctly.
        private static float _lastRowBottom = 0f;
        private static float _capturedColWidth;
        private static Vector2 _scrollPosition;

        private static List<(string Label, float Value)> _cachedTicks;

        internal static List<(string Label, float Value)> GetTicks()
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

        // Called by the transpiler in place of Widgets.BeginGroup(rect2).
        // Wraps the entire left column (vanilla rows + mod rows) in a scroll view.
        // The outRect is extended ScrollBarW pixels to the right so the vertical scrollbar
        // sits in the ~18px gap between columns rather than overlapping column content.
        private static void BeginColumnScrollView(Rect colRect)
        {
            _capturedColWidth = colRect.width;
            float ratContentH = 76f + TickAreaH + 3f * RowH;
            // Use previous frame's rowBottom for viewRect sizing.
            // First frame: _lastRowBottom = 0, so contentH == colRect.height (no scroll yet, no overlap).
            // +40f: num2 at EndGroup is the y-pos of the last row label, not past it.
            float contentH = Mathf.Max(_lastRowBottom + 40f + ratContentH, colRect.height);
            Rect outRect  = new Rect(colRect.x, colRect.y, colRect.width + ScrollBarW, colRect.height);
            Rect viewRect = new Rect(0f, 0f, colRect.width, contentH);
            Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);
        }

        // Called by the transpiler in place of Widgets.EndGroup().
        // rowBottom is the live value of num2 from DoWindowContents — loaded directly
        // from the IL so it reflects every mod's row additions regardless of priority.
        private static void DrawRATAndEndScroll(Page_CreateWorldParams instance, float rowBottom)
        {
            _lastRowBottom = rowBottom;

            if (!RealisticPlanets2Compat.IsActive)
            {
                float innerW = _capturedColWidth - 200f;
                // +40f: num2 at EndGroup is the y-pos of the last row label, not past it.
                float sy = rowBottom + 40f;

                float pending = AxialTiltWorldComp.PendingAxialTiltDeg;
                string tiltLabel = Mathf.Abs(pending - EarthTilt) < 0.01f
                    ? pending.ToString("F2") + "° (Vanilla)"
                    : pending.ToString("F1") + "°";

                Rect tiltRow = new Rect(0f, sy, 200f + innerW, 30f);
                Widgets.Label(new Rect(0f, sy, 200f, 30f), "AxialTilt".Translate());
                float rawTilt = Widgets.HorizontalSlider(
                    new Rect(200f, sy, innerW, 30f),
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

                DrawTicks(sy + 30f, 200f, innerW);

                float dampingY = sy + 40f + TickAreaH;
                Rect dampingRow = new Rect(0f, dampingY, 200f + innerW, 30f);
                Widgets.Label(new Rect(0f, dampingY, 200f, 30f), "SeasonalDamping".Translate());
                float rawK = Widgets.HorizontalSlider(
                    new Rect(200f, dampingY, innerW, 30f),
                    AxialTiltWorldComp.PendingK,
                    0f, 1f,
                    middleAlignment: true,
                    "k = " + AxialTiltWorldComp.PendingK.ToString("F2"),
                    null, null,
                    roundTo: 0.05f);
                AxialTiltWorldComp.PendingK = Mathf.Abs(rawK - 0.5f) < 0.05f ? 0.5f : rawK;
                TooltipHandler.TipRegion(dampingRow, "SeasonalDampingTip".Translate());

                var overallTemp = (OverallTemperature)TemperatureField.GetValue(instance);
                SimpleCurve tempCurve = overallTemp.GetTemperatureCurve();
                float tilt = AxialTiltWorldComp.PendingAxialTiltDeg;
                float k    = AxialTiltWorldComp.PendingK;

                float tableY = sy + 76f + TickAreaH;
                float rowW   = 200f + innerW;
                float cellW  = rowW / ColHeaders.Length;

                Widgets.Label(new Rect(0f, tableY, rowW, RowH), "TempRangeTableLabel".Translate());
                tableY += RowH;

                for (int ii = 0; ii < ColHeaders.Length; ii++)
                    Widgets.Label(new Rect(ii * cellW, tableY, cellW, RowH), ColHeaders[ii]);
                tableY += RowH;

                for (int ii = 0; ii < ColHeaders.Length; ii++)
                {
                    (float tMin, float tMax) = AxialSeasonalTemperature.ApproxTempRange(ColLats[ii], tilt, k);
                    if (tempCurve != null)
                    {
                        tMin = tempCurve.Evaluate(tMin);
                        tMax = tempCurve.Evaluate(tMax);
                    }
                    string rangeStr = tMin.ToString("F0") + " / " + tMax.ToString("F0") + "°C";
                    Widgets.Label(new Rect(ii * cellW, tableY, cellW, RowH), rangeStr);
                }

            }

            Widgets.EndScrollView();
        }

        // Redraws any suppressed mod buttons in the correct bottom-bar position,
        // laid out right-to-left from next to "Reset factions".
        [HarmonyPatch(nameof(Page_CreateWorldParams.DoWindowContents))]
        [HarmonyPostfix]
        static void DrawCompatButtons(Page_CreateWorldParams __instance, Rect rect)
        {
            // Dialog is 1020px wide. 201.5px sits between Back and Reset all.
            // Three 8.5f gaps + two buttons → btnW = 88f fits exactly on either side.
            const float btnW = 150f;
            const float gap  = 8.5f;
            float y = rect.yMax - 38f;
            // Anchor from the right edge of back
            float x = 150f + 2f * gap; // 150f = Page.BottomButSize.x; left of Generate

            if (Compat.FactionControlCompat.IsActive)
            {
                Compat.FactionControlCompat.DrawBottomButton(new Rect(x, y, btnW, 38f));
                x += btnW;
                x += gap;
            }
            if (Compat.RimWarCompat.IsActive)
            {
                x += 2f * btnW; // center two buttons
                x += 4f * gap;
                Compat.RimWarCompat.DrawBottomButton(new Rect(x, y, btnW, 38f), __instance);
            }
        }

        // Replaces vanilla's BeginGroup/EndGroup with scroll-view wrappers so every row
        // in the left column — vanilla, mod-injected, and ours — scrolls as a single unit.
        //
        // Priority is set very low (1) so this transpiler sees the IL after all other mods
        // have injected their rows. We then inject `ldloc num2` directly before our call,
        // so DrawRATAndEndScroll receives the live runtime value of num2 regardless of which
        // mods added rows or at what priority their transpilers ran.
        [HarmonyPatch(nameof(Page_CreateWorldParams.DoWindowContents))]
        [HarmonyTranspiler]
        [HarmonyPriority(1)]
        static IEnumerable<CodeInstruction> WrapLeftColumnInScroll(IEnumerable<CodeInstruction> instructions)
        {
            var list        = instructions.ToList();
            var beginGroup  = AccessTools.Method(typeof(Widgets), nameof(Widgets.BeginGroup));
            var endGroup    = AccessTools.Method(typeof(Widgets), nameof(Widgets.EndGroup));
            var beginScroll = AccessTools.Method(typeof(WorldParamsPatch), nameof(BeginColumnScrollView));
            var drawAndEnd  = AccessTools.Method(typeof(WorldParamsPatch), nameof(DrawRATAndEndScroll));

            int num2Idx    = FindNum2Local(list);
            bool foundBegin = false;

            for (int ii = 0; ii < list.Count; ii++)
            {
                if (!foundBegin && list[ii].Calls(beginGroup))
                {
                    list[ii] = new CodeInstruction(OpCodes.Call, beginScroll).WithLabels(list[ii].labels);
                    list[ii].labels.Clear();
                    list[ii].labels.AddRange(instructions.ToList()[ii].labels); // preserve branch targets
                    foundBegin = true;
                }
                else if (foundBegin && list[ii].Calls(endGroup))
                {
                    var endInstr = list[ii];
                    // Replace EndGroup with DrawRATAndEndScroll(instance, rowBottom).
                    list[ii] = new CodeInstruction(OpCodes.Call, drawAndEnd);

                    // Push rowBottom (num2) — use live local if found, else 0f.
                    if (num2Idx >= 0)
                        list.Insert(ii, MakeLdloc(list, num2Idx));
                    else
                        list.Insert(ii, new CodeInstruction(OpCodes.Ldc_R4, 0f));

                    // Push instance (this) — transfer labels from original EndGroup.
                    var ldarg0 = new CodeInstruction(OpCodes.Ldarg_0);
                    ldarg0.labels.AddRange(endInstr.labels);
                    list.Insert(ii, ldarg0);
                    break;
                }
            }

            if (!foundBegin)
                Log.Warning("[RealisticAxialTilt] WrapLeftColumnInScroll: BeginGroup not found; scroll not applied.");

            return list;
        }

        internal static void DrawTicks(float y, float sliderX, float sliderWidth)
        {
            var ticks = GetTicks();
            if (ticks.Count == 0)
                return;

            GameFont savedFont = Text.Font;
            var savedAnchor = Text.Anchor;
            Color savedColor = GUI.color;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);

            const float labelW = 70f;
            const float halfLabel = labelW * 0.5f;
            float rightEdge = sliderX + sliderWidth;

            foreach ((string label, float value) in ticks)
            {
                float xCenter = sliderX + 6f + (value / 90f) * (sliderWidth - 12f);
                Widgets.DrawLineVertical(xCenter, y, TickLineH);

                Rect labelRect;
                Text.Anchor = TextAnchor.UpperCenter;
                labelRect = new Rect(xCenter - halfLabel, y + TickLineH, labelW, TickLabelH);
                Widgets.Label(labelRect, label);
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
            AxialTiltWorldComp.PendingK = 0.5f;
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
            Log.Warning("[RealisticAxialTilt] WrapLeftColumnInScroll: could not locate num2 local; using 0 as rowBottom fallback.");
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
    }
}
