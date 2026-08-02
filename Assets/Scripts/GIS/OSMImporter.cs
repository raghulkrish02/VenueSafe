using System.Collections.Generic;
using System.IO;
using System.Xml;
using NetTopologySuite.Geometries;
using UnityEngine;

public class OSMImporter : MonoBehaviour
{
    [SerializeField]
    private CoordinateConverter coordinateConverter;
    [SerializeField]
    private RoadRenderer roadRenderer;
    [SerializeField]
    private BuildingRenderer buildingRenderer;
    private Dictionary<long, NodeData> nodesDictionary =
    new Dictionary<long, NodeData>();
    private Dictionary<long, List<long>> wayNodeDictionary =
    new Dictionary<long, List<long>>();
    private List<RoadData> roads = new List<RoadData>();
    private List<BuildingData> buildings = new List<BuildingData>();
    private void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "map.osm");

        if (!File.Exists(path))
        {
            Debug.LogError("OSM file not found!");
            Debug.Log(path);
            return;
        }

        XmlDocument xml = new XmlDocument();
        xml.Load(path);

        XmlNodeList nodes = xml.GetElementsByTagName("node");
        XmlNodeList ways = xml.GetElementsByTagName("way");
        XmlNodeList relations = xml.GetElementsByTagName("relation");

        NodeData firstNode = ParseNodes(nodes);

        coordinateConverter.SetOrigin(
            firstNode.Latitude,
            firstNode.Longitude
        );

        BuildWayDictionary(ways);

        PolygonBuilder polygonBuilder = new PolygonBuilder();

        foreach (var way in wayNodeDictionary)
        {
            var line = polygonBuilder.CreateLineString(
                way.Value,
                nodesDictionary
            );

            Debug.Log($"Way {way.Key}");
            Debug.Log($"Points : {line.NumPoints}");
            Debug.Log($"Length : {line.Length}");

            break;
        }

        ParseRoads(ways);

        ParseBuildings(ways);

        ParseBuildingRelations(relations);

        

        if (roads.Count > 0)
        {
            Debug.Log(roadRenderer);
            Debug.Log(roads.Count);
            Debug.Log(nodesDictionary.Count);
            foreach (RoadData road in roads)
            {
                roadRenderer.DrawRoad(road, nodesDictionary);
            }
        }
        if (buildings.Count > 0)
        {
            foreach (BuildingData building in buildings)
            {
                Debug.LogWarning(
                    $"DRAW REQUEST | ID={building.Id} | " +
                    $"OuterNodes={building.OuterNodeIds.Count}"
                );

                buildingRenderer.DrawBuilding(
                    building,
                    nodesDictionary,
                    coordinateConverter
                );
            }
        }
        Debug.Log("Dictionary Size : " + nodesDictionary.Count);

        Debug.Log("OSM File Loaded Successfully!");
        Debug.Log("Nodes : " + nodes.Count);
        Debug.Log("Ways : " + ways.Count);
        Debug.Log("Relations : " + relations.Count);

        Debug.Log("Total Roads: " + roads.Count);
        Debug.Log("First Road ID: " + roads[0].Id);
        Debug.Log("Nodes in First Road: " + roads[0].NodeIds.Count);
    }
    private void ParseRoads(XmlNodeList ways)
    {
        foreach (XmlNode way in ways)
        {
            WayType wayType = WayType.Unknown;
            string highwayType = "";
            XmlNodeList tagNodes = way.SelectNodes("tag");

            foreach (XmlNode tag in tagNodes)
            {
                string key = tag.Attributes["k"].Value;
                string value = tag.Attributes["v"].Value;
                if (key == "highway")
                {
                    wayType = WayType.Highway;
                    highwayType = value;
                    break;
                }
                else if (key == "building")
                {
                    wayType = WayType.Building;
                    break;
                }
                else if (key == "railway")
                {
                    wayType = WayType.Railway;
                    break;
                }

            }
            if (wayType != WayType.Highway)
            {
                continue;
            }

            RoadData road = new RoadData();

            XmlNodeList ndNodes = way.SelectNodes("nd");
            road.Id = long.Parse(way.Attributes["id"].Value);
            road.HighwayType = highwayType;
            foreach (XmlNode nd in ndNodes)
            {
                long nodeId = long.Parse(nd.Attributes["ref"].Value);
                road.NodeIds.Add(nodeId);
            }
            roads.Add(road);
            Debug.Log("Road " + road.Id + " : " + road.HighwayType);

        }
    }
    private NodeData ParseNodes(XmlNodeList nodes)
    {
        NodeData firstNode = null;
        foreach (XmlNode node in nodes)
        {
            NodeData nodeData = new NodeData();

            nodeData.Id = long.Parse(node.Attributes["id"].Value);
            nodeData.Latitude = double.Parse(node.Attributes["lat"].Value);
            nodeData.Longitude = double.Parse(node.Attributes["lon"].Value);

            if (firstNode == null)
            {
                firstNode = nodeData;
            }

            nodesDictionary.Add(nodeData.Id, nodeData);
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
                nodeList.Add(
                    long.Parse(nd.Attributes["ref"].Value)
                );
            }

            wayNodeDictionary[wayId] = nodeList;
        }

        Debug.Log("Way Dictionary : " + wayNodeDictionary.Count);
    }
    private void ParseBuildings(XmlNodeList ways)
{
        foreach (XmlNode way in ways)
        {
            WayType wayType = WayType.Unknown;

            float? explicitHeight = null;
            int? levels = null;

            XmlNodeList tagNodes = way.SelectNodes("tag");

            foreach (XmlNode tag in tagNodes)
            {
                string key = tag.Attributes["k"].Value;
                string value = tag.Attributes["v"].Value;

                Debug.Log($"{way.Attributes["id"].Value} : {key} = {value}");

                if (key == "building")
                {
                    wayType = WayType.Building;
                }

                else if (key == "height")
                {
                    string h = value.Replace("m", "").Trim();

                    if (float.TryParse(
                            h,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float parsed))
                    {
                        explicitHeight = parsed;
                    }
                }

                else if (key == "building:levels")
                {
                    if (int.TryParse(value, out int parsed))
                    {
                        levels = parsed;
                    }
                }
            }

            if (wayType != WayType.Building)
            {
                continue;
            }

            long debugId = long.Parse(way.Attributes["id"].Value);

            Debug.LogWarning(
                $"SIMPLE BUILDING PARSED | ID={debugId} | " +
                $"ND={way.SelectNodes("nd").Count}"
            );

            BuildingData building = new BuildingData();

            building.Id = long.Parse(way.Attributes["id"].Value);

            if (explicitHeight.HasValue)
            {
                building.Height = explicitHeight.Value;
            }
            else if (levels.HasValue)
            {
                building.Height = levels.Value * 3.2f;
            }
            else
            {
                building.Height = 8f;
            }

            Debug.Log(
    $"Building {building.Id} | Height={building.Height}m | Levels={levels}"
);

            if (building.Id == 556538997 || building.Id == 556538998)
            {
                Debug.LogWarning(
                    $"TARGET BUILDING FOUND | ID={building.Id}"
                );
            }

            XmlNodeList ndNodes = way.SelectNodes("nd");

            foreach (XmlNode nd in ndNodes)
            {
                long nodeId = long.Parse(nd.Attributes["ref"].Value);

                building.OuterNodeIds.Add(nodeId);
            }

            if (building.Id == 556538997 || building.Id == 556538998)
            {
                Debug.LogWarning(
                    $"TARGET BUILDING PARSED | ID={building.Id} | " +
                    $"Nodes={building.OuterNodeIds.Count} | " +
                    $"Closed={building.OuterNodeIds[0] == building.OuterNodeIds[building.OuterNodeIds.Count - 1]}"
                );
            }

            buildings.Add(building);

            Debug.Log("Building : " + building.Id);
        }
    }
    private void ParseBuildingRelations(XmlNodeList relations)
    {
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

                    if (float.TryParse(
                        h,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float parsed))
                    {
                        explicitHeight = parsed;
                    }
                }

                if (key == "building:levels")
                {
                    if (int.TryParse(value, out int parsed))
                    {
                        levels = parsed;
                    }
                }

                if (key == "type" && value == "multipolygon")
                {
                    isMultipolygon = true;
                }

                if (key == "building" && value != "no")
                {
                    isBuilding = true;
                }
            }

            if (!isMultipolygon || !isBuilding)
            {
                continue;
            }

            Debug.Log($"BUILDING MULTIPOLYGON | Relation={relationId}");

            BuildingData building = new BuildingData();

            building.Id = long.Parse(relationId);

            if (explicitHeight.HasValue)
            {
                building.Height = explicitHeight.Value;
            }
            else if (levels.HasValue)
            {
                building.Height = levels.Value * 3.2f;
            }
            else
            {
                building.Height = 8f;
            }


            List<List<long>> outerWays = new List<List<long>>();

            List<List<long>> innerWays = new List<List<long>>();

            foreach (XmlNode member in relation.SelectNodes("member"))
            {
                if (member.Attributes["type"].Value != "way")
                    continue;

                long wayId = long.Parse(member.Attributes["ref"].Value);
                string role = member.Attributes["role"].Value;

                if (role == "outer")
                {
                    Debug.Log("Outer Way : " + wayId);

                    if (wayNodeDictionary.ContainsKey(wayId))
                    {
                        Debug.Log("FOUND");

                        outerWays.Add(
                            new List<long>(wayNodeDictionary[wayId])
                        );
                    }
                    else
                    {
                        Debug.LogWarning($"Missing way in OSM extract: {wayId}");
                    }
                }

                else if (role == "inner")
                {
                    Debug.Log("Inner Way : " + wayId);

                    if (wayNodeDictionary.ContainsKey(wayId))
                    {
                        Debug.Log("FOUND");

                        List<long> innerWay =
                            new List<long>(wayNodeDictionary[wayId]);

                        innerWays.Add(innerWay);

                        // Keep old representation temporarily
                        building.InnerNodeIds.Add(
                            new List<long>(innerWay)
                        );
                    }
                    else
                    {
                        Debug.LogWarning($"Missing way in OSM extract: {wayId}");
                    }
                }
            }

            if (innerWays.Count > 0)
            {
                Debug.Log(
                    $"===== RELATION WITH INNER FOUND | ID={building.Id} | " +
                    $"OuterWays={outerWays.Count} | InnerWays={innerWays.Count} ====="
                );
                foreach (List<long> inner in innerWays)
                {
                    bool closed =
                        inner.Count >= 4 &&
                        inner[0] == inner[inner.Count - 1];

                    Debug.Log(
                        $"INNER TEST | Nodes={inner.Count} | " +
                        $"First={inner[0]} | Last={inner[inner.Count - 1]} | " +
                        $"Closed={closed}"
                    );
                }
            }

            //if (outerWays.Count == 1)
            //{
            //    continue;
            //}

            PolygonBuilder polygonBuilder = new PolygonBuilder();

            Debug.Log("Outer Ways Count = " + outerWays.Count);

            Polygon polygon = polygonBuilder.BuildPolygon(
                outerWays,
                innerWays,
                nodesDictionary
            );

            if (polygon != null)
            {
                Debug.Log("========== POLYGON TEST PASSED ==========");

                Debug.Log(
                    $"RENDER RELATION | ID={building.Id}" +
                    $" | Area={polygon.Area}" +
                    $" | OuterWays={outerWays.Count}" +
                    $" | InnerWays={innerWays.Count}"
                );

                buildingRenderer.DrawPolygon(
                    polygon,
                    coordinateConverter,
                    building
                );

                Debug.Log(
                    $"========== NTS RENDER REQUESTED : {building.Id} =========="
                );

                continue; 
            }

            building.OuterNodeIds = StitchWays(outerWays);

            if (building.OuterNodeIds.Count > 0)
            {
                Debug.Log($"Relation {building.Id}");
                Debug.Log($"Outer Node Count = {building.OuterNodeIds.Count}");
                Debug.Log($"First Node = {building.OuterNodeIds[0]}");
                Debug.Log($"Last Node = {building.OuterNodeIds[building.OuterNodeIds.Count - 1]}");
            }

            buildings.Add(building);

            Debug.Log("Relation Building : " + building.Id);
        }
    }

    private List<long> StitchWays(List<List<long>> ways)
    {
        List<long> result = new List<long>();

        if (ways.Count == 0)
            return result;

        List<List<long>> remaining = new List<List<long>>(ways);

        // Start with the first way
        result.AddRange(remaining[0]);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            bool found = false;

            long endNode = result[result.Count - 1];

            for (int i = 0; i < remaining.Count; i++)
            {
                List<long> way = remaining[i];

                long first = way[0];
                long last = way[way.Count - 1];

                // Normal direction
                if (first == endNode)
                {
                    result.AddRange(way.GetRange(1, way.Count - 1));

                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }

                // Reverse direction
                if (last == endNode)
                {
                    way.Reverse();

                    result.AddRange(way.GetRange(1, way.Count - 1));

                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning("Could not stitch remaining ways.");
                break;
            }
        }

        // Close polygon if necessary
        if (result.Count > 0 && result[0] != result[result.Count - 1])
        {
            result.Add(result[0]);
        }

        return result;
    }
}