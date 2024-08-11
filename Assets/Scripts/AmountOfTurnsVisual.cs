using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class AmountOfTurnsVisual : MonoBehaviour
{
    public GameObject BarVisualPrefab; // Prefab for the bar visual
    public SimulationManager Manager;  // Reference to SimulationManager
    public float MaxYScale = 41f;      // Maximum scale for the highest bar
    public float BarWidth = 1;
    public Text MaxNumberText;

    private void Start()
    {
        StartCoroutine(UpdateBarVisuals());
    }

    private IEnumerator UpdateBarVisuals()
    {
        while (true)
        {
            // Clear existing bars
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // Fetch and sort the data from SimulationManager
            Dictionary<int, int> turnsSurvivedCount = Manager.TurnsSurvivedCount;

            if (turnsSurvivedCount.Count == 0)
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }    
            int maxCount = 0;
            foreach (var count in turnsSurvivedCount.Values)
            {
                if (count > maxCount)
                {
                    maxCount = count;
                }
            }

            int maxTurns = Mathf.Max(turnsSurvivedCount.Keys.ToArray());
            int maxTurnCount = turnsSurvivedCount[maxTurns];

            // Sort keys to maintain order
            List<int> sortedKeys = new List<int>(turnsSurvivedCount.Keys);
            sortedKeys.Sort();

            // Calculate the spacing between bars
            float barWidth = BarWidth;
            float spacing = 5f; // Adjust for desired spacing
            float totalWidth = sortedKeys.Count * (barWidth + spacing);
            float startX = -totalWidth / 2;

            // Create bars
            foreach (int turns in sortedKeys)
            {
                int count = turnsSurvivedCount[turns];

                // Instantiate a new bar
                GameObject bar = Instantiate(BarVisualPrefab, transform);

                // Scale the bar (child index 0)
                Transform barTransform = bar.transform.GetChild(0); // Bar visual
                barTransform.localScale = new Vector3(barTransform.localScale.x, Mathf.Lerp(0, MaxYScale, (float)count / maxCount), 1f);

                // Set the position of the bar
                bar.transform.localPosition = new Vector3(startX + (barWidth + spacing) * sortedKeys.IndexOf(turns), bar.transform.localPosition.y, bar.transform.localPosition.z);

                // Update the text (child index 1)
                Transform textTransform = bar.transform.GetChild(1); // Legacy text
                Text textMesh = textTransform.GetComponent<Text>();
                if (textMesh != null)
                {
                    textMesh.text = $"{turns}\n{count}";
                }
            }

            if (MaxNumberText != null)
            {
                MaxNumberText.text = $"Max turns: {maxTurns}\nAchieved: {maxTurnCount}";
            }

            yield return new WaitForSeconds(0.2f);
        }
    }
}
