using HarmonyLib;
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
            new Harmony("dsweber.RealisticAxialTilt").PatchAll();
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
