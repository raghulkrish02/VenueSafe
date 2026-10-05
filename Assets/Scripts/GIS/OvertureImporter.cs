using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;

public class OvertureImporter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CoordinateConverter coordinateConverter;
    [SerializeField] private BuildingRenderer buildingRenderer;
    [SerializeField] private WalkabilityModel walkabilityModel;

    [Header("Data Files")]
    [SerializeField] private string buildingsFileName = "overture_buildings.geojson";
    [SerializeField] private string roadsFileName = "overture_roads.geojson";

    private Transform roadsParent;
    private Material sharedRoadMaterial;
    private bool originSet = false;

    private List<BuildingData> buildings = new List<BuildingData>();

    private void Awake()
    {
        // Dark asphalt material for real roads
        sharedRoadMaterial = new Material(Shader.Find("Sprites/Default"));
        sharedRoadMaterial.color = new Color(0.18f, 0.18f, 0.18f); // Asphalt Charcoal Grey
    }

    private void Start()
    {
        // 1. Load Buildings First (Sets Origin & Renders 3D Structures)
        LoadBuildings();

        // 2. Generate Walkable Pedestrian Space & Bake NavMesh using NTS
        if (walkabilityModel != null && buildings != null)
        {
            walkabilityModel.GenerateWalkableSpace(buildings);
        }
        else
        {
            Debug.LogWarning("WalkabilityModel or buildings list is null. Skipping walkable space generation.");
        }
    }

    private void LoadBuildings()
    {
        string path = Path.Combine(Application.streamingAssetsPath, buildingsFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"Buildings file not found: {path}");
            return;
        }

        Debug.Log("Loading Overture Buildings...");
        string jsonText = File.ReadAllText(path);
        JObject root = JObject.Parse(jsonText);
        JArray features = (JArray)root["features"];
        if (features == null) return;

        buildings.Clear();
        long idCounter = 1;

        foreach (JObject feature in features)
        {
            JObject geometry = (JObject)feature["geometry"];
            JObject properties = (JObject)feature["properties"];
            if (geometry == null) continue;

            string geomType = (string)geometry["type"];
            JArray coordinates = (JArray)geometry["coordinates"];
            if (coordinates == null) continue;

            float height = 8f;
            if (properties != null)
            {
                if (properties["height"] != null) height = (float)properties["height"];
                else if (properties["num_floors"] != null) height = (int)properties["num_floors"] * 3.2f;
            }

            if (geomType == "Polygon")
            {
                BuildingData b = ParsePolygon(coordinates);
                if (b != null) { b.Id = idCounter++; b.Height = height; buildings.Add(b); }
            }
            else if (geomType == "MultiPolygon")
            {
                foreach (JArray polyCoords in coordinates)
                {
                    BuildingData b = ParsePolygon(polyCoords);
                    if (b != null) { b.Id = idCounter++; b.Height = height; buildings.Add(b); }
                }
            }
        }

        foreach (BuildingData b in buildings)
        {
            buildingRenderer.DrawBuilding(b);
        }

        Debug.Log($"Overture Buildings Complete: {buildings.Count} rendered.");
    }

    private BuildingData ParsePolygon(JArray polygonCoordinates)
    {
        if (polygonCoordinates.Count == 0) return null;
        JArray exteriorRing = (JArray)polygonCoordinates[0];
        if (exteriorRing == null || exteriorRing.Count < 3) return null;

        if (!originSet)
        {
            double firstLon = (double)exteriorRing[0][0];
            double firstLat = (double)exteriorRing[0][1];
            coordinateConverter.SetOrigin(firstLat, firstLon);
            originSet = true;
        }

        BuildingData building = new BuildingData();
        foreach (JArray point in exteriorRing)
        {
            Vector3 worldPos = coordinateConverter.ToUnityPosition((double)point[1], (double)point[0]);
            building.OuterFootprint.Add(worldPos);
        }

        for (int h = 1; h < polygonCoordinates.Count; h++)
        {
            JArray holeRing = (JArray)polygonCoordinates[h];
            if (holeRing == null || holeRing.Count < 3) continue;

            List<Vector3> hole = new List<Vector3>();
            foreach (JArray point in holeRing)
            {
                Vector3 worldPos = coordinateConverter.ToUnityPosition((double)point[1], (double)point[0]);
                hole.Add(worldPos);
            }
            building.InnerFootprints.Add(hole);
        }

        return building;
    }

    private void LoadRoads()
    {
        string path = Path.Combine(Application.streamingAssetsPath, roadsFileName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Roads file not found at: {path}. Skipping roads.");
            return;
        }

        Debug.Log("Loading Overture Road Network...");
        string jsonText = File.ReadAllText(path);
        JObject root = JObject.Parse(jsonText);
        JArray features = (JArray)root["features"];
        if (features == null) return;

        GameObject roadsObj = new GameObject("Roads");
        roadsParent = roadsObj.transform;

        int roadCount = 0;

        foreach (JObject feature in features)
        {
            JObject geometry = (JObject)feature["geometry"];
            JObject properties = (JObject)feature["properties"];
            if (geometry == null) continue;

            string geomType = (string)geometry["type"];
            JArray coordinates = (JArray)geometry["coordinates"];
            if (coordinates == null) continue;

            // Determine physical road width based on class
            string roadClass = properties?["class"]?.ToString();
            float roadWidth = GetRoadWidth(roadClass);

            if (geomType == "LineString")
            {
                CreateRoadSegment(coordinates, roadWidth, roadCount++);
            }
            else if (geomType == "MultiLineString")
            {
                foreach (JArray lineCoords in coordinates)
                {
                    CreateRoadSegment(lineCoords, roadWidth, roadCount++);
                }
            }
        }

        Debug.Log($"Overture Roads Complete: {roadCount} road ribbons generated.");
    }

    private void CreateRoadSegment(JArray coordinates, float width, int index)
    {
        if (coordinates.Count < 2) return;

        List<Vector3> centerline = new List<Vector3>();
        foreach (JArray pt in coordinates)
        {
            double lon = (double)pt[0];
            double lat = (double)pt[1];
            Vector3 worldPos = coordinateConverter.ToUnityPosition(lat, lon);

            // CLIPPING: Only keep road points within the city area (+- 1500m from center)
            if (Mathf.Abs(worldPos.x) < 1500f && Mathf.Abs(worldPos.z) < 1500f)
            {
                centerline.Add(worldPos);
            }
        }

        // If the road was completely outside the venue, discard it!
        if (centerline.Count < 2) return;

        Mesh roadMesh = RoadMeshGenerator.GenerateRoadRibbon(centerline, width);
        if (roadMesh == null) return;

        GameObject roadObj = new GameObject($"Road_{index}");
        roadObj.transform.SetParent(roadsParent, false);

        MeshFilter mf = roadObj.AddComponent<MeshFilter>();
        mf.sharedMesh = roadMesh;

        MeshRenderer mr = roadObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = sharedRoadMaterial;

        // NOTE: We REMOVED MeshCollider here! This eliminates the 2-minute CPU freeze on your laptop.
    }

    private float GetRoadWidth(string roadClass)
    {
        switch (roadClass?.ToLower())
        {
            case "motorway":
            case "trunk":
                return 14f;
            case "primary":
                return 10f;
            case "secondary":
                return 8f;
            case "tertiary":
                return 6.5f;
            case "residential":
            case "unclassified":
                return 5f;
            case "service":
            case "living_street":
                return 4f;
            case "pedestrian":
            case "footway":
            case "path":
            case "steps":
                return 3f;
            default:
                return 5f;
        }
    }

    private void BakeRoadNavMesh()
    {
        if (roadsParent == null || roadsParent.childCount == 0) return;

        Debug.Log("Baking NavMesh on venue roads...");
        NavMeshSurface surface = roadsParent.gameObject.AddComponent<NavMeshSurface>();
        
        // Bake directly from the visual road meshes (takes < 2 seconds!)
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.collectObjects = CollectObjects.Children;
        surface.BuildNavMesh();
        
        Debug.Log("Road NavMesh Baking Complete! Roads are ready for crowd simulation.");
    }
}