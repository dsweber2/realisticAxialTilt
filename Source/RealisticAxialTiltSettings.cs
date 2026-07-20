using Verse;

namespace RealisticAxialTilt
{
    public class RealisticAxialTiltSettings : ModSettings
    {
        public bool realisticPlantRest = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref realisticPlantRest, "realisticPlantRest", true);
            base.ExposeData();
        }
    }
}
