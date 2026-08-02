    using System.Collections.Generic;
    using LibTessDotNet;
    using NetTopologySuite.Geometries;
    using UnityEngine;

    public class BuildingRenderer : MonoBehaviour
    {
        private Transform buildingsParent;
        public void DrawBuilding(
            BuildingData building,
            Dictionary<long, NodeData> nodesDictionary,
            CoordinateConverter converter)
        {
        Debug.Log($"RENDER HEIGHT | {building.Id} | {building.Height}");
        List<Vector3> vertices = new List<Vector3>();

            foreach (long nodeId in building.OuterNodeIds)
            {
                if (!nodesDictionary.TryGetValue(nodeId, out NodeData node))
                {
                    Debug.LogWarning(
                        $"BUILDING SKIPPED | ID={building.Id} | Missing node={nodeId}"
                    );
                    return;
                }

                Vector3 point = converter.ToUnityPosition(
                    node.Latitude,
                    node.Longitude
                );

                vertices.Add(point);
            }

            Triangulator.RemoveClosingVertex(vertices);

            if (Triangulator.IsClockwise(vertices))
            {
                vertices.Reverse();
            }

            if (vertices.Count < 3)
            {
                Debug.LogWarning(
                    $"BUILDING SKIPPED | ID={building.Id} | Vertices={vertices.Count}"
                );
                return;
            }

            Tess tess = new Tess();

            ContourVertex[] contour =
                new ContourVertex[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                // IMPORTANT:
                // Unity map = XZ plane
                // LibTess polygon = XY plane
                contour[i].Position = new Vec3(
                    vertices[i].x,
                    vertices[i].z,
                    0
                );
            }
        

            tess.AddContour(
                contour,
                ContourOrientation.Original
            );

            tess.Tessellate(
                WindingRule.EvenOdd,
                ElementType.Polygons,
                3
            );

            if (tess.Vertices.Length < 3 ||
                tess.Elements == null ||
                tess.Elements.Length < 3)
            {
                Debug.LogWarning(
                    $"TESSELLATION FAILED | Building={building.Id}" +
                    $" | Input={vertices.Count}" +
                    $" | Output={tess.Vertices.Length}"
                );

                return;
            }

            if (buildingsParent == null)
            {
                GameObject obj = new GameObject("Buildings");
                buildingsParent = obj.transform;
            }

            GameObject buildingObject =
                new GameObject("Building_" + building.Id);

            buildingObject.transform.SetParent(buildingsParent, false);

            MeshFilter meshFilter =
                buildingObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer =
                buildingObject.AddComponent<MeshRenderer>();

            Material material =
        new Material(Shader.Find("Sprites/Default"));

            material.color = new Color(0.88f, 0.84f, 0.72f);

            meshRenderer.material = material;

            Vector3[] meshVertices =
                new Vector3[tess.Vertices.Length];

            for (int i = 0; i < tess.Vertices.Length; i++)
            {
                // Convert LibTess XY back to Unity XZ
                meshVertices[i] = new Vector3(
                    tess.Vertices[i].Position.X,
                    building.Height,
                    tess.Vertices[i].Position.Y
                );
            }

            int[] triangles = (int[])tess.Elements.Clone();

        // ----------------------------
        // Build Roof Mesh
        // ----------------------------
        Mesh roofMesh =
            RoofGenerator.BuildRoof(
                meshVertices,
                triangles
            );

        // ----------------------------
        // Build Wall Mesh
        // ----------------------------
        Mesh wallMesh =
                WallGenerator.BuildWalls(
                    vertices,
                     building.Height
                );

        // ----------------------------
        // Combine Roof + Walls
        // ----------------------------
        Mesh finalMesh =
            MeshCombiner.Combine(
                roofMesh,
                wallMesh
            );

        meshFilter.sharedMesh = finalMesh;





        Debug.Log(
                $"BUILDING RENDERED | ID={building.Id}" +
                $" | Input={vertices.Count}" +
                $" | MeshVertices={meshVertices.Length}" +
                $" | Triangles={triangles.Length / 3}"
            );
        }


    public void DrawPolygon(
            Polygon polygon,
            CoordinateConverter converter,
            BuildingData building)
    {
        Debug.Log($"NTS RENDER HEIGHT | {building.Id} | {building.Height}");
        long buildingId = building.Id;
        if (polygon == null || polygon.IsEmpty)
            {
                Debug.LogWarning(
                    $"Building {buildingId}: polygon null/empty."
                );

                return;
            }

            Tess tess = new Tess();

            // ============================
            // OUTER RING
            // ============================

            var coordinates =
                polygon.ExteriorRing.Coordinates;

            List<Vector3> vertices =
                new List<Vector3>();

            foreach (var coordinate in coordinates)
            {
                Vector3 point =
                    converter.ToUnityPosition(
                        coordinate.Y,
                        coordinate.X
                    );

                vertices.Add(point);
            }

            Triangulator.RemoveClosingVertex(vertices);

            if (Triangulator.IsClockwise(vertices))
            {
                vertices.Reverse();
            }

            if (vertices.Count < 3)
            {
                Debug.LogWarning(
                    $"NTS BUILDING SKIPPED | ID={buildingId}"
                );

                return;
            }

            ContourVertex[] contour =
                new ContourVertex[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                contour[i].Position = new Vec3(
                    vertices[i].x,
                    vertices[i].z,
                    0
                );
            }

            tess.AddContour(
                contour,
                ContourOrientation.Original
            );


            // ============================
            // HOLES
            // ============================

            for (int h = 0;
                 h < polygon.NumInteriorRings;
                 h++)
            {
                var holeCoordinates =
                    polygon.GetInteriorRingN(h).Coordinates;

                List<Vector3> holeVertices =
                    new List<Vector3>();

                foreach (var coordinate in holeCoordinates)
                {
                    Vector3 point =
                        converter.ToUnityPosition(
                            coordinate.Y,
                            coordinate.X
                        );

                    holeVertices.Add(point);
                }

                Triangulator.RemoveClosingVertex(
                    holeVertices
                );

                if (!Triangulator.IsClockwise(holeVertices))
                {
                    holeVertices.Reverse();
                }

                if (holeVertices.Count < 3)
                    continue;

                ContourVertex[] holeContour =
                    new ContourVertex[holeVertices.Count];

                for (int i = 0;
                     i < holeVertices.Count;
                     i++)
                {
                    holeContour[i].Position =
                        new Vec3(
                            holeVertices[i].x,
                            holeVertices[i].z,
                            0
                        );
                }

                tess.AddContour(
                    holeContour,
                    ContourOrientation.Original
                );
            }


            tess.Tessellate(
                WindingRule.EvenOdd,
                ElementType.Polygons,
                3
            );

            if (tess.Vertices.Length < 3 ||
                tess.Elements == null ||
                tess.Elements.Length < 3)
            {
                Debug.LogWarning(
                    $"NTS TESSELLATION FAILED | Building={buildingId}"
                );

                return;
            }


            // ============================
            // CREATE UNITY MESH
            // ============================

            if (buildingsParent == null)
            {
                GameObject obj = new GameObject("Buildings");
                buildingsParent = obj.transform;
            }

            GameObject buildingObject =
                new GameObject(
                    "NTS_Building_" + buildingId
                );

            buildingObject.transform.SetParent(
                buildingsParent,
                false
            );

            MeshFilter meshFilter =
                buildingObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer =
                buildingObject.AddComponent<MeshRenderer>();

            Material material =
        new Material(Shader.Find("Sprites/Default"));

            material.color = new Color(0.88f, 0.84f, 0.72f);

            meshRenderer.material = material;

            Vector3[] meshVertices =
                new Vector3[tess.Vertices.Length];

            for (int i = 0;
                 i < tess.Vertices.Length;
                 i++)
            {
                meshVertices[i] =
                    new Vector3(
                        tess.Vertices[i].Position.X,
                        building.Height,
                        tess.Vertices[i].Position.Y
                    );
            }

            int[] triangles =
                (int[])tess.Elements.Clone();


        Mesh roofMesh =
            RoofGenerator.BuildRoof(
                meshVertices,
                triangles
            );

        Mesh wallMesh =
            WallGenerator.BuildWalls(
                vertices,
                building.Height
            );

        // ----------------------------
        // Combine Roof + Walls
        // ----------------------------

        Mesh finalMesh =
            MeshCombiner.Combine(
                roofMesh,
                wallMesh
            );

        meshFilter.sharedMesh = finalMesh;


        Debug.Log(
                $"NTS RENDER PASSED | Building={buildingId}" +
                $" | MeshVertices={meshVertices.Length}" +
                $" | Triangles={triangles.Length / 3}" +
                $" | Holes={polygon.NumInteriorRings}"
            );
        }
    }