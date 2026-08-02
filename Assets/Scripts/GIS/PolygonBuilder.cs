using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Polygonize;
using System.Linq;
public class PolygonBuilder
{
    private GeometryFactory geometryFactory = new GeometryFactory();

    public LineString CreateLineString(
        List<long> nodeIds,
        Dictionary<long, NodeData> nodesDictionary)
    {
        List<Coordinate> coordinates = new List<Coordinate>();

        foreach (long nodeId in nodeIds)
        {
            if (!nodesDictionary.ContainsKey(nodeId))
                continue;

            NodeData node = nodesDictionary[nodeId];

            coordinates.Add(
                new Coordinate(
                    node.Longitude,
                    node.Latitude
                )
            );
        }

        return geometryFactory.CreateLineString(coordinates.ToArray());
    }

    public Polygon BuildPolygon(
    List<List<long>> outerWays,
    List<List<long>> innerWays,
    Dictionary<long, NodeData> nodesDictionary)
    {
        // -------------------------
        // 1. Build OUTER polygon
        // -------------------------

        Polygonizer outerPolygonizer = new Polygonizer();

        foreach (List<long> way in outerWays)
        {
            LineString line = CreateLineString(
                way,
                nodesDictionary
            );

            UnityEngine.Debug.Log(
                $"Outer LineString Points : {line.NumPoints}"
            );

            outerPolygonizer.Add(line);
        }

        var outerPolygons = outerPolygonizer
            .GetPolygons()
            .Cast<Polygon>()
            .ToList();

        UnityEngine.Debug.Log(
            $"Outer Polygons Found : {outerPolygons.Count}"
        );

        if (outerPolygons.Count == 0)
        {
            UnityEngine.Debug.LogWarning(
                "No outer polygon created."
            );

            return null;
        }

        // TEMPORARY:
        // choose largest outer polygon.
        Polygon outerPolygon = outerPolygons
            .OrderByDescending(p => p.Area)
            .First();

        LinearRing shell = geometryFactory.CreateLinearRing(
            outerPolygon.ExteriorRing.Coordinates
        );


        // -------------------------
        // 2. Build INNER polygons
        // -------------------------

        List<LinearRing> holes = new List<LinearRing>();

        if (innerWays != null && innerWays.Count > 0)
        {
            Polygonizer innerPolygonizer = new Polygonizer();

            foreach (List<long> way in innerWays)
            {
                LineString line = CreateLineString(
                    way,
                    nodesDictionary
                );

                UnityEngine.Debug.Log(
                    $"Inner LineString Points : {line.NumPoints}"
                );

                innerPolygonizer.Add(line);
            }

            var innerPolygons = innerPolygonizer
                .GetPolygons()
                .Cast<Polygon>()
                .ToList();

            UnityEngine.Debug.Log(
                $"Inner Polygons Found : {innerPolygons.Count}"
            );

            foreach (Polygon innerPolygon in innerPolygons)
            {
                // Only accept holes actually inside this shell.
                if (!outerPolygon.Covers(innerPolygon))
                {
                    UnityEngine.Debug.LogWarning(
                        "Inner polygon is not inside outer polygon. Skipping."
                    );

                    continue;
                }

                LinearRing hole =
                    geometryFactory.CreateLinearRing(
                        innerPolygon.ExteriorRing.Coordinates
                    );

                holes.Add(hole);
            }
        }


        // -------------------------
        // 3. Create final polygon
        // -------------------------

        Polygon finalPolygon =
            geometryFactory.CreatePolygon(
                shell,
                holes.ToArray()
            );

        UnityEngine.Debug.Log(
            "========== NTS POLYGON BUILT =========="
        );

        UnityEngine.Debug.Log(
            $"Area : {finalPolygon.Area}"
        );

        UnityEngine.Debug.Log(
            $"Exterior Points : {finalPolygon.ExteriorRing.NumPoints}"
        );

        UnityEngine.Debug.Log(
            $"Interior Rings : {finalPolygon.NumInteriorRings}"
        );

        return finalPolygon;
    }
}