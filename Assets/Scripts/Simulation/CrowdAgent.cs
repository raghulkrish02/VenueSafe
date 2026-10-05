using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class CrowdAgent : MonoBehaviour
{
    public int AgentId { get; private set; }
    public float PreferredSpeed { get; private set; }
    public float SpawnTime { get; private set; }
    public float TravelTime => Time.time - SpawnTime;
    public bool HasReachedDestination { get; private set; } = false;

    private NavMeshAgent navAgent;
    private Vector3 destination;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    public void Initialize(int id, Vector3 targetDestination, float speed = 1.3f)
    {
        AgentId = id;
        destination = targetDestination;
        PreferredSpeed = speed;
        SpawnTime = Time.time;
        HasReachedDestination = false;

        // Human walking speed variation (1.1m/s to 1.5m/s normal distribution)
        navAgent.speed = PreferredSpeed;
        navAgent.avoidancePriority = Random.Range(30, 60); // Random priority avoids gridlock
        navAgent.radius = 0.35f; // Personal space radius (body radius ~0.3m to 0.4m)
        navAgent.height = 1.8f;

        // Only set destination if agent successfully snapped to the NavMesh
        if (navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(destination);
        }
    }

    private void Update()
    {
        // SAFETY GUARD: If agent is not on a valid NavMesh yet, do nothing (prevents errors)
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
        {
            return;
        }

        if (HasReachedDestination) return;

        // Check if agent reached destination (within 2 meters)
        if (!navAgent.pathPending && navAgent.remainingDistance <= 2.0f)
        {
            HasReachedDestination = true;
            OnDestinationReached();
        }
    }

    private void OnDestinationReached()
    {
        Debug.Log($"Agent {AgentId} reached destination! Travel Time: {TravelTime:F1}s | Speed: {PreferredSpeed:F2}m/s");
        // Deactivate or destroy upon exit
        Destroy(gameObject, 0.5f);
    }
}