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

        private static string HourStr(float dayPercent)
        {
            float totalMinutes = dayPercent * 24f * 60f;
            int hh = (int)(totalMinutes / 60f) % 24;
            int mm = (int)(totalMinutes % 60f);
            return $"{hh:D2}:{mm:D2}";
        }

        private static void JumpToDay(int targetDay)
        {
            float lon = PlayerLongitude;
            int currentDay = GenDate.DayOfYear(Find.TickManager.TicksAbs, lon);
            ShiftTicks((targetDay - currentDay) * GenDate.TicksPerDay);
        }

        [DebugAction("Realistic Axial Tilt", "Log sun & glow state")]
        static void LogSunState()
        {
            Map map = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
            if (map == null) { Log.Warning("[RAT] No map available."); return; }

            Vector2 lonLat = Find.WorldGrid.LongLatOf(map.Tile);
            float lat      = lonLat.y;
            int dayOfYear  = GenDate.DayOfYear(GenTicks.TicksAbs, lonLat.x);
            float dayPct   = GenLocalDate.DayPercent(map);

            Vector3 sun   = SolarGeometry.ComputeSunPosition((float)dayOfYear, dayPct, new Vector3(1f, 0f, 0f));
            float latRad  = lat * Mathf.Deg2Rad;
            Vector3 up    = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 north = new Vector3(-Mathf.Sin(latRad), Mathf.Cos(latRad), 0f);

            float sinEl  = Vector3.Dot(sun, up);
            float elDeg  = Mathf.Asin(Mathf.Clamp(sinEl, -1f, 1f)) * Mathf.Rad2Deg;

            Vector3 sunHoriz = sun - sinEl * up;
            float   cosEl    = sunHoriz.magnitude;
            string  azStr    = "N/A (zenith)";
            if (cosEl > 1e-4f)
            {
                float sunE = Vector3.Dot(sunHoriz, Vector3.forward) / cosEl;
                float sunN = Vector3.Dot(sunHoriz, north) / cosEl;
                float az   = Mathf.Atan2(sunE, sunN) * Mathf.Rad2Deg;
                if (az < 0f) az += 360f;
                azStr = $"{az:F1}°";
            }

            float   glow   = GenCelestial.CurCelestialSunGlow(map);
            Vector2 shadow = SolarGeometry.ComputeShadowVector(lat, dayOfYear, dayPct);

            var    srss   = SolarGeometry.ComputeSunriseSunset(lat, dayOfYear);
            string polar  = sinEl > 0f ? "polar day" : "polar night";
            string srStr  = srss.HasValue ? HourStr(srss.Value.sunrise) : polar;
            string ssStr  = srss.HasValue ? HourStr(srss.Value.sunset)  : polar;

            float   moonPhase   = SolarGeometry.LunarCyclePosition(GenTicks.TicksAbs);
            float   moonElDeg   = SolarGeometry.LunarElevationDegrees(lat, dayOfYear, dayPct, moonPhase);
            float   moonAzDeg   = SolarGeometry.LunarAzimuthDegrees(lat, dayOfYear, dayPct, moonPhase);
            string  moonAzStr   = moonElDeg > -89.9f && moonElDeg < 89.9f ? $"{moonAzDeg:F1}°" : "N/A (zenith)";
            string phaseLabel = moonPhase < 0.05f || moonPhase > 0.95f ? "new"
                              : moonPhase < 0.45f ? "waxing"
                              : moonPhase < 0.55f ? "full"
                              : "waning";

            Log.Message(
                $"[RAT] lat={lat:F1}°  day={dayOfYear}  time={dayPct * 24f:F2}h\n" +
                $"  elevation={elDeg:F1}°  azimuth={azStr}\n" +
                $"  sunrise={srStr}  sunset={ssStr}\n" +
                $"  glow={glow:F3}  isDaytime={GenCelestial.IsDaytime(glow)}\n" +
                $"  shadow=(E={shadow.x:F2}, N={shadow.y:F2})  |shadow|={shadow.magnitude:F2}");
        }
    }
}
