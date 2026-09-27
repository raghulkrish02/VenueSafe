using System.Collections.Generic;
using System.IO;
using System.Xml;
using NetTopologySuite.Geometries;
using UnityEngine;

public class OSMImporter : MonoBehaviour
{
    [SerializeField] private CoordinateConverter coordinateConverter;
    [SerializeField] private RoadRenderer roadRenderer;
    [SerializeField] private BuildingRenderer buildingRenderer;

    private Dictionary<long, NodeData> nodesDictionary = new Dictionary<long, NodeData>();
    private Dictionary<long, List<long>> wayNodeDictionary = new Dictionary<long, List<long>>();
    private List<RoadData> roads = new List<RoadData>();
    private List<BuildingData> buildings = new List<BuildingData>();

    private void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "map.osm");

        if (!File.Exists(path))
        {
            Debug.LogError("OSM file not found at path: " + path);
            return;
        }

        XmlDocument xml = new XmlDocument();
        xml.Load(path);

        XmlNodeList nodes = xml.GetElementsByTagName("node");
        XmlNodeList ways = xml.GetElementsByTagName("way");
        XmlNodeList relations = xml.GetElementsByTagName("relation");

        NodeData firstNode = ParseNodes(nodes);
        if (firstNode == null)
        {
            Debug.LogError("No valid nodes found in OSM file!");
            return;
        }

        coordinateConverter.SetOrigin(firstNode.Latitude, firstNode.Longitude);

        BuildWayDictionary(ways);

        ParseRoads(ways);
        ParseBuildings(ways);
        ParseBuildingRelations(relations);

        // ------------------------------------
        // Unified Render Phase
        // ------------------------------------
        if (roads.Count > 0 && roadRenderer != null)
        {
            foreach (RoadData road in roads)
            {
                roadRenderer.DrawRoad(road, nodesDictionary);
            }
        }

        if (buildings.Count > 0 && buildingRenderer != null)
        {
            foreach (BuildingData building in buildings)
            {
                buildingRenderer.DrawBuilding(building);
            }
        }

        Debug.Log($"OSM Import Complete | Nodes: {nodes.Count} | Roads: {roads.Count} | Buildings Rendered: {buildings.Count}");
    }

    private NodeData ParseNodes(XmlNodeList nodes)
    {
        NodeData firstNode = null;
        foreach (XmlNode node in nodes)
        {
            NodeData nodeData = new NodeData
            {
                Id = long.Parse(node.Attributes["id"].Value),
                Latitude = double.Parse(node.Attributes["lat"].Value, System.Globalization.CultureInfo.InvariantCulture),
                Longitude = double.Parse(node.Attributes["lon"].Value, System.Globalization.CultureInfo.InvariantCulture)
            };

            if (firstNode == null) firstNode = nodeData;
            nodesDictionary[nodeData.Id] = nodeData;
        }
        return firstNode;
    }

    private void BuildWayDictionary(XmlNodeList ways)
    {
        foreach (XmlNode way in ways)
        {
            long wayId = long.Parse(way.Attributes["id"].Value);
            List<long> nodeList = new List<long>();

            foreach (XmlNode nd in way.SelectNodes("nd"))
            {
                nodeList.Add(long.Parse(nd.Attributes["ref"].Value));
            }

            wayNodeDictionary[wayId] = nodeList;
        }
    }

    private void ParseRoads(XmlNodeList ways)
    {
        foreach (XmlNode way in ways)
        {
            WayType wayType = WayType.Unknown;
            string highwayType = "";

            foreach (XmlNode tag in way.SelectNodes("tag"))
            {
                string key = tag.Attributes["k"].Value;
                if (key == "highway")
                {
                    wayType = WayType.Highway;
                    highwayType = tag.Attributes["v"].Value;
                    break;
                }
            }

            if (wayType != WayType.Highway) continue;

            RoadData road = new RoadData
            {
                Id = long.Parse(way.Attributes["id"].Value),
                HighwayType = highwayType
            };

            foreach (XmlNode nd in way.SelectNodes("nd"))
            {
                road.NodeIds.Add(long.Parse(nd.Attributes["ref"].Value));
            }

            roads.Add(road);
        }
    }

    private void ParseBuildings(XmlNodeList ways)
    {
        foreach (XmlNode way in ways)
        {
            WayType wayType = WayType.Unknown;
            float? explicitHeight = null;
            int? levels = null;

            foreach (XmlNode tag in way.SelectNodes("tag"))
            {
                string key = tag.Attributes["k"].Value;
                string value = tag.Attributes["v"].Value;

                if (key == "building") wayType = WayType.Building;
                else if (key == "height")
                {
                    string h = value.Replace("m", "").Trim();
                    if (float.TryParse(h, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                        explicitHeight = parsed;
                }
                else if (key == "building:levels")
                {
                    if (int.TryParse(value, out int parsed)) levels = parsed;
                }
            }

            if (wayType != WayType.Building) continue;

            BuildingData building = new BuildingData
            {
                Id = long.Parse(way.Attributes["id"].Value),
                Height = explicitHeight ?? (levels.HasValue ? levels.Value * 3.2f : 8f)
            };

            foreach (XmlNode nd in way.SelectNodes("nd"))
            {
                long nodeId = long.Parse(nd.Attributes["ref"].Value);
                if (nodesDictionary.TryGetValue(nodeId, out NodeData node))
                {
                    Vector3 worldPos = coordinateConverter.ToUnityPosition(node.Latitude, node.Longitude);
                    building.OuterFootprint.Add(worldPos);
                }
            }

            if (building.OuterFootprint.Count >= 3)
            {
                buildings.Add(building);
            }
        }
    }

    private void ParseBuildingRelations(XmlNodeList relations)
    {
        PolygonBuilder polygonBuilder = new PolygonBuilder();

        foreach (XmlNode relation in relations)
        {
            bool isMultipolygon = false;
            bool isBuilding = false;
            float? explicitHeight = null;
            int? levels = null;

            string relationId = relation.Attributes["id"].Value;

            foreach (XmlNode tag in relation.SelectNodes("tag"))
            {
                string key = tag.Attributes["k"].Value;
                string value = tag.Attributes["v"].Value;

                if (key == "height")
                {
                    string h = value.Replace("m", "").Trim();
                    if (float.TryParse(h, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                        explicitHeight = parsed;
                }
                else if (key == "building:levels")
                {
                    if (int.TryParse(value, out int parsed)) levels = parsed;
                }
                else if (key == "type" && value == "multipolygon") isMultipolygon = true;
                else if (key == "building" && value != "no") isBuilding = true;
            }

            if (!isMultipolygon || !isBuilding) continue;

            BuildingData building = new BuildingData
            {
                Id = long.Parse(relationId),
                Height = explicitHeight ?? (levels.HasValue ? levels.Value * 3.2f : 8f)
            };

            List<List<long>> outerWays = new List<List<long>>();
            List<List<long>> innerWays = new List<List<long>>();

            foreach (XmlNode member in relation.SelectNodes("member"))
            {
                if (member.Attributes["type"].Value != "way") continue;

                long wayId = long.Parse(member.Attributes["ref"].Value);
                string role = member.Attributes["role"].Value;

                if (wayNodeDictionary.TryGetValue(wayId, out List<long> wayNodes))
                {
                    if (role == "outer") outerWays.Add(new List<long>(wayNodes));
                    else if (role == "inner") innerWays.Add(new List<long>(wayNodes));
                }
            }

            // 1. Try NTS Polygon Construction (for courtyards/holes)
            Polygon polygon = polygonBuilder.BuildPolygon(outerWays, innerWays, nodesDictionary);

            if (polygon != null && !polygon.IsEmpty)
            {
                // Convert NTS Outer Ring (Coordinates are Lon, Lat)
                foreach (var coord in polygon.ExteriorRing.Coordinates)
                {
                    Vector3 worldPos = coordinateConverter.ToUnityPosition(coord.Y, coord.X);
                    building.OuterFootprint.Add(worldPos);
                }

                // Convert NTS Holes / Courtyards
                for (int h = 0; h < polygon.NumInteriorRings; h++)
                {
                    List<Vector3> holeFootprint = new List<Vector3>();
                    foreach (var coord in polygon.GetInteriorRingN(h).Coordinates)
                    {
                        Vector3 worldPos = coordinateConverter.ToUnityPosition(coord.Y, coord.X);
                        holeFootprint.Add(worldPos);
                    }
                    building.InnerFootprints.Add(holeFootprint);
                }

                buildings.Add(building);
                continue;
            }

            // 2. Fallback: Stitched Outer Way
            List<long> stitchedNodes = StitchWays(outerWays);
            foreach (long nodeId in stitchedNodes)
            {
                if (nodesDictionary.TryGetValue(nodeId, out NodeData node))
                {
                    Vector3 worldPos = coordinateConverter.ToUnityPosition(node.Latitude, node.Longitude);
                    building.OuterFootprint.Add(worldPos);
                }
            }

            if (building.OuterFootprint.Count >= 3)
            {
                buildings.Add(building);
            }
        }
    }

    private List<long> StitchWays(List<List<long>> ways)
    {
        List<long> result = new List<long>();
        if (ways.Count == 0) return result;

        List<List<long>> remaining = new List<List<long>>(ways);
        result.AddRange(remaining[0]);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            bool found = false;
            long endNode = result[result.Count - 1];

            for (int i = 0; i < remaining.Count; i++)
            {
                List<long> way = remaining[i];
                if (way[0] == endNode)
                {
                    result.AddRange(way.GetRange(1, way.Count - 1));
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
                if (way[way.Count - 1] == endNode)
                {
                    way.Reverse();
                    result.AddRange(way.GetRange(1, way.Count - 1));
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
            }

            if (!found) break;
        }

        if (result.Count > 0 && result[0] != result[result.Count - 1])
        {
            result.Add(result[0]);
        }

        return result;
    }
}