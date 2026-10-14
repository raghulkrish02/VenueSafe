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
        // Double-sided rendering (6 indices for top face + 6 indices for bottom face per quad)
        int[] triangles = new int[(count - 1) * 12];

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
            Vector3 normal = new Vector3(-forward.z, 0f, forward.x).normalized;

            // Place roads at Y = 0.05f so they sit cleanly on top of Walkable_Venue_Ground (Y = -0.05f)
            Vector3 left = new Vector3(current.x + normal.x * halfWidth, 0.05f, current.z + normal.z * halfWidth);
            Vector3 right = new Vector3(current.x - normal.x * halfWidth, 0.05f, current.z - normal.z * halfWidth);

            vertices[i * 2] = left;
            vertices[i * 2 + 1] = right;

            uvs[i * 2] = new Vector2(0f, (float)i / count);
            uvs[i * 2 + 1] = new Vector2(1f, (float)i / count);

            if (i < count - 1)
            {
                int root = i * 2;
                int triIdx = i * 12;

                // --- TOP FACE (Clockwise from above: Upward Normal) ---
                triangles[triIdx] = root;
                triangles[triIdx + 1] = root + 2;
                triangles[triIdx + 2] = root + 1;

                triangles[triIdx + 3] = root + 1;
                triangles[triIdx + 4] = root + 2;
                triangles[triIdx + 5] = root + 3;

                // --- BOTTOM FACE (Reverse Winding for Double-Sided Visibility) ---
                triangles[triIdx + 6] = root;
                triangles[triIdx + 7] = root + 1;
                triangles[triIdx + 8] = root + 2;

                triangles[triIdx + 9] = root + 1;
                triangles[triIdx + 10] = root + 3;
                triangles[triIdx + 11] = root + 2;
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