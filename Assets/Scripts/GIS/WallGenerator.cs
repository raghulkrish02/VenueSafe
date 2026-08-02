using System.Collections.Generic;
using UnityEngine;

public static class WallGenerator
{
    public static Mesh BuildWalls(
        List<Vector3> footprint,
        float height)
    {
        Mesh mesh = new Mesh();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int index = 0;

        for (int i = 0; i < footprint.Count; i++)
        {
            int next = (i + 1) % footprint.Count;

            Vector3 p0 = footprint[i];
            Vector3 p1 = footprint[next];

            Vector3 b0 = p0;
            Vector3 b1 = p1;

            Vector3 t0 = p0 + Vector3.up * height;
            Vector3 t1 = p1 + Vector3.up * height;

            vertices.Add(b0);   // index
            vertices.Add(b1);   // index+1
            vertices.Add(t1);   // index+2
            vertices.Add(t0);   // index+3

            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);

            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);

            index += 4;
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}