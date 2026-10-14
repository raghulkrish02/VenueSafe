    using System.Collections.Generic;
    using LibTessDotNet;
    using NetTopologySuite.Geometries;
    using UnityEngine;
    using Unity.AI.Navigation;

public class BuildingRenderer : MonoBehaviour
{
    private Transform buildingsParent;
    private Material sharedBuildingMaterial;
    private Material sharedEdgeMaterial;

    private void Awake()
    {
        // Loads from Resources - GUARANTEED zero pink in .exe builds!
        sharedBuildingMaterial = Resources.Load<Material>("BuildingMaterial");
        if (sharedBuildingMaterial == null)
        {
            sharedBuildingMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sharedBuildingMaterial.color = new Color(0.88f, 0.84f, 0.72f);
        }

        sharedEdgeMaterial = new Material(Shader.Find("Sprites/Default"));
        sharedEdgeMaterial.color = Color.black;
    }
    public void DrawBuilding(BuildingData building)
    {
        if (building == null || building.OuterFootprint == null || building.OuterFootprint.Count < 3)
        {
            Debug.LogWarning($"BUILDING SKIPPED | ID={building?.Id} | Invalid footprint");
            return;
        }

        List<Vector3> outerVertices = new List<Vector3>(building.OuterFootprint);

        Triangulator.RemoveClosingVertex(outerVertices);
        if (Triangulator.IsClockwise(outerVertices))
        {
            outerVertices.Reverse();
        }

        if (outerVertices.Count < 3) return;

        Tess tess = new Tess();

        // 1. Add Outer Contour
        ContourVertex[] outerContour = new ContourVertex[outerVertices.Count];
        for (int i = 0; i < outerVertices.Count; i++)
        {
            outerContour[i].Position = new Vec3(outerVertices[i].x, outerVertices[i].z, 0);
        }
        tess.AddContour(outerContour, ContourOrientation.Original);

        // 2. Add Inner Contours (Holes) if present
        if (building.InnerFootprints != null)
        {
            foreach (var hole in building.InnerFootprints)
            {
                List<Vector3> holeVertices = new List<Vector3>(hole);
                Triangulator.RemoveClosingVertex(holeVertices);
                if (!Triangulator.IsClockwise(holeVertices))
                {
                    holeVertices.Reverse();
                }

                if (holeVertices.Count < 3) continue;

                ContourVertex[] holeContour = new ContourVertex[holeVertices.Count];
                for (int i = 0; i < holeVertices.Count; i++)
                {
                    holeContour[i].Position = new Vec3(holeVertices[i].x, holeVertices[i].z, 0);
                }
                tess.AddContour(holeContour, ContourOrientation.Original);
            }
        }

        // 3. Tessellate
        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        if (tess.Vertices.Length < 3 || tess.Elements == null || tess.Elements.Length < 3)
        {
            Debug.LogWarning($"TESSELLATION FAILED | Building={building.Id}");
            return;
        }

        // 4. Instantiate Unity Game Object
        if (buildingsParent == null)
        {
            GameObject obj = new GameObject("Buildings");
            buildingsParent = obj.transform;
        }

        GameObject buildingObject = new GameObject("Building_" + building.Id);
        buildingObject.transform.SetParent(buildingsParent, false);

        MeshFilter meshFilter = buildingObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = buildingObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = sharedBuildingMaterial;

        // Mark this building as a SOLID NON-WALKABLE OBSTACLE for NavMesh
        NavMeshModifier modifier = buildingObject.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = 1; // 1 = "Not Walkable"
        modifier.ignoreFromBuild = false;

        Vector3[] meshVertices = new Vector3[tess.Vertices.Length];
        for (int i = 0; i < tess.Vertices.Length; i++)
        {
            meshVertices[i] = new Vector3(
                tess.Vertices[i].Position.X,
                building.Height,
                tess.Vertices[i].Position.Y
            );
        }

        int[] triangles = (int[])tess.Elements.Clone();

        Mesh roofMesh = RoofGenerator.BuildRoof(meshVertices, triangles);

        // Also build a bottom floor cap at Y = 0 to make the building watertight with no gaps underneath
        Vector3[] bottomVertices = new Vector3[tess.Vertices.Length];
        for (int i = 0; i < tess.Vertices.Length; i++)
        {
            bottomVertices[i] = new Vector3(
                tess.Vertices[i].Position.X,
                0f,
                tess.Vertices[i].Position.Y
            );
        }
        // Invert winding order for bottom floor so normals face downward/outward
        int[] bottomTriangles = (int[])tess.Elements.Clone();
        for (int i = 0; i < bottomTriangles.Length; i += 3)
        {
            int temp = bottomTriangles[i];
            bottomTriangles[i] = bottomTriangles[i + 2];
            bottomTriangles[i + 2] = temp;
        }

        Mesh floorMesh = RoofGenerator.BuildRoof(bottomVertices, bottomTriangles);
        Mesh wallMesh = WallGenerator.BuildWalls(outerVertices, building.Height);

        // Combine roof, floor, and walls into one solid watertight mesh
        Mesh finalMesh = MeshCombiner.CombineThree(roofMesh, floorMesh, wallMesh);

        // ------------------------------------
        // Generate Black Architectural Outlines
        // ------------------------------------
        List<Vector3> lineVertices = new List<Vector3>();
        List<int> lineIndices = new List<int>();

        // 1. Outer Wall Edges (Top Roof Loop + Vertical Wall Pillars)
        AddFootprintOutlines(outerVertices, building.Height, lineVertices, lineIndices);

        // 2. Inner Courtyard Edges (if present)
        if (building.InnerFootprints != null)
        {
            foreach (var hole in building.InnerFootprints)
            {
                List<Vector3> holeVerts = new List<Vector3>(hole);
                Triangulator.RemoveClosingVertex(holeVerts);
                if (holeVerts.Count >= 3)
                {
                    AddFootprintOutlines(holeVerts, building.Height, lineVertices, lineIndices);
                }
            }
        }

        // 3. Attach Edge Lines as Submesh #1
        Vector3[] origVerts = finalMesh.vertices;
        int origCount = origVerts.Length;

        Vector3[] newVerts = new Vector3[origCount + lineVertices.Count];
        System.Array.Copy(origVerts, 0, newVerts, 0, origCount);
        for (int i = 0; i < lineVertices.Count; i++)
        {
            newVerts[origCount + i] = lineVertices[i];
        }

        int[] offsetIndices = new int[lineIndices.Count];
        for (int i = 0; i < lineIndices.Count; i++)
        {
            offsetIndices[i] = lineIndices[i] + origCount;
        }

        finalMesh.vertices = newVerts;
        finalMesh.subMeshCount = 2; // Submesh 0 = Triangles (Beige), Submesh 1 = Lines (Black)
        finalMesh.SetIndices(offsetIndices, MeshTopology.Lines, 1);

        meshFilter.sharedMesh = finalMesh;
        meshRenderer.sharedMaterials = new Material[] { sharedBuildingMaterial, sharedEdgeMaterial };
    }

    private void AddFootprintOutlines(List<Vector3> footprint, float height, List<Vector3> lineVerts, List<int> lineIndices)
    {
        int count = footprint.Count;
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;

            Vector3 botCurr = new Vector3(footprint[i].x, 0, footprint[i].z);
            Vector3 topCurr = new Vector3(footprint[i].x, height, footprint[i].z);
            Vector3 topNext = new Vector3(footprint[next].x, height, footprint[next].z);

            int idxBotCurr = lineVerts.Count;
            lineVerts.Add(botCurr);

            int idxTopCurr = lineVerts.Count;
            lineVerts.Add(topCurr);

            int idxTopNext = lineVerts.Count;
            lineVerts.Add(topNext);

            // Top Roof Edge Line
            lineIndices.Add(idxTopCurr);
            lineIndices.Add(idxTopNext);

            // Vertical Wall Corner Line
            lineIndices.Add(idxBotCurr);
            lineIndices.Add(idxTopCurr);
        }
    }
}
    //public class BuildingRenderer : MonoBehaviour
    //{
    //    private Transform buildingsParent;
    //    public void DrawBuilding(
    //        BuildingData building,
    //        Dictionary<long, NodeData> nodesDictionary,
    //        CoordinateConverter converter)
    //    {
    //    Debug.Log($"RENDER HEIGHT | {building.Id} | {building.Height}");
    //    List<Vector3> vertices = new List<Vector3>();

//        foreach (long nodeId in building.OuterNodeIds)
//        {
//            if (!nodesDictionary.TryGetValue(nodeId, out NodeData node))
//            {
//                Debug.LogWarning(
//                    $"BUILDING SKIPPED | ID={building.Id} | Missing node={nodeId}"
//                );
//                return;
//            }

//            Vector3 point = converter.ToUnityPosition(
//                node.Latitude,
//                node.Longitude
//            );

//            vertices.Add(point);
//        }

//        Triangulator.RemoveClosingVertex(vertices);

//        if (Triangulator.IsClockwise(vertices))
//        {
//            vertices.Reverse();
//        }

//        if (vertices.Count < 3)
//        {
//            Debug.LogWarning(
//                $"BUILDING SKIPPED | ID={building.Id} | Vertices={vertices.Count}"
//            );
//            return;
//        }

//        Tess tess = new Tess();

//        ContourVertex[] contour =
//            new ContourVertex[vertices.Count];

//        for (int i = 0; i < vertices.Count; i++)
//        {
//            // IMPORTANT:
//            // Unity map = XZ plane
//            // LibTess polygon = XY plane
//            contour[i].Position = new Vec3(
//                vertices[i].x,
//                vertices[i].z,
//                0
//            );
//        }


//        tess.AddContour(
//            contour,
//            ContourOrientation.Original
//        );

//        tess.Tessellate(
//            WindingRule.EvenOdd,
//            ElementType.Polygons,
//            3
//        );

//        if (tess.Vertices.Length < 3 ||
//            tess.Elements == null ||
//            tess.Elements.Length < 3)
//        {
//            Debug.LogWarning(
//                $"TESSELLATION FAILED | Building={building.Id}" +
//                $" | Input={vertices.Count}" +
//                $" | Output={tess.Vertices.Length}"
//            );

//            return;
//        }

//        if (buildingsParent == null)
//        {
//            GameObject obj = new GameObject("Buildings");
//            buildingsParent = obj.transform;
//        }

//        GameObject buildingObject =
//            new GameObject("Building_" + building.Id);

//        buildingObject.transform.SetParent(buildingsParent, false);

//        MeshFilter meshFilter =
//            buildingObject.AddComponent<MeshFilter>();

//        MeshRenderer meshRenderer =
//            buildingObject.AddComponent<MeshRenderer>();

//        Material material =
//    new Material(Shader.Find("Sprites/Default"));

//        material.color = new Color(0.88f, 0.84f, 0.72f);

//        meshRenderer.material = material;

//        Vector3[] meshVertices =
//            new Vector3[tess.Vertices.Length];

//        for (int i = 0; i < tess.Vertices.Length; i++)
//        {
//            // Convert LibTess XY back to Unity XZ
//            meshVertices[i] = new Vector3(
//                tess.Vertices[i].Position.X,
//                building.Height,
//                tess.Vertices[i].Position.Y
//            );
//        }

//        int[] triangles = (int[])tess.Elements.Clone();

//    // ----------------------------
//    // Build Roof Mesh
//    // ----------------------------
//    Mesh roofMesh =
//        RoofGenerator.BuildRoof(
//            meshVertices,
//            triangles
//        );

//    // ----------------------------
//    // Build Wall Mesh
//    // ----------------------------
//    Mesh wallMesh =
//            WallGenerator.BuildWalls(
//                vertices,
//                 building.Height
//            );

//    // ----------------------------
//    // Combine Roof + Walls
//    // ----------------------------
//    Mesh finalMesh =
//        MeshCombiner.Combine(
//            roofMesh,
//            wallMesh
//        );

//    meshFilter.sharedMesh = finalMesh;





//    Debug.Log(
//            $"BUILDING RENDERED | ID={building.Id}" +
//            $" | Input={vertices.Count}" +
//            $" | MeshVertices={meshVertices.Length}" +
//            $" | Triangles={triangles.Length / 3}"
//        );
//    }


//public void DrawPolygon(
//        Polygon polygon,
//        CoordinateConverter converter,
//        BuildingData building)
//{
//    Debug.Log($"NTS RENDER HEIGHT | {building.Id} | {building.Height}");
//    long buildingId = building.Id;
//    if (polygon == null || polygon.IsEmpty)
//        {
//            Debug.LogWarning(
//                $"Building {buildingId}: polygon null/empty."
//            );

//            return;
//        }

//        Tess tess = new Tess();

//        // ============================
//        // OUTER RING
//        // ============================

//        var coordinates =
//            polygon.ExteriorRing.Coordinates;

//        List<Vector3> vertices =
//            new List<Vector3>();

//        foreach (var coordinate in coordinates)
//        {
//            Vector3 point =
//                converter.ToUnityPosition(
//                    coordinate.Y,
//                    coordinate.X
//                );

//            vertices.Add(point);
//        }

//        Triangulator.RemoveClosingVertex(vertices);

//        if (Triangulator.IsClockwise(vertices))
//        {
//            vertices.Reverse();
//        }

//        if (vertices.Count < 3)
//        {
//            Debug.LogWarning(
//                $"NTS BUILDING SKIPPED | ID={buildingId}"
//            );

//            return;
//        }

//        ContourVertex[] contour =
//            new ContourVertex[vertices.Count];

//        for (int i = 0; i < vertices.Count; i++)
//        {
//            contour[i].Position = new Vec3(
//                vertices[i].x,
//                vertices[i].z,
//                0
//            );
//        }

//        tess.AddContour(
//            contour,
//            ContourOrientation.Original
//        );


//        // ============================
//        // HOLES
//        // ============================

//        for (int h = 0;
//             h < polygon.NumInteriorRings;
//             h++)
//        {
//            var holeCoordinates =
//                polygon.GetInteriorRingN(h).Coordinates;

//            List<Vector3> holeVertices =
//                new List<Vector3>();

//            foreach (var coordinate in holeCoordinates)
//            {
//                Vector3 point =
//                    converter.ToUnityPosition(
//                        coordinate.Y,
//                        coordinate.X
//                    );

//                holeVertices.Add(point);
//            }

//            Triangulator.RemoveClosingVertex(
//                holeVertices
//            );

//            if (!Triangulator.IsClockwise(holeVertices))
//            {
//                holeVertices.Reverse();
//            }

//            if (holeVertices.Count < 3)
//                continue;

//            ContourVertex[] holeContour =
//                new ContourVertex[holeVertices.Count];

//            for (int i = 0;
//                 i < holeVertices.Count;
//                 i++)
//            {
//                holeContour[i].Position =
//                    new Vec3(
//                        holeVertices[i].x,
//                        holeVertices[i].z,
//                        0
//                    );
//            }

//            tess.AddContour(
//                holeContour,
//                ContourOrientation.Original
//            );
//        }


//        tess.Tessellate(
//            WindingRule.EvenOdd,
//            ElementType.Polygons,
//            3
//        );

//        if (tess.Vertices.Length < 3 ||
//            tess.Elements == null ||
//            tess.Elements.Length < 3)
//        {
//            Debug.LogWarning(
//                $"NTS TESSELLATION FAILED | Building={buildingId}"
//            );

//            return;
//        }


//        // ============================
//        // CREATE UNITY MESH
//        // ============================

//        if (buildingsParent == null)
//        {
//            GameObject obj = new GameObject("Buildings");
//            buildingsParent = obj.transform;
//        }

//        GameObject buildingObject =
//            new GameObject(
//                "NTS_Building_" + buildingId
//            );

//        buildingObject.transform.SetParent(
//            buildingsParent,
//            false
//        );

//        MeshFilter meshFilter =
//            buildingObject.AddComponent<MeshFilter>();

//        MeshRenderer meshRenderer =
//            buildingObject.AddComponent<MeshRenderer>();

//        Material material =
//    new Material(Shader.Find("Sprites/Default"));

//        material.color = new Color(0.88f, 0.84f, 0.72f);

//        meshRenderer.material = material;

//        Vector3[] meshVertices =
//            new Vector3[tess.Vertices.Length];

//        for (int i = 0;
//             i < tess.Vertices.Length;
//             i++)
//        {
//            meshVertices[i] =
//                new Vector3(
//                    tess.Vertices[i].Position.X,
//                    building.Height,
//                    tess.Vertices[i].Position.Y
//                );
//        }

//        int[] triangles =
//            (int[])tess.Elements.Clone();


//    Mesh roofMesh =
//        RoofGenerator.BuildRoof(
//            meshVertices,
//            triangles
//        );

//    Mesh wallMesh =
//        WallGenerator.BuildWalls(
//            vertices,
//            building.Height
//        );

//    // ----------------------------
//    // Combine Roof + Walls
//    // ----------------------------

//    Mesh finalMesh =
//        MeshCombiner.Combine(
//            roofMesh,
//            wallMesh
//        );

//    meshFilter.sharedMesh = finalMesh;


//    Debug.Log(
//            $"NTS RENDER PASSED | Building={buildingId}" +
//            $" | MeshVertices={meshVertices.Length}" +
//            $" | Triangles={triangles.Length / 3}" +
//            $" | Holes={polygon.NumInteriorRings}"
//        );
//    }
//}