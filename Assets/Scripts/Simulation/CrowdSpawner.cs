using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CrowdSpawner : MonoBehaviour
{
    [Header("Default Simulation Settings")]
    [SerializeField] private int totalAgents = 25;
    [SerializeField] private float spawnRate = 0.5f;

    private int agentsSpawned = 0;
    private List<GameObject> activeAgents = new List<GameObject>();
    private Coroutine spawnCoroutine;

    // Called DIRECTLY by OvertureImporter or UncertaintyEngine
    public void StartSimulation(Vector3 origin, Vector3 destination)
    {
        StartSimulation(origin, destination, totalAgents, spawnRate, new Vector2(1.2f, 1.6f));
    }

    // Overload for UncertaintyEngine to inject Monte Carlo sampled parameters
    public void StartSimulation(Vector3 origin, Vector3 destination, int count, float interval, Vector2 speedRange, Action onAllSpawned = null)
    {
        ClearAgents();

        if (NavMesh.SamplePosition(origin, out NavMeshHit hit1, 20f, NavMesh.AllAreas))
        {
            origin = hit1.position;
        }
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit2, 20f, NavMesh.AllAreas))
        {
            destination = hit2.position;
        }

        spawnCoroutine = StartCoroutine(SpawnRoutine(origin, destination, count, interval, speedRange, onAllSpawned));
    }

    private IEnumerator SpawnRoutine(Vector3 origin, Vector3 destination, int count, float interval, Vector2 speedRange, Action onAllSpawned)
    {
        agentsSpawned = 0;
        while (agentsSpawned < count)
        {
            SpawnAgent(origin, destination, speedRange);
            agentsSpawned++;
            yield return new WaitForSeconds(interval);
        }

        onAllSpawned?.Invoke();
    }

    private void SpawnAgent(Vector3 origin, Vector3 destination, Vector2 speedRange)
    {
        GameObject agentObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        agentObj.name = $"Pedestrian_{agentsSpawned + 1}";
        agentObj.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        agentObj.transform.SetParent(transform);

        MeshRenderer mr = agentObj.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.1f, 0.75f, 1.0f);
        mr.sharedMaterial = mat;

        Destroy(agentObj.GetComponent<CapsuleCollider>());

        NavMeshAgent nav = agentObj.AddComponent<NavMeshAgent>();
        nav.Warp(origin);
        nav.speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);
        nav.radius = 0.35f;
        nav.avoidancePriority = UnityEngine.Random.Range(20, 80);

        CrowdAgent crowdAgent = agentObj.AddComponent<CrowdAgent>();
        crowdAgent.Initialize(agentsSpawned + 1, destination, nav.speed);

        activeAgents.Add(agentObj);
    }

    public void ClearAgents()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        for (int i = activeAgents.Count - 1; i >= 0; i--)
        {
            if (activeAgents[i] != null)
            {
                Destroy(activeAgents[i]);
            }
        }
        activeAgents.Clear();
        agentsSpawned = 0;
    }

    public int GetActiveAgentCount()
    {
        activeAgents.RemoveAll(a => a == null);
        return activeAgents.Count;
    }
}