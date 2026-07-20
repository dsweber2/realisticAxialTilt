using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    internal static class DevTools
    {
        private static readonly FieldInfo TicksGameField =
            AccessTools.Field(typeof(TickManager), "ticksGameInt");

        private static float PlayerLongitude =>
            Find.AnyPlayerHomeMap != null
                ? Find.WorldGrid.LongLatOf(Find.AnyPlayerHomeMap.Tile).x
                : 0f;

        private static void ShiftTicks(int delta)
        {
            int current = (int)TicksGameField.GetValue(Find.TickManager);
            TicksGameField.SetValue(Find.TickManager, current + delta);
        }

        [DebugAction("Realistic Axial Tilt", "Set hour of day")]
        static void SetHour()
        {
            var options = new List<DebugMenuOption>();
            for (int hour = 0; hour < 24; hour += 2)
            {
                int hh = hour;
                options.Add(new DebugMenuOption($"{hh:D2}:00", DebugMenuOptionMode.Action, () =>
                {
                    float lon = PlayerLongitude;
                    float currentFrac = GenDate.DayPercent(Find.TickManager.TicksAbs, lon);
                    float desiredFrac = hh / 24f;
                    ShiftTicks((int)((desiredFrac - currentFrac) * GenDate.TicksPerDay));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "Hour of day"));
        }

        private static readonly string[] QuadrumNames = { "Aprimay", "Jugust", "Septober", "Decembary" };

        [DebugAction("Realistic Axial Tilt", "Set day of year")]
        static void SetDay()
        {
            var options = new List<DebugMenuOption>
            {
                new DebugMenuOption("Day 0  — spring equinox  (1 Aprimay)",  DebugMenuOptionMode.Action, () => JumpToDay(0)),
                new DebugMenuOption("Day 7  — mid-spring      (8 Aprimay)",  DebugMenuOptionMode.Action, () => JumpToDay(7)),
                new DebugMenuOption("Day 15 — summer solstice (1 Jugust)",   DebugMenuOptionMode.Action, () => JumpToDay(15)),
                new DebugMenuOption("Day 22 — midsummer       (8 Jugust)",   DebugMenuOptionMode.Action, () => JumpToDay(22)),
                new DebugMenuOption("Day 30 — autumn equinox  (1 Septober)", DebugMenuOptionMode.Action, () => JumpToDay(30)),
                new DebugMenuOption("Day 37 — mid-autumn      (8 Septober)", DebugMenuOptionMode.Action, () => JumpToDay(37)),
                new DebugMenuOption("Day 45 — winter solstice (1 Decembary)",DebugMenuOptionMode.Action, () => JumpToDay(45)),
                new DebugMenuOption("Day 52 — midwinter       (8 Decembary)",DebugMenuOptionMode.Action, () => JumpToDay(52)),
            };
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options, "Day of year"));
        }

        private static void JumpToDay(int targetDay)
        {
            float lon = PlayerLongitude;
            int currentDay = GenDate.DayOfYear(Find.TickManager.TicksAbs, lon);
            ShiftTicks((targetDay - currentDay) * GenDate.TicksPerDay);
        }
    }
}
