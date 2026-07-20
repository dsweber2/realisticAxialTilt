using RimWorld.Planet;
using Verse;

namespace RealisticAxialTilt
{
    public class AxialTiltWorldComp : WorldComponent
    {
        public static float PendingAxialTiltDeg = 23.45f;
        public static float PendingK = 1.0f;

        private float axialTiltDeg = 23.45f;
        private float k = 1.0f;

        public float AxialTiltDeg => axialTiltDeg;

        public AxialTiltWorldComp(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref axialTiltDeg, "axialTiltDeg", 23.45f);
            Scribe_Values.Look(ref k, "dampingK", 1.0f);
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            if (!fromLoad)
            {
                axialTiltDeg = PendingAxialTiltDeg;
                k = PendingK;
            }
            SolarGeometry.ApplyAxialTilt(axialTiltDeg, k);
        }
    }
}
