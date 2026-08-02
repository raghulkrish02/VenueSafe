using System.Collections.Generic;
using UnityEngine;

public class RoadRenderer : MonoBehaviour
{
    [SerializeField]
    private CoordinateConverter coordinateConverter;
    private Transform roadsParent;
    public void DrawRoad(
     RoadData road,
     Dictionary<long, NodeData> nodesDictionary)
    {
        if (roadsParent == null)
        {
            GameObject roadsObject = new GameObject("Roads");
            roadsParent = roadsObject.transform;
        }
        GameObject roadObject = new GameObject("Road");
        roadObject.transform.SetParent(roadsParent);

        LineRenderer lineRenderer = roadObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;

        List<Vector3> roadPoints = new List<Vector3>();
        foreach (long nodeId in road.NodeIds)
        {
            NodeData node = nodesDictionary[nodeId];

            Vector3 point = coordinateConverter.ToUnityPosition(
                node.Latitude,
                node.Longitude
            );

            roadPoints.Add(point);
        }

        lineRenderer.positionCount = roadPoints.Count;
        lineRenderer.SetPositions(roadPoints.ToArray());
        Debug.Log(roadPoints[0]);
        Debug.Log(roadPoints[roadPoints.Count - 1]);

        lineRenderer.startWidth = 2f;
        lineRenderer.endWidth = 2f;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
    }
}