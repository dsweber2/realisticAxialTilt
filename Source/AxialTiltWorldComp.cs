using RealisticAxialTilt.Compat;
using RimWorld.Planet;
using Verse;

namespace RealisticAxialTilt
{
    public class AxialTiltWorldComp : WorldComponent
    {
        public static float PendingAxialTiltDeg = 23.45f;
        public static float PendingK = 0.5f;

        private float axialTiltDeg = 23.45f;
        private float k = 0.5f;

        public float AxialTiltDeg => axialTiltDeg;

        public AxialTiltWorldComp(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref axialTiltDeg, "axialTiltDeg", 23.45f);
            Scribe_Values.Look(ref k, "dampingK", 0.5f);
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            if (RealisticPlanets2Compat.IsActive)
            {
                SolarGeometry.ApplyAxialTilt(RealisticPlanets2Compat.GetTiltDegrees(), 1.0f);
                return;
            }
            if (!fromLoad)
            {
                axialTiltDeg = PendingAxialTiltDeg;
                k = PendingK;
            }
            SolarGeometry.ApplyAxialTilt(axialTiltDeg, k);
        }
    }
}
