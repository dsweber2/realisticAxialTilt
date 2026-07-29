using Verse;

namespace RealisticAxialTilt.Api
{
    // Stable public surface for other mods. Everything else in this assembly is internal and
    // free to change; these members are a contract consumers bind to by reflection.
    public static class RealisticAxialTiltApi
    {
        // Bump on breaking changes only; additive changes leave it alone. Consumers gate on >=.
        public const int ApiVersion = 1;

        // Geometry is degenerate until AxialTiltWorldComp.FinalizeInit seeds the tilt
        // (cosTilt defaults to 0, not 1). Check before trusting anything below.
        public static bool GeometryReady => SolarGeometry.Ready;

        // Bumped on each (re)seed. Consumers holding derived caches should drop them on change.
        public static int GeometryGeneration => SolarGeometry.Generation;

        // This world's obliquity, 0..90 degrees. 23.45 is Earth-like.
        public static float AxialTiltDegrees => SolarGeometry.TiltDegrees;

        // Seasonal damping, 0..1. Does not enter the geometry below.
        public static float SeasonalDampingK => SolarGeometry.dampingK;

        // Angles in degrees, latitude signed (+N/-S), dayOfYear 0-based over a 60-day year,
        // dayPercent [0,1) with 0.5 == local solar noon. No Map/Find access.

        // Carries both this world's tilt and our seasonal phase. Prefer this over re-deriving
        // declination from AxialTiltDegrees, which would drift if we re-phase the year.
        public static float SolarDeclinationDegrees(float dayOfYear) =>
            SolarGeometry.DeclinationDegrees(dayOfYear);

        // Geometric elevation; no atmospheric refraction applied, so the horizon is 0.
        public static float SolarElevationDegrees(float latitudeDeg, float dayOfYear, float dayPercent) =>
            SolarGeometry.ElevationDegrees(latitudeDeg, dayOfYear, dayPercent);

        // Clockwise from north, [0,360). Returns 0 at zenith/nadir where azimuth is undefined.
        public static float SolarAzimuthDegrees(float latitudeDeg, float dayOfYear, float dayPercent) =>
            SolarGeometry.AzimuthDegrees(latitudeDeg, dayOfYear, dayPercent);

        // null for polar day and polar night alike; disambiguate with the sign of
        // SolarElevationDegrees(lat, dayOfYear, 0.5f).
        public static (float sunrise, float sunset)? SunriseSunset(float latitudeDeg, int dayOfYear) =>
            SolarGeometry.ComputeSunriseSunset(latitudeDeg, dayOfYear);

        // Lighting handover. A mod that renders its own sun/shadows/glow claims this once and
        // every lighting patch here stands down; tilt gameplay (temperature, seasonal amplitude,
        // plant rest/dormancy, world-params UI) is unaffected.
        public static bool LightingSuppressed =>
            lightingOwner != null
            || (RealisticAxialTiltMod.Settings?.suppressLighting ?? false);

        // Claimant's package id, or null. Shown in our settings screen so players can see who
        // took over.
        public static string LightingOwner => lightingOwner;

        private static string lightingOwner;

        // Idempotent for the same owner. Returns false if another mod already holds the claim —
        // two lighting mods at once is a real conflict, so this is first-wins and logs it.
        public static bool TryClaimLighting(string ownerPackageId)
        {
            if (ownerPackageId.NullOrEmpty())
                return false;

            if (lightingOwner != null && lightingOwner != ownerPackageId)
            {
                Log.Warning($"[RAT] {ownerPackageId} tried to claim lighting but {lightingOwner} holds it. "
                    + "Both mods render their own sun and shadows; disable one.");
                return false;
            }

            if (lightingOwner == null)
                Log.Message($"[RAT] Lighting claimed by {ownerPackageId}; RAT lighting patches standing down.");

            lightingOwner = ownerPackageId;
            return true;
        }

        // Only the holder can release.
        public static void ReleaseLighting(string ownerPackageId)
        {
            if (lightingOwner == null || lightingOwner != ownerPackageId)
                return;

            Log.Message($"[RAT] Lighting released by {ownerPackageId}; RAT lighting patches active again.");
            lightingOwner = null;
        }
    }
}
