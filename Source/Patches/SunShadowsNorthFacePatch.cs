using HarmonyLib;
using UnityEngine;
using Verse;

namespace RealisticAxialTilt.Patches
{
    [HarmonyPatch]
    internal static class SunShadowsNorthFacePatch
    {
        private static readonly Color32 LowVertexColor = new Color32(0, 0, 0, 0);

        private static readonly System.Reflection.FieldInfo SectionField =
            AccessTools.Field(typeof(SectionLayer), "section");

        static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method("Verse.SectionLayer_SunShadows:Regenerate");

        [HarmonyPrefix]
        private static bool Prefix(SectionLayer __instance)
        {
            if (!MatBases.SunShadow.shader.isSupported)
                return false;

            Section section = (Section)SectionField.GetValue(__instance);
            Map map = section.map;
            Building[] innerArray = map.edificeGrid.InnerArray;
            float y = AltitudeLayer.Shadows.AltitudeFor();
            CellRect cellRect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
            cellRect.ClipInsideMap(map);
            LayerSubMesh subMesh = __instance.GetSubMesh(MatBases.SunShadow);
            subMesh.Clear(MeshParts.All);
            subMesh.verts.Capacity = cellRect.Area * 2;
            subMesh.tris.Capacity = cellRect.Area * 4;
            subMesh.colors.Capacity = cellRect.Area * 2;
            CellIndices cellIndices = map.cellIndices;

            for (int i = cellRect.minX; i <= cellRect.maxX; i++)
            {
                for (int j = cellRect.minZ; j <= cellRect.maxZ; j++)
                {
                    Building building = innerArray[cellIndices.CellToIndex(i, j)];
                    if (building == null || !(building.def.staticSunShadowHeight > 0f))
                        continue;

                    float h = building.def.staticSunShadowHeight;
                    Color32 item = new Color32(0, 0, 0, (byte)(255f * h));
                    int count = subMesh.verts.Count;

                    subMesh.verts.Add(new Vector3(i, y, j));
                    subMesh.verts.Add(new Vector3(i, y, j + 1));
                    subMesh.verts.Add(new Vector3(i + 1, y, j + 1));
                    subMesh.verts.Add(new Vector3(i + 1, y, j));
                    subMesh.colors.Add(LowVertexColor);
                    subMesh.colors.Add(LowVertexColor);
                    subMesh.colors.Add(LowVertexColor);
                    subMesh.colors.Add(LowVertexColor);
                    int count2 = subMesh.verts.Count;
                    subMesh.tris.Add(count2 - 4);
                    subMesh.tris.Add(count2 - 3);
                    subMesh.tris.Add(count2 - 2);
                    subMesh.tris.Add(count2 - 4);
                    subMesh.tris.Add(count2 - 2);
                    subMesh.tris.Add(count2 - 1);

                    if (i > 0)
                    {
                        building = innerArray[cellIndices.CellToIndex(i - 1, j)];
                        if (building == null || building.def.staticSunShadowHeight < h)
                        {
                            int count3 = subMesh.verts.Count;
                            subMesh.verts.Add(new Vector3(i, y, j));
                            subMesh.verts.Add(new Vector3(i, y, j + 1));
                            subMesh.colors.Add(item);
                            subMesh.colors.Add(item);
                            subMesh.tris.Add(count + 1);
                            subMesh.tris.Add(count);
                            subMesh.tris.Add(count3);
                            subMesh.tris.Add(count3);
                            subMesh.tris.Add(count3 + 1);
                            subMesh.tris.Add(count + 1);
                        }
                    }

                    if (i < map.Size.x - 1)
                    {
                        building = innerArray[cellIndices.CellToIndex(i + 1, j)];
                        if (building == null || building.def.staticSunShadowHeight < h)
                        {
                            int count4 = subMesh.verts.Count;
                            subMesh.verts.Add(new Vector3(i + 1, y, j + 1));
                            subMesh.verts.Add(new Vector3(i + 1, y, j));
                            subMesh.colors.Add(item);
                            subMesh.colors.Add(item);
                            subMesh.tris.Add(count + 2);
                            subMesh.tris.Add(count4);
                            subMesh.tris.Add(count4 + 1);
                            subMesh.tris.Add(count4 + 1);
                            subMesh.tris.Add(count + 3);
                            subMesh.tris.Add(count + 2);
                        }
                    }

                    if (j > 0)
                    {
                        building = innerArray[cellIndices.CellToIndex(i, j - 1)];
                        if (building == null || building.def.staticSunShadowHeight < h)
                        {
                            int count5 = subMesh.verts.Count;
                            subMesh.verts.Add(new Vector3(i, y, j));
                            subMesh.verts.Add(new Vector3(i + 1, y, j));
                            subMesh.colors.Add(item);
                            subMesh.colors.Add(item);
                            subMesh.tris.Add(count);
                            subMesh.tris.Add(count + 3);
                            subMesh.tris.Add(count5);
                            subMesh.tris.Add(count + 3);
                            subMesh.tris.Add(count5 + 1);
                            subMesh.tris.Add(count5);
                        }
                    }

                    // North face — absent from vanilla, needed for northward shadows
                    if (j < map.Size.z - 1)
                    {
                        building = innerArray[cellIndices.CellToIndex(i, j + 1)];
                        if (building == null || building.def.staticSunShadowHeight < h)
                        {
                            int count6 = subMesh.verts.Count;
                            subMesh.verts.Add(new Vector3(i, y, j + 1));
                            subMesh.verts.Add(new Vector3(i + 1, y, j + 1));
                            subMesh.colors.Add(item);
                            subMesh.colors.Add(item);
                            subMesh.tris.Add(count + 1);
                            subMesh.tris.Add(count6);
                            subMesh.tris.Add(count + 2);
                            subMesh.tris.Add(count + 2);
                            subMesh.tris.Add(count6);
                            subMesh.tris.Add(count6 + 1);
                        }
                    }
                }
            }

            if (subMesh.verts.Count > 0)
            {
                subMesh.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
                subMesh.mesh.bounds = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1000f));
            }

            return false;
        }
    }
}
