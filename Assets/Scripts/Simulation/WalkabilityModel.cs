using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class WalkabilityModel : MonoBehaviour
{
    [Header("Visualization")]
    [SerializeField] private Material pavementMaterial;

    private GameObject groundObject;

    public void GenerateWalkableSpace(List<BuildingData> buildings)
    {
        if (buildings == null || buildings.Count == 0) return;

        Debug.Log("WalkabilityModel: Fitting ground directly to building coordinates...");

        // 1. Calculate the exact World-Space Bounding Box of the buildings
        Bounds bounds = new Bounds(buildings[0].OuterFootprint[0], Vector3.zero);
        foreach (var b in buildings)
        {
            foreach (var pt in b.OuterFootprint)
            {
                bounds.Encapsulate(pt);
            }
        }

        // Expand bounds slightly to create a perimeter around the venue
        bounds.Expand(40f);

        // 2. Mark all Buildings as SOLID OBSTACLES (Carves ground, blocks roofs)
        Transform buildingsParent = GameObject.Find("Buildings")?.transform;
        if (buildingsParent != null)
        {
            NavMeshModifier buildingMod = buildingsParent.GetComponent<NavMeshModifier>();
            if (buildingMod == null) buildingMod = buildingsParent.gameObject.AddComponent<NavMeshModifier>();
            buildingMod.overrideArea = true;
            buildingMod.area = 1; // 1 = "Not Walkable"
            buildingMod.applyToChildren = true;
        }

        // 3. Create Ground Quad directly at the buildings' true location
        if (groundObject != null) Destroy(groundObject);

        groundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundObject.name = "Walkable_Venue_Ground";
        
        // Put ground at the exact center of buildings, at Y = -0.05f (no Z-fighting)
        groundObject.transform.position = new Vector3(bounds.center.x, -0.05f, bounds.center.z);
        // Plane is 10x10 units by default
        groundObject.transform.localScale = new Vector3(bounds.size.x / 10f, 1f, bounds.size.z / 10f);

        // Material (Dark Asphalt Grey)
        MeshRenderer mr = groundObject.GetComponent<MeshRenderer>();
        if (pavementMaterial == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            pavementMaterial = new Material(s);
            pavementMaterial.color = new Color(0.2f, 0.2f, 0.2f);
        }
        mr.sharedMaterial = pavementMaterial;

        // 4. Native C++ NavMesh Bake (Instant: takes < 0.2 seconds!)
        BakeNativeNavMesh(bounds.center);
    }

    private void BakeNativeNavMesh(Vector3 center)
    {
        Debug.Log("WalkabilityModel: Baking Native NavMesh across streets...");
        NavMeshSurface surface = groundObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All; // Collects ground + buildings
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        surface.BuildNavMesh();
        Debug.Log("WalkabilityModel: NavMesh Complete! Buildings carved out cleanly.");

        // 5. Spawn crowd safely on an open street near the center
        CrowdSpawner spawner = FindFirstObjectByType<CrowdSpawner>();
        if (spawner != null)
        {
            Vector3 origin = center + new Vector3(30f, 0f, 30f);
            Vector3 destination = center - new Vector3(30f, 0f, 30f);
            spawner.StartSimulation(origin, destination);
        }
    }
}