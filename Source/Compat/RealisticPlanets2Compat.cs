using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Compat
{
    internal static class RealisticPlanets2Compat
    {
        private static bool? _isActive;
        private static FieldInfo _axialTiltField;

        private static Type _climateDataType;
        private static FieldInfo _annualMeanTempField;
        private static FieldInfo _seasonalAmpField;
        private static FieldInfo _maxSummerTempField;
        private static FieldInfo _minWinterTempField;
        private static FieldInfo _latitudeSignedField;

        internal static bool IsActive
        {
            get
            {
                if (!_isActive.HasValue)
                    _isActive = LoadedModManager.RunningMods.Any(
                        m => m.PackageId.Equals("koth.RealisticPlanets2", StringComparison.OrdinalIgnoreCase));
                return _isActive.Value;
            }
        }

        // Reads Planets_GameComponent.axialTilt (static enum field) and converts to degrees.
        // Enum values: VeryLow=0→0°, Low=1→11.25°, Normal=2→22.5°, High=3→33.75°, VeryHigh=4→45°
        internal static float GetTiltDegrees()
        {
            if (_axialTiltField == null)
            {
                Type compType = AccessTools.TypeByName("Planets.Planets_GameComponent");
                if (compType != null)
                    _axialTiltField = AccessTools.Field(compType, "axialTilt");
            }
            if (_axialTiltField == null)
                return 23.45f;

            return (int)_axialTiltField.GetValue(null) * 11.25f;
        }

        // After RP2's world gen pipeline completes, applies RAT's axial-tilt-based annual mean
        // temperature correction to RP2's precomputed ClimateData arrays and tile.temperature.
        // RP2's TemperatureLayer uses a fixed cos(lat)^1.4 curve regardless of tilt, so the
        // equator-to-pole gradient is wrong at non-Earth tilts; this corrects that.
        internal static void ApplyAnnualTempCorrection()
        {
            if (!EnsureClimateDataFields()) return;

            WorldComponent comp = Find.World.components.FirstOrDefault(
                c => c.GetType() == _climateDataType);
            if (comp == null)
            {
                Log.Warning("[RAT/RP2] ClimateData world component not found; skipping annual temp correction.");
                return;
            }

            float[] annualMeanTemp = _annualMeanTempField.GetValue(comp) as float[];
            float[] seasonalAmp    = _seasonalAmpField.GetValue(comp) as float[];
            float[] maxSummerTemp  = _maxSummerTempField.GetValue(comp) as float[];
            float[] minWinterTemp  = _minWinterTempField.GetValue(comp) as float[];
            float[] latitudeSigned = _latitudeSignedField.GetValue(comp) as float[];

            if (annualMeanTemp == null || latitudeSigned == null) return;

            List<Tile> tiles = Find.WorldGrid.Surface.Tiles;
            int tileCount = annualMeanTemp.Length;

            for (int ii = 0; ii < tileCount; ii++)
            {
                float latDeg = latitudeSigned[ii] * Mathf.Rad2Deg;
                float correction = SolarGeometry.AnnualTemperatureCorrection(latDeg);

                annualMeanTemp[ii] += correction;

                if (seasonalAmp != null)
                {
                    float halfAmp = seasonalAmp[ii] * 0.5f;
                    if (maxSummerTemp != null) maxSummerTemp[ii] = annualMeanTemp[ii] + halfAmp;
                    if (minWinterTemp != null) minWinterTemp[ii] = annualMeanTemp[ii] - halfAmp;
                }

                if (ii < tiles.Count)
                    tiles[ii].temperature = annualMeanTemp[ii];
            }
        }

        private static bool EnsureClimateDataFields()
        {
            if (_climateDataType != null) return true;

            _climateDataType = AccessTools.TypeByName("Planets.WorldGen.Climate.ClimateData");
            if (_climateDataType == null)
            {
                Log.Warning("[RAT/RP2] Could not find Planets.WorldGen.Climate.ClimateData; skipping annual temp correction.");
                return false;
            }

            _annualMeanTempField  = AccessTools.Field(_climateDataType, "AnnualMeanTemp");
            _seasonalAmpField     = AccessTools.Field(_climateDataType, "SeasonalAmp");
            _maxSummerTempField   = AccessTools.Field(_climateDataType, "MaxSummerTemp");
            _minWinterTempField   = AccessTools.Field(_climateDataType, "MinWinterTemp");
            _latitudeSignedField  = AccessTools.Field(_climateDataType, "LatitudeSigned");

            return _annualMeanTempField != null && _latitudeSignedField != null;
        }
    }
}
