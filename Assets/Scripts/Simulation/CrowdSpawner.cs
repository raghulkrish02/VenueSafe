using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CrowdSpawner : MonoBehaviour
{
    [Header("Simulation Settings")]
    [SerializeField] private int totalAgents = 25;
    [SerializeField] private float spawnRate = 0.5f;

    private int agentsSpawned = 0;

    // Called DIRECTLY by OvertureImporter once the NavMesh is 100% baked
    public void StartSimulation(Vector3 origin, Vector3 destination)
    {
        // Snap origin and destination cleanly onto the baked pavement NavMesh
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit1, 20f, NavMesh.AllAreas))
        {
            origin = hit1.position;
        }
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit2, 20f, NavMesh.AllAreas))
        {
            destination = hit2.position;
        }

        Debug.Log($"Crowd Spawner Starting: Spawning {totalAgents} pedestrians from {origin} to {destination}");
        StartCoroutine(SpawnRoutine(origin, destination));
    }

    private IEnumerator SpawnRoutine(Vector3 origin, Vector3 destination)
    {
        while (agentsSpawned < totalAgents)
        {
            SpawnAgent(origin, destination);
            agentsSpawned++;
            yield return new WaitForSeconds(spawnRate);
        }
    }

    private void SpawnAgent(Vector3 origin, Vector3 destination)
    {
        // 1. Create Pedestrian Capsule
        GameObject agentObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        agentObj.name = $"Pedestrian_{agentsSpawned + 1}";
        agentObj.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        agentObj.transform.SetParent(transform);

        // 2. Add visual material (Cyan crowd)
        MeshRenderer mr = agentObj.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.1f, 0.75f, 1.0f);
        mr.sharedMaterial = mat;

        // 3. Remove physics collider
        Destroy(agentObj.GetComponent<CapsuleCollider>());

        // 4. Add NavMeshAgent and WARP directly to the road NavMesh
        NavMeshAgent nav = agentObj.AddComponent<NavMeshAgent>();
        nav.Warp(origin); // Professional Unity API: Guarantees zero "not close enough" warnings!
        nav.speed = Random.Range(1.2f, 1.6f);
        nav.radius = 0.35f;
        nav.avoidancePriority = Random.Range(20, 80);

        // 5. Add CrowdAgent logic
        CrowdAgent crowdAgent = agentObj.AddComponent<CrowdAgent>();
        crowdAgent.Initialize(agentsSpawned + 1, destination, nav.speed);
    }
}