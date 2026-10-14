using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UncertaintyEngine : MonoBehaviour
{
    public enum ScenarioMode
    {
        Baseline,          // Standard unmitigated flow
        MeteredArrival,    // AI Intervention: Flow pacing (controlled batching)
        ExpandedGate       // AI Intervention: Simulated spatial widening
    }

    [Header("Dependencies")]
    [SerializeField] private CrowdSpawner crowdSpawner;
    [SerializeField] private RiskEvaluator riskEvaluator;

    [Header("Monte Carlo Configuration")]
    [Tooltip("Number of stochastic trials to run (recommended 5-15 for laptop GPU)")]
    [SerializeField] private int monteCarloTrials = 10;
    [Tooltip("Maximum duration in seconds before forcing a trial to wrap up")]
    [SerializeField] private float trialTimeoutSeconds = 35f;

    [Header("Pedestrian Uncertainty Parameters")]
    [SerializeField] private int baselineAgentCount = 30;
    [SerializeField] private int countVariance = 8;
    [SerializeField] private float meanWalkingSpeed = 1.35f;
    [SerializeField] private float speedStdDev = 0.25f;
    [SerializeField] private float meanArrivalRateLambda = 2.0f;

    [Header("GIS Spatial Tolerance")]
    [Tooltip("Simulated GPS/Overture boundary offset uncertainty in meters")]
    [SerializeField] private float gisSpatialJitter = 0.75f;

    [Header("Counterfactual Scenario Selection")]
    public ScenarioMode activeScenario = ScenarioMode.Baseline;

    [System.Serializable]
    public struct TrialResult
    {
        public int trialIndex;
        public ScenarioMode scenario;
        public int agentCount;
        public float completionTime;
        public float peakDensityObserved;
        public bool experiencedBottleneck;
    }

    public List<TrialResult> completedTrials = new List<TrialResult>();
    public bool IsRunning { get; private set; } = false;
    public int CurrentTrialIndex { get; private set; } = 0;

    private Vector3 baseOrigin;
    private Vector3 baseDestination;

    private void Awake()
    {
        if (crowdSpawner == null) crowdSpawner = FindFirstObjectByType<CrowdSpawner>();
        if (riskEvaluator == null) riskEvaluator = FindFirstObjectByType<RiskEvaluator>();
    }

    public void RunMonteCarloAssessment(Vector3 origin, Vector3 destination)
    {
        if (IsRunning)
        {
            Debug.LogWarning("[UncertaintyEngine] Simulation already running!");
            return;
        }

        baseOrigin = origin;
        baseDestination = destination;
        completedTrials.Clear();

        StartCoroutine(MonteCarloLoop());
    }

    private IEnumerator MonteCarloLoop()
    {
        IsRunning = true;
        Debug.Log($"[UncertaintyEngine] Starting Monte Carlo Assessment ({monteCarloTrials} trials) - Scenario: {activeScenario}");

        for (int i = 0; i < monteCarloTrials; i++)
        {
            CurrentTrialIndex = i + 1;
            Debug.Log($"--- Running Trial {CurrentTrialIndex}/{monteCarloTrials} ---");

            // Reset density & risk metrics for this trial
            if (riskEvaluator != null)
            {
                riskEvaluator.ResetTrialMetrics();
            }

            // 1. Stochastic Parameter Sampling
            int trialAgentCount = SampleSurgeCount(baselineAgentCount, countVariance, activeScenario);
            Vector2 trialSpeedRange = SampleSpeedRange(meanWalkingSpeed, speedStdDev);
            float trialSpawnInterval = SampleInterarrivalTime(meanArrivalRateLambda, activeScenario);

            // 2. Spatial Geodata Perturbation
            Vector3 perturbedOrigin = ApplySpatialJitter(baseOrigin, gisSpatialJitter);
            Vector3 perturbedDestination = ApplySpatialJitter(baseDestination, gisSpatialJitter);

            // 3. Launch Spawner with sampled parameters
            float trialStartTime = Time.time;
            bool spawnFinished = false;

            crowdSpawner.StartSimulation(
                perturbedOrigin,
                perturbedDestination,
                trialAgentCount,
                trialSpawnInterval,
                trialSpeedRange,
                () => spawnFinished = true
            );

            // 4. Wait until pedestrians reach destination or timeout
            while (Time.time - trialStartTime < trialTimeoutSeconds)
            {
                if (spawnFinished && crowdSpawner.GetActiveAgentCount() == 0)
                {
                    break;
                }
                yield return new WaitForSeconds(0.5f);
            }

            float duration = Time.time - trialStartTime;
            float observedPeak = (riskEvaluator != null) ? riskEvaluator.PeakDensity : 0f;
            bool hadBottleneck = (riskEvaluator != null && riskEvaluator.CriticalHazardDetected) || (duration >= trialTimeoutSeconds - 1f);

            // 5. Record Trial Metrics
            TrialResult result = new TrialResult
            {
                trialIndex = CurrentTrialIndex,
                scenario = activeScenario,
                agentCount = trialAgentCount,
                completionTime = duration,
                peakDensityObserved = observedPeak,
                experiencedBottleneck = hadBottleneck
            };
            completedTrials.Add(result);

            // Clear pedestrians before next trial
            crowdSpawner.ClearAgents();
            yield return new WaitForSeconds(0.5f);
        }

        IsRunning = false;
        PrintExecutiveSummary();

        // Export CSV report to project directory
        if (riskEvaluator != null)
        {
            riskEvaluator.ExportSafetyReportCSV(completedTrials);
        }
    }

    private Vector2 SampleSpeedRange(float mean, float stdDev)
    {
        float u1 = Mathf.Max(0.0001f, Random.value);
        float u2 = Mathf.Max(0.0001f, Random.value);
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);

        float minSpeed = Mathf.Clamp(mean + (randStdNormal * stdDev) - 0.2f, 0.8f, 1.8f);
        float maxSpeed = Mathf.Clamp(minSpeed + Random.Range(0.2f, 0.4f), minSpeed, 2.2f);
        return new Vector2(minSpeed, maxSpeed);
    }

    private float SampleInterarrivalTime(float lambda, ScenarioMode scenario)
    {
        if (scenario == ScenarioMode.MeteredArrival) lambda *= 0.6f;

        float u = Mathf.Max(0.0001f, Random.value);
        float interval = -Mathf.Log(u) / lambda;
        return Mathf.Clamp(interval, 0.15f, 1.5f);
    }

    private int SampleSurgeCount(int baseline, int variance, ScenarioMode scenario)
    {
        int sampled = baseline + Random.Range(-variance, variance + 1);
        return Mathf.Max(5, sampled);
    }

    private Vector3 ApplySpatialJitter(Vector3 point, float jitterRadius)
    {
        Vector2 circle = Random.insideUnitCircle * jitterRadius;
        return new Vector3(point.x + circle.x, point.y, point.z + circle.y);
    }

    private void PrintExecutiveSummary()
    {
        float totalTime = 0f;
        float maxPeak = 0f;
        int bottlenecks = 0;

        foreach (var t in completedTrials)
        {
            totalTime += t.completionTime;
            if (t.peakDensityObserved > maxPeak) maxPeak = t.peakDensityObserved;
            if (t.experiencedBottleneck) bottlenecks++;
        }

        float avgDuration = totalTime / completedTrials.Count;
        float bottleneckProb = ((float)bottlenecks / completedTrials.Count) * 100f;

        Debug.Log("==================================================");
        Debug.Log($"[VenueSafe Executive Safety Assessment]");
        Debug.Log($"Active Scenario: {activeScenario}");
        Debug.Log($"Total Trials Run: {completedTrials.Count}");
        Debug.Log($"Mean Evacuation Duration: {avgDuration:F2} seconds");
        Debug.Log($"Max Peak Density Observed: {maxPeak:F2} ped/m² (Crush threshold: 4.0 ped/m²)");
        Debug.Log($"Bottleneck Probability (Risk): {bottleneckProb:F1}%");
        Debug.Log("==================================================");
    }
}