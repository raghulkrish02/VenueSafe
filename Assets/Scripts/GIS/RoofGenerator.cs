using UnityEngine;

public static class RoofGenerator
{
    public static Mesh BuildRoof(
        Vector3[] vertices,
        int[] triangles)
    {
        Mesh roof = new Mesh();

        roof.vertices = vertices;
        roof.triangles = triangles;

        roof.RecalculateNormals();
        roof.RecalculateBounds();

        return roof;
    }
}