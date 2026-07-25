using HarmonyLib;
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
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("RAT_RealisticPlantRest".Translate(), ref Settings.realisticPlantRest, "RAT_RealisticPlantRestDesc".Translate());
            listing.End();
        }

        public override string SettingsCategory() => "RAT_SettingsCategory".Translate();
    }
}
