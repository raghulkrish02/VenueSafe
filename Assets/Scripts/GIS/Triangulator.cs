using System.Collections.Generic;
using UnityEngine;
using LibTessDotNet;
public static class Triangulator
{
    public static int[] Triangulate(List<Vector3> vertices)
    {
        List<int> triangles = new List<int>();

        if (vertices.Count < 3)
            return triangles.ToArray();

        var tess = new Tess();

        ContourVertex[] contour = new ContourVertex[vertices.Count];

        for (int i = 0; i < vertices.Count; i++)
        {
            contour[i].Position = new Vec3()
            {
                X = vertices[i].x,
                Y = vertices[i].z,
                Z = 0
            };
        }

        tess.AddContour(contour);

        tess.Tessellate(
            WindingRule.EvenOdd,
            ElementType.Polygons,
            3);

        Debug.Log("Original Vertices = " + vertices.Count);
        Debug.Log("LibTess Vertices = " + tess.Vertices.Length);
        Debug.Log("Triangle Indices = " + tess.Elements.Length);

        return tess.Elements;
    }

    public static void RemoveClosingVertex(List<Vector3> vertices)
    {
        if (vertices.Count < 2)
            return;

        if (vertices[0] == vertices[vertices.Count - 1])
        {
            vertices.RemoveAt(vertices.Count - 1);
        }
    }

    public static bool IsClockwise(List<Vector3> vertices)
    {
        float sum = 0f;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % vertices.Count];

            sum += (b.x - a.x) * (b.z + a.z);
        }

        return sum > 0;
    }

}