using System.Collections.Generic;
using UnityEngine;

public static class BuildingExtruder
{
    public static Mesh Extrude(
        List<Vector3> footprint,
        int[] roofTriangles,
        float height)
    {
        Mesh mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int n = footprint.Count;

        // Bottom vertices
        vertices.AddRange(footprint);

        // Top vertices
        for (int i = 0; i < n; i++)
            vertices.Add(footprint[i] + Vector3.up * height);

        // Roof
        foreach (int t in roofTriangles)
            triangles.Add(t + n);

        // Floor
        for (int i = 0; i < roofTriangles.Length; i += 3)
        {
            triangles.Add(roofTriangles[i]);
            triangles.Add(roofTriangles[i + 2]);
            triangles.Add(roofTriangles[i + 1]);
        }

        // Walls
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;

            int b0 = i;
            int b1 = next;

            int t0 = i + n;
            int t1 = next + n;

            triangles.Add(b0);
            triangles.Add(t0);
            triangles.Add(t1);

            triangles.Add(b0);
            triangles.Add(t1);
            triangles.Add(b1);
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}