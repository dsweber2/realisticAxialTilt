using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    // Core solar position and lighting math
    internal static class SolarGeometry
    {
        internal static float sinTilt;
        internal static float cosTilt;
        internal static float dampingK;

        internal static readonly float SinEarthTilt = Mathf.Sin(23.45f * Mathf.Deg2Rad);
        internal static readonly float CosEarthTilt = Mathf.Cos(23.45f * Mathf.Deg2Rad);

        // Backing state for the public Api surface; see Api/RealisticAxialTiltApi.cs.
        internal static float TiltDegrees;
        internal static bool Ready;
        internal static int Generation;

        internal static void ApplyAxialTilt(float tiltDeg, float k)
        {
            float rad = tiltDeg * Mathf.Deg2Rad;
            sinTilt = Mathf.Sin(rad);
            cosTilt = Mathf.Cos(rad);
            dampingK = k;

            TiltDegrees = tiltDeg;
            Ready = true;
            Generation++;
        }

        internal static float DeclinationDegrees(float dayOfYear)
        {
            float S = Mathf.Sin(dayOfYear / 60f * Mathf.PI * 2f);
            return Mathf.Asin(Mathf.Clamp(sinTilt * S, -1f, 1f)) * Mathf.Rad2Deg;
        }

        internal static float ElevationDegrees(float latitude, float dayOfYear, float dayPercent)
        {
            Vector3 sun = ComputeSunPosition(dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));
            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(sun, up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        internal static float AzimuthDegrees(float latitude, float dayOfYear, float dayPercent)
        {
            Vector3 sun = ComputeSunPosition(dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));
            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 north = new Vector3(-Mathf.Sin(latRad), Mathf.Cos(latRad), 0f);

            Vector3 sunHoriz = sun - Vector3.Dot(sun, up) * up;
            if (sunHoriz.magnitude < 1e-4f)
                return 0f;

            float deg = Mathf.Atan2(Vector3.Dot(sunHoriz, Vector3.forward),
                                    Vector3.Dot(sunHoriz, north)) * Mathf.Rad2Deg;
            return Mathf.Repeat(deg, 360f);
        }

        internal static Vector3 ComputeSunPosition(float dayOfYear, float dayPercent, Vector3 initialSunPos)
        {
            float sinDecl = sinTilt * Mathf.Sin(dayOfYear / 60f * Mathf.PI * 2f);
            return ComputeBodyPosition(sinDecl, dayPercent, initialSunPos);
        }

        // Shared by sun and moon: place any ecliptic body given its sin(declination) and day percent.
        private static Vector3 ComputeBodyPosition(float sinDecl, float dayPercent, Vector3 initialPos)
        {
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;

            Vector3 vector = initialPos * 100f;
            vector.y += tanDecl * 100f;
            return (Quaternion.AngleAxis((dayPercent - 0.5f) * 360f, Vector3.up) * vector).normalized;
        }

        // --- Lunar geometry ---
        //
        // The moon is a second body on the inclined orbit, offset from the sun by its elongation
        // (cyclePosition * 360°). The orbital plane is tilted moonInclinationDeg from the ecliptic,
        // with the ascending node regressing retrograde once per year (nodal period = 60 days).
        //
        // Position math: ecliptic (longitude, latitude) → equatorial (declination), then altitude/
        // azimuth via the same ComputeBodyPosition as the sun. The RA correction from inclination
        // is a few degrees at most and is folded into moonDayPercent, which determines the hour angle.

        // Fraction [0,1) through the synodic cycle from the current absolute tick count and settings.
        // 0 = new moon (aligned with sun), 0.5 = full moon (opposite sun).
        internal static float LunarCyclePosition(long ticksAbs)
        {
            float periodTicks = RealisticAxialTiltMod.Settings.moonOrbitalDays * GenDate.TicksPerDay;
            // Positive modulo: works for negative ticksAbs (pre-game start).
            float pos = ticksAbs % periodTicks;
            if (pos < 0f) pos += periodTicks;
            return pos / periodTicks;
        }

        // Ascending node longitude in radians. The node regresses retrograde one full revolution
        // per year (60 days), so Ω = −(dayOfYear/60)*2π.
        private static float LunarNodeAngleRad(float dayOfYear) =>
            -(dayOfYear / 60f) * Mathf.PI * 2f;

        // sin(declination) for the moon, including the inclination of its orbit off the ecliptic.
        // Standard ecliptic-to-equatorial: sin(δ) = sin(β)cos(ε) + cos(β)sin(ε)sin(λ)
        // where λ = ecliptic longitude, β = ecliptic latitude, ε = axial tilt.
        // At inclination 0, β = 0 and this reduces to sinTilt*sin(λ) — the pure-ecliptic result.
        private static float LunarSinDecl(float dayOfYear, float cyclePosition)
        {
            float eclLong = (dayOfYear / 60f + cyclePosition) * Mathf.PI * 2f;
            float argFromNode = eclLong - LunarNodeAngleRad(dayOfYear);
            float eclLatRad = RealisticAxialTiltMod.Settings.moonInclinationDeg * Mathf.Deg2Rad
                              * Mathf.Sin(argFromNode);
            float sinBeta = Mathf.Sin(eclLatRad);
            float cosBeta = Mathf.Cos(eclLatRad);
            return Mathf.Clamp(sinBeta * cosTilt + cosBeta * sinTilt * Mathf.Sin(eclLong), -1f, 1f);
        }

        internal static float LunarDeclinationDegrees(float dayOfYear, float cyclePosition) =>
            Mathf.Asin(LunarSinDecl(dayOfYear, cyclePosition)) * Mathf.Rad2Deg;

        internal static float LunarElevationDegrees(float latitude, float dayOfYear, float dayPercent, float cyclePosition)
        {
            float sinDecl = LunarSinDecl(dayOfYear, cyclePosition);
            float moonDayPercent = Mathf.Repeat(dayPercent - cyclePosition, 1f);
            Vector3 moon = ComputeBodyPosition(sinDecl, moonDayPercent, new Vector3(1f, 0f, 0f));
            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(moon, up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        internal static float LunarAzimuthDegrees(float latitude, float dayOfYear, float dayPercent, float cyclePosition)
        {
            float sinDecl = LunarSinDecl(dayOfYear, cyclePosition);
            float moonDayPercent = Mathf.Repeat(dayPercent - cyclePosition, 1f);
            Vector3 moon = ComputeBodyPosition(sinDecl, moonDayPercent, new Vector3(1f, 0f, 0f));
            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up    = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 north = new Vector3(-Mathf.Sin(latRad), Mathf.Cos(latRad), 0f);

            Vector3 moonHoriz = moon - Vector3.Dot(moon, up) * up;
            if (moonHoriz.magnitude < 1e-4f)
                return 0f;

            float deg = Mathf.Atan2(Vector3.Dot(moonHoriz, Vector3.forward),
                                    Vector3.Dot(moonHoriz, north)) * Mathf.Rad2Deg;
            return Mathf.Repeat(deg, 360f);
        }

        // Returns (moonrise, moonset) as solar dayPercent values, or null for circumpolar.
        // Rise/set are converted from the moon's hour-angle frame back to solar day percent
        // so they can be compared directly with SunriseSunset.
        internal static (float moonrise, float moonset)? ComputeMoonriseMoonset(float latitude, float dayOfYear, float cyclePosition)
        {
            float sinDecl = LunarSinDecl(dayOfYear, cyclePosition);
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;
            float tanLat  = Mathf.Tan(latitude * Mathf.Deg2Rad);
            float cosH0   = -tanDecl * tanLat;

            if (cosH0 <= -1f || cosH0 >= 1f)
                return null;

            float halfDay = Mathf.Acos(cosH0) / (2f * Mathf.PI);
            // Moon-frame rise/set → solar day percent: inverse of moonDayPercent = dayPercent − cyclePosition
            float moonrise = Mathf.Repeat(0.5f - halfDay + cyclePosition, 1f);
            float moonset  = Mathf.Repeat(0.5f + halfDay + cyclePosition, 1f);
            return (moonrise, moonset);
        }

        // dev utility function
        // Returns (sunrise, sunset) as dayPercent values [0,1], or null for polar day/night.
        internal static (float sunrise, float sunset)? ComputeSunriseSunset(float latitude, int dayOfYear)
        {
            float S       = Mathf.Sin(dayOfYear / 60f * Mathf.PI * 2f);
            float sinDecl = sinTilt * S;
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;
            float tanLat  = Mathf.Tan(latitude * Mathf.Deg2Rad);
            float cosH0   = -tanDecl * tanLat;

            if (cosH0 <= -1f || cosH0 >= 1f)
                return null;

            float halfDay = Mathf.Acos(cosH0) / (2f * Mathf.PI);
            return (0.5f - halfDay, 0.5f + halfDay);
        }

        private const float ShadowRef = 2.0f;
        // ShadowStrengthPatch fades opacity near the horizon; length uses the true cotangent,
        // capped at MaxLengthDay so shadows don't stretch absurdly at very low elevations.
        internal const float ShadowFadeThreshold = 0.25f; // sin(~14.5°) — used by ShadowStrengthPatch

        internal static Vector2 ComputeShadowVector(float latitude, int dayOfYear, float dayPercent)
        {
            Vector3 sun = ComputeSunPosition((float)dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));

            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up    = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 north = new Vector3(-Mathf.Sin(latRad), Mathf.Cos(latRad), 0f);

            float sinEl = Vector3.Dot(sun, up);
            if (sinEl <= 0f)
                return Vector2.zero;

            Vector3 sunHoriz = sun - sinEl * up;
            float cosEl = sunHoriz.magnitude;
            if (cosEl < 1e-4f)
                return Vector2.zero;

            float shadowEast  = -Vector3.Dot(sunHoriz, Vector3.forward);
            float shadowNorth = -Vector3.Dot(sunHoriz, north);
            float shadowLength = Mathf.Min(GenCelestial.ShadowMaxLengthDay,
                ShadowRef * cosEl / sinEl);

            float invMag = shadowLength / cosEl;
            return new Vector2(shadowEast * invMag, shadowNorth * invMag);
        }
    }
}
