using Verse;

namespace RealisticAxialTilt
{
    public class RealisticAxialTiltSettings : ModSettings
    {
        public bool realisticPlantRest = true;

        // Manual override for the Api lighting handover, for conflicting mods that never claim it.
        public bool suppressLighting = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref realisticPlantRest, "realisticPlantRest", true);
            Scribe_Values.Look(ref suppressLighting, "suppressLighting", false);
            base.ExposeData();
        }
    }
}
