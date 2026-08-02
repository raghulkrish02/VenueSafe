using UnityEngine;

public static class MeshCombiner
{
    public static Mesh Combine(
        Mesh roof,
        Mesh walls)
    {
        CombineInstance[] combine =
            new CombineInstance[2];

        combine[0].mesh = roof;
        combine[0].transform = Matrix4x4.identity;

        combine[1].mesh = walls;
        combine[1].transform = Matrix4x4.identity;

        Mesh mesh = new Mesh();

        mesh.CombineMeshes(combine);

        return mesh;
    }
}