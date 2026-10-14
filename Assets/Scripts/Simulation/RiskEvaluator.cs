using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RiskEvaluator : MonoBehaviour
{
    [Header("Spatial Grid Configuration")]
    [Tooltip("Grid resolution in meters. Standard pedestrian safety is 1.0m x 1.0m")]
    [SerializeField] private float cellSize = 1.0f;
    [Tooltip("Sampling frequency in seconds (0.2s = 5 samples/sec, optimal for laptop CPU)")]
    [SerializeField] private float sampleInterval = 0.2f;

    [Header("Crowd Density Thresholds (Fruin LOS Model)")]
    [Tooltip("Threshold where flow becomes severely restricted")]
    [SerializeField] private float warningDensity = 2.5f; // ped/m^2
    [Tooltip("Critical crowd crush hazard threshold")]
    [SerializeField] private float criticalDensity = 4.0f; // ped/m^2

    [Header("Debug Visualization")]
    [SerializeField] private bool showGridGizmos = true;

    // Spatial binning storage: Cell coordinate -> Agent count in that cell
    private readonly Dictionary<Vector2Int, int> densityGrid = new Dictionary<Vector2Int, int>();

    // Trial Metrics
    public float PeakDensity { get; private set; } = 0f;
    public float BottleneckDurationSeconds { get; private set; } = 0f;
    public bool CriticalHazardDetected { get; private set; } = false;
    public Vector3 CriticalHazardLocation { get; private set; } = Vector3.zero;

    private Coroutine evaluationCoroutine;
    private bool isEvaluating = false;

    private void Start()
    {
        StartEvaluation();
    }

    public void StartEvaluation()
    {
        if (isEvaluating) return;
        isEvaluating = true;
        evaluationCoroutine = StartCoroutine(EvaluationRoutine());
    }

    public void StopEvaluation()
    {
        if (!isEvaluating) return;
        isEvaluating = false;
        if (evaluationCoroutine != null)
        {
            StopCoroutine(evaluationCoroutine);
            evaluationCoroutine = null;
        }
    }

    /// <summary>
    /// Resets all metrics for the start of a fresh Monte Carlo trial.
    /// </summary>
    public void ResetTrialMetrics()
    {
        densityGrid.Clear();
        PeakDensity = 0f;
        BottleneckDurationSeconds = 0f;
        CriticalHazardDetected = false;
        CriticalHazardLocation = Vector3.zero;
    }

    private IEnumerator EvaluationRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(sampleInterval);

        while (isEvaluating)
        {
            EvaluateCurrentFrame();
            yield return wait;
        }
    }

    private void EvaluateCurrentFrame()
    {
        // 1. Gather all active crowd agents
        CrowdAgent[] agents = FindObjectsByType<CrowdAgent>(FindObjectsSortMode.None);
        if (agents.Length == 0) return;

        // 2. Clear current spatial bins
        densityGrid.Clear();

        // 3. Bin agent positions into 1m x 1m spatial cells
        for (int i = 0; i < agents.Length; i++)
        {
            Vector3 pos = agents[i].transform.position;
            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt(pos.x / cellSize),
                Mathf.FloorToInt(pos.z / cellSize)
            );

            if (densityGrid.ContainsKey(cell))
            {
                densityGrid[cell]++;
            }
            else
            {
                densityGrid[cell] = 1;
            }
        }

        // 4. Analyze densities and evaluate hazard thresholds
        bool frameHasCritical = false;

        foreach (var kvp in densityGrid)
        {
            // Calculate ped/m^2 (area = cellSize * cellSize)
            float cellArea = cellSize * cellSize;
            float currentDensity = kvp.Value / cellArea;

            if (currentDensity > PeakDensity)
            {
                PeakDensity = currentDensity;
            }

            if (currentDensity >= criticalDensity)
            {
                frameHasCritical = true;
                CriticalHazardDetected = true;
                // Store world coordinate center of the critical cell
                CriticalHazardLocation = new Vector3(
                    (kvp.Key.x + 0.5f) * cellSize,
                    0f,
                    (kvp.Key.y + 0.5f) * cellSize
                );
            }
        }

        if (frameHasCritical)
        {
            BottleneckDurationSeconds += sampleInterval;
        }
    }

    // --- CSV Safety Report Exporter ---
    public void ExportSafetyReportCSV(List<UncertaintyEngine.TrialResult> trialResults, string fileName = "VenueSafe_Assessment_Report.csv")
    {
        string filePath = Path.Combine(Application.dataPath, fileName);
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            writer.WriteLine("TrialIndex,Scenario,AgentCount,CompletionTime(s),PeakDensity(ped/m2),BottleneckFlag");
            foreach (var t in trialResults)
            {
                writer.WriteLine($"{t.trialIndex},{t.scenario},{t.agentCount},{t.completionTime:F2},{t.peakDensityObserved:F2},{t.experiencedBottleneck}");
            }
        }
        Debug.Log($"[RiskEvaluator] Report successfully exported to: {filePath}");
    }

    // --- Scene View Gizmo Heatmap ---
    private void OnDrawGizmos()
    {
        if (!showGridGizmos || densityGrid.Count == 0) return;

        foreach (var kvp in densityGrid)
        {
            float density = kvp.Value / (cellSize * cellSize);

            if (density < 1.0f) continue; // Skip single isolated pedestrians to keep scene view clean

            Vector3 center = new Vector3(
                (kvp.Key.x + 0.5f) * cellSize,
                0.1f,
                (kvp.Key.y + 0.5f) * cellSize
            );

            // Color coding based on risk
            if (density >= criticalDensity)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.75f); // Red: Crush Risk
            }
            else if (density >= warningDensity)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f); // Orange: High Congestion
            }
            else
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f); // Yellow: Moderate Flow
            }

            Gizmos.DrawCube(center, new Vector3(cellSize * 0.95f, 0.15f, cellSize * 0.95f));
        }
    }
}