using HarmonyLib;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch(typeof(Printer_Shadow), nameof(Printer_Shadow.PrintShadow),
        new[] { typeof(SectionLayer), typeof(Vector3), typeof(Vector3), typeof(Rot4) })]
    internal static class PrintShadowNorthFacePatch
    {
        [HarmonyPrefix]
        private static void Prefix(SectionLayer layer, ref int __state)
        {
            __state = layer.GetSubMesh(MatBases.SunShadowFade).verts.Count;
        }

        [HarmonyPostfix]
        private static void Postfix(SectionLayer layer, int __state)
        {
            LayerSubMesh subMesh = layer.GetSubMesh(MatBases.SunShadowFade);
            int pre = __state;

            // Printer_Shadow adds 10 verts per call; bail if it returned early (drawShadows=false)
            if (subMesh.verts.Count < pre + 10)
                return;

            // Base quad vertex layout (from Printer_Shadow):
            //   pre+0 = SW_base, pre+1 = NW_base, pre+2 = NE_base, pre+3 = SE_base
            // Face-top vertices:
            //   pre+4 = SW_top (west face), pre+5 = NW_top (west face)
            //   pre+6 = NE_top (east face), pre+7 = SE_top (east face)
            //   pre+8 = SW_top (south face), pre+9 = SE_top (south face)
            //
            // The North face is absent from vanilla — add it here.
            // Its top vertices share positions with NW_base/NE_base; the shader
            // displaces them by MapSunLightDirection * alpha to project the shadow north.

            Color32 item = subMesh.colors[pre + 4]; // same height-alpha as other tops

            int northCount = subMesh.verts.Count;
            subMesh.verts.Add(subMesh.verts[pre + 1]); // NW_top_N (same pos as NW_base)
            subMesh.verts.Add(subMesh.verts[pre + 2]); // NE_top_N (same pos as NE_base)
            subMesh.colors.Add(item);
            subMesh.colors.Add(item);

            subMesh.tris.Add(pre + 1);         // NW_base
            subMesh.tris.Add(northCount);       // NW_top_N
            subMesh.tris.Add(pre + 2);         // NE_base
            subMesh.tris.Add(pre + 2);         // NE_base
            subMesh.tris.Add(northCount);       // NW_top_N
            subMesh.tris.Add(northCount + 1);   // NE_top_N
        }
    }
}
