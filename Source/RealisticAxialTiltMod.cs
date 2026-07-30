using HarmonyLib;
using RealisticAxialTilt.Api;
using RealisticAxialTilt.Compat;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt
{
    public class RealisticAxialTiltMod : Mod
    {
        public static RealisticAxialTiltSettings Settings;

        public RealisticAxialTiltMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RealisticAxialTiltSettings>();
            var harmony = new Harmony("dsweber.RealisticAxialTilt");
            harmony.PatchAll();
            NicePlantsMenuCompat.TryPatch(harmony);
            FactionControlCompat.TryPatch(harmony);
            RimWarCompat.TryPatch(harmony);
            ConfigurableMapsCompat.TryPatch(harmony);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("RAT_RealisticPlantRest".Translate(), ref Settings.realisticPlantRest, "RAT_RealisticPlantRestDesc".Translate());

            Settings.moonOrbitalDays = listing.SliderLabeled(
                "RAT_MoonOrbitalDays".Translate(Settings.moonOrbitalDays.ToString("F2")),
                Settings.moonOrbitalDays, 1f, 30f,
                tooltip: "RAT_MoonOrbitalDaysDesc".Translate());
            Settings.moonInclinationDeg = listing.SliderLabeled(
                "RAT_MoonInclination".Translate(Settings.moonInclinationDeg.ToString("F1")),
                Settings.moonInclinationDeg, 0f, 30f,
                tooltip: "RAT_MoonInclinationDesc".Translate());

            listing.CheckboxLabeled("RAT_SuppressLighting".Translate(), ref Settings.suppressLighting, "RAT_SuppressLightingDesc".Translate());
            if (RealisticAxialTiltApi.LightingOwner != null)
                listing.Label("RAT_LightingClaimedBy".Translate(RealisticAxialTiltApi.LightingOwner));

            listing.End();
        }

        public override string SettingsCategory() => "RAT_SettingsCategory".Translate();
    }
}
