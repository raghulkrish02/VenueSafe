using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class OSMImporter : MonoBehaviour
{
    [SerializeField]
    private CoordinateConverter coordinateConverter;
    [SerializeField]
    private RoadRenderer roadRenderer;
    private Dictionary<long, NodeData> nodesDictionary =
    new Dictionary<long, NodeData>();
    private List<RoadData> roads = new List<RoadData>();
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
        foreach (XmlNode way in ways)
        {
            RoadData road = new RoadData();

            XmlNodeList ndNodes = way.SelectNodes("nd");
            road.Id = long.Parse(way.Attributes["id"].Value);
            foreach (XmlNode nd in ndNodes)
            {
                long nodeId = long.Parse(nd.Attributes["ref"].Value);
                road.NodeIds.Add(nodeId);
            }
            roads.Add(road);

        }

        coordinateConverter.SetOrigin(
    firstNode.Latitude,
    firstNode.Longitude
);

        if (roads.Count > 0)
        {
            Debug.Log(roadRenderer);
            Debug.Log(roads.Count);
            Debug.Log(nodesDictionary.Count);
            roadRenderer.DrawRoad(roads[0], nodesDictionary);
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
}