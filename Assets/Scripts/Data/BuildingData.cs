using UnityEngine;

using System.Collections.Generic;

public class BuildingData
{
    public long Id;

    public float Height = 8f;

    // Simple building (ordinary way)
    public List<long> OuterNodeIds = new List<long>();

    // Multipolygon relation
    public List<long> OuterWayIds = new List<long>();

    public List<long> InnerWayIds = new List<long>();

    // Final polygons after reconstruction
    public List<List<long>> InnerNodeIds = new List<List<long>>();
}