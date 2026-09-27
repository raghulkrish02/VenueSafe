using System.Collections.Generic;
using UnityEngine;

public class BuildingData
{
    public long Id;
    public float Height = 8f;

    // Outer contour in Unity World Coordinates (XZ plane)
    public List<Vector3> OuterFootprint = new List<Vector3>();

    // Optional Inner contours (holes) in Unity World Coordinates
    public List<List<Vector3>> InnerFootprints = new List<List<Vector3>>();
}