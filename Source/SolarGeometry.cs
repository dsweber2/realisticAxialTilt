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

        internal static void ApplyAxialTilt(float tiltDeg, float k)
        {
            float rad = tiltDeg * Mathf.Deg2Rad;
            sinTilt = Mathf.Sin(rad);
            cosTilt = Mathf.Cos(rad);
            dampingK = k;
        }

        internal static Vector3 ComputeSunPosition(float dayOfYear, float dayPercent, Vector3 initialSunPos)
        {
            float S = Mathf.Sin(dayOfYear / 60f * Mathf.PI * 2f);
            float sinDecl = sinTilt * S;
            float cosDecl = Mathf.Sqrt(Mathf.Max(0f, 1f - sinDecl * sinDecl));
            float tanDecl = cosDecl > 1e-6f ? sinDecl / cosDecl : Mathf.Sign(sinDecl) * 1e6f;

            Vector3 vector = initialSunPos * 100f;
            vector.y += tanDecl * 100f;
            return (Quaternion.AngleAxis((dayPercent - 0.5f) * 360f, Vector3.up) * vector).normalized;
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
        // shadows fade in over this elevation range to avoid absurd lengths
        // passing through other objects at near-horizontal sun
        internal const float ShadowFadeThreshold = 0.25f; // sin(~14.5°)

        internal static Vector2 ComputeShadowVector(float latitude, int dayOfYear, float dayPercent)
        {
            Vector3 sun = ComputeSunPosition((float)dayOfYear, dayPercent, new Vector3(1f, 0f, 0f));

            float latRad = latitude * Mathf.Deg2Rad;
            Vector3 up    = new Vector3(Mathf.Cos(latRad), Mathf.Sin(latRad), 0f);
            Vector3 north = new Vector3(-Mathf.Sin(latRad), Mathf.Cos(latRad), 0f);

            float sinEl = Vector3.Dot(sun, up);
            if (sinEl <= 0f)
                return Vector2.zero;

            float fade = Mathf.Clamp01(sinEl / ShadowFadeThreshold);

            Vector3 sunHoriz = sun - sinEl * up;
            float cosEl = sunHoriz.magnitude;
            if (cosEl < 1e-4f)
                return Vector2.zero;

            float shadowEast  = -Vector3.Dot(sunHoriz, Vector3.forward);
            float shadowNorth = -Vector3.Dot(sunHoriz, north);
            float shadowLength = Mathf.Min(GenCelestial.ShadowMaxLengthDay,
                ShadowRef * cosEl / Mathf.Max(sinEl, ShadowFadeThreshold)) * fade;

            if (shadowLength < 0.01f)
                return Vector2.zero;

            float invMag = shadowLength / cosEl;
            return new Vector2(shadowEast * invMag, shadowNorth * invMag);
        }
    }
}
