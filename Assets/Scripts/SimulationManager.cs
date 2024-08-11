using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class SimulationManager : MonoBehaviour
{
    public ComputeShader simulationShader;
    public int numSimulations = 1000000; // Total number of simulations to run
    public bool ContinuSim = false;
    public int turnsRequired = 231;
    public int ppInitial = 54;
    public Text TextToShow;
    private const int MAX_THREAD_GROUPS = 65535; // Maximum allowed thread groups
    private const int BATCH_SIZE = 5000000; // Size of each batch

    private float _startTime;
    private Stopwatch _stopwatch = new Stopwatch();
    private int _totalGravelersSurvived = 0;
    private int _simDone;

    public Dictionary<int, int> TurnsSurvivedCount = new Dictionary<int, int>();

    void Start()
    {
        _stopwatch.Start();
        StartCoroutine(RunSimulationsCoroutine());
    }

    IEnumerator RunSimulationsCoroutine()
    {
        _startTime = Time.time;
        int remainingSimulations = numSimulations;
        _simDone = 0;

        while (ContinuSim || remainingSimulations > 0)
        {
            int batchSize = Mathf.Min(BATCH_SIZE, remainingSimulations);
            if (ContinuSim ) 
                batchSize = BATCH_SIZE;

            yield return StartCoroutine(ProcessSimulationsBatch(batchSize));
            remainingSimulations -= batchSize;
            _simDone += batchSize;
        }
    }

    IEnumerator ProcessSimulationsBatch(int batchSize)
    {
        // Create and set buffers
        ComputeBuffer resultBuffer = new ComputeBuffer(batchSize, sizeof(int));
        simulationShader.SetBuffer(0, "outputBuffer", resultBuffer);
        simulationShader.SetInt("ppInitial", ppInitial);
        simulationShader.SetInt("turnsRequired", turnsRequired);
        simulationShader.SetInt("randSeed", UnityEngine.Random.Range(1, int.MaxValue));

        // Calculate the number of thread groups needed
        int threadGroupSize = 1024; // Number of threads per group in x dimension
        int numThreadGroups = Mathf.CeilToInt((float)batchSize / threadGroupSize);
        int numBatches = Mathf.CeilToInt((float)numThreadGroups / MAX_THREAD_GROUPS);

        for (int batch = 0; batch < numBatches; batch++)
        {
            int startGroup = batch * MAX_THREAD_GROUPS;
            int endGroup = Mathf.Min(startGroup + MAX_THREAD_GROUPS, numThreadGroups);
            int threadGroupsToDispatch = endGroup - startGroup;

            // Dispatch the compute shader in the current batch
            simulationShader.Dispatch(0, threadGroupsToDispatch, 1, 1);
            yield return null; // Yield to avoid freezing the main thread
        }

        // Request readback to ensure we get the complete data after all dispatches
        long bufferSize = (long)batchSize * sizeof(int); // Total size of buffer data
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(resultBuffer);

        // Wait for readback to complete
        yield return new WaitUntil(() => request.done);

        if (request.hasError)
        {
            Debug.LogError("Error during GPU readback.");
            yield break;
        }

        int[] results = request.GetData<int>().ToArray();

        // Analyze and update results
        UpdateResults(results);

        resultBuffer.Dispose();
    }

    void UpdateResults(int[] results)
    {
        int gravelersSurvivedInBatch = 0;

        foreach (int turns in results)
        {
            // Update the dictionary with the count of simulations surviving a certain number of turns
            if (TurnsSurvivedCount.ContainsKey(turns))
            {
                TurnsSurvivedCount[turns]++;
            }
            else
            {
                TurnsSurvivedCount[turns] = 1;
            }

            if (turns >= turnsRequired) gravelersSurvivedInBatch++;
        }

        _totalGravelersSurvived += gravelersSurvivedInBatch;

        ReportBatchProgress();
    }

    void ReportBatchProgress()
    {
        // Handle cases where no simulations have been done yet
        if (_simDone == 0)
        {
            TextToShow.text = $"Simulations processed: {_simDone}\n" +
                              $"Total gravelers survived: {_totalGravelersSurvived}\n" +
                              $"Minimum turns survived: 0\n" +
                              $"Maximum turns survived: 0\n" +
                              $"Time elapsed: 0 seconds\n" +
                              $"Estimated time to complete 1,000,000,000 simulations: N/A";
            return;
        }

        int minTurns = TurnsSurvivedCount.Count > 0 ? Mathf.Min(TurnsSurvivedCount.Keys.ToArray()) : 0;
        int maxTurns = TurnsSurvivedCount.Count > 0 ? Mathf.Max(TurnsSurvivedCount.Keys.ToArray()) : 0;

        // Calculate elapsed time
        float elapsedTime = _stopwatch.ElapsedMilliseconds / 1000f;

        // Calculate the simulation rate (simulations per second)
        float simulationRate = _simDone / elapsedTime;

        // Calculate the total time required to complete 1,000,000,000 simulations
        float targetSimulations = 1000000000f;
        float estimatedTotalTime = targetSimulations / simulationRate; // in seconds


        // Display results
        TextToShow.text = $"Simulations processed: {_simDone}\n" +
                          $"Total gravelers survived: {_totalGravelersSurvived}\n" +
                          $"Minimum turns survived: {minTurns}\n" +
                          $"Maximum turns survived: {maxTurns}\n" +
                          $"Time elapsed: {elapsedTime:F2} seconds\n" +
                          $"Estimated total time to complete 1,000,000,000 simulations: {estimatedTotalTime:F2} seconds";                          
    }

}
