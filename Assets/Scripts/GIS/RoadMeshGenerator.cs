using System.Collections.Generic;
using UnityEngine;

public static class RoadMeshGenerator
{
    public static Mesh GenerateRoadRibbon(List<Vector3> centerline, float width)
    {
        if (centerline == null || centerline.Count < 2) return null;

        int count = centerline.Count;
        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uvs = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];

        float halfWidth = width * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector3 current = centerline[i];
            Vector3 forward;

            if (i == 0)
            {
                forward = (centerline[1] - current).normalized;
            }
            else if (i == count - 1)
            {
                forward = (current - centerline[i - 1]).normalized;
            }
            else
            {
                Vector3 prevDir = (current - centerline[i - 1]).normalized;
                Vector3 nextDir = (centerline[i + 1] - current).normalized;
                forward = (prevDir + nextDir).normalized;
            }

            // Perpendicular vector across the road on XZ plane
            Vector3 normal = new Vector3(-forward.z, 0, forward.x).normalized;

            // Elevate slightly to Y = 0.05f to sit cleanly above ground
            Vector3 left = new Vector3(current.x - normal.x * halfWidth, 0.05f, current.z - normal.z * halfWidth);
            Vector3 right = new Vector3(current.x + normal.x * halfWidth, 0.05f, current.z + normal.z * halfWidth);

            vertices[i * 2] = left;
            vertices[i * 2 + 1] = right;

            uvs[i * 2] = new Vector2(0f, (float)i / count);
            uvs[i * 2 + 1] = new Vector2(1f, (float)i / count);

            if (i < count - 1)
            {
                int root = i * 2;
                int triIndex = i * 6;

                triangles[triIndex] = root;
                triangles[triIndex + 1] = root + 2;
                triangles[triIndex + 2] = root + 1;

                triangles[triIndex + 3] = root + 1;
                triangles[triIndex + 4] = root + 2;
                triangles[triIndex + 5] = root + 3;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "RoadRibbon";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}