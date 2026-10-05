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

    public static Mesh CombineThree(
        Mesh mesh1,
        Mesh mesh2,
        Mesh mesh3)
    {
        CombineInstance[] combine = new CombineInstance[3];

        combine[0].mesh = mesh1;
        combine[0].transform = Matrix4x4.identity;

        combine[1].mesh = mesh2;
        combine[1].transform = Matrix4x4.identity;

        combine[2].mesh = mesh3;
        combine[2].transform = Matrix4x4.identity;

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);

        return combinedMesh;
    }
}