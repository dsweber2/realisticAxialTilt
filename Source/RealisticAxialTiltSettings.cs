using Verse;

namespace RealisticAxialTilt
{
    public class RealisticAxialTiltSettings : ModSettings
    {
        public bool realisticPlantRest = true;
        public float moonOrbitalDays = 9f;
        public float moonInclinationDeg = 5.1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref realisticPlantRest, "realisticPlantRest", true);
            Scribe_Values.Look(ref moonOrbitalDays, "moonOrbitalDays", 9f);
            Scribe_Values.Look(ref moonInclinationDeg, "moonInclinationDeg", 5.1f);
            base.ExposeData();
        }
    }
}
