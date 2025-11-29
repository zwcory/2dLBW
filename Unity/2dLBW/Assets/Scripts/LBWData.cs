using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class LBWData : MonoBehaviour
{
    private List<TrainingExample> trainingData = new List<TrainingExample>();

    [System.Serializable]
    public class TrainingExample
    {
        // Input features
        public int spinType;
        public float speed;
        public float spinAmount;
        public float timeSinceRelease;
        public float ballPosX;
        public float ballPosY;
        public float ballVelX;
        public float ballVelY;
        public float ballAngularVel;
        public float distanceToStumps;
        public float distanceToPad;
        public int hitPad;
        public int reachedPadPosition;

        // Output label
        public int willHitStumps;
    }

    [System.Serializable]
    public class TrainingDataset
    {
        public List<TrainingExample> examples;
    }

    public void AddSampleDirectly(TrainingExample example)
    {
        trainingData.Add(example);
    }

    public void SaveDataset(string traingOrTest)
    {
        if (trainingData.Count == 0)
        {
            Debug.LogWarning("No training data to save!");
            return;
        }

        TrainingDataset dataset = new TrainingDataset
        {
            examples = trainingData
        };

        string json = JsonUtility.ToJson(dataset, true);
        string path = Application.dataPath + "/LBWTrainingData.json";;
        if (traingOrTest == "Test")
        {
            path = Application.dataPath + "/LBWTestData.json";

        }
        File.WriteAllText(path, json);

        Debug.Log($"Saved {trainingData.Count} training examples to {path}");
    }

    public void SaveAsCSV(string traingOrTest)
    {
        if (trainingData.Count == 0)
        {
            Debug.LogWarning("No training data to save!");
            return;
        }
        string path = Application.dataPath + "/LBWTrainingData.csv";
        if (traingOrTest == "Test")
        {
             path = Application.dataPath + "/LBWTestData.csv";

        }

        using (StreamWriter writer = new StreamWriter(path))
            {
                writer.WriteLine("spinType,speed,spinAmount,timeSinceRelease,ballPosX,ballPosY," +
                               "ballVelX,ballVelY,ballAngularVel,distanceToStumps," +
                               "distanceToPad,hitPad, reachedPad, willHitStumps");

                foreach (var example in trainingData)
                {
                    writer.WriteLine($"{example.spinType},{example.speed:F6}," +
                                   $"{example.spinAmount:F6},{example.timeSinceRelease:F6}," +
                                   $"{example.ballPosX:F6},{example.ballPosY:F6}," +
                                   $"{example.ballVelX:F6},{example.ballVelY:F6}," +
                                   $"{example.ballAngularVel:F6},{example.distanceToStumps:F6}," +
                                   $"{example.distanceToPad:F6},{example.hitPad},{example.reachedPadPosition}," +
                                   $"{example.willHitStumps}");
                }
            }

        Debug.Log($"Saved CSV with {trainingData.Count} samples to {path}");
        PrintDatasetStatistics();
    }

    private void PrintDatasetStatistics()
    {
        int totalSamples = trainingData.Count;
        int hitStumps = trainingData.Count(x => x.willHitStumps == 1);
        int missedStumps = trainingData.Count(x => x.willHitStumps == 0);
        int hitPad = trainingData.Count(x => x.hitPad == 1);
        int missedPad = trainingData.Count(x => x.hitPad == 0);

        Debug.Log("=== DATASET STATISTICS ===");
        Debug.Log($"Total samples: {totalSamples}");
        Debug.Log($"Hit stumps: {hitStumps} ({100f * hitStumps / totalSamples:F1}%)");
        Debug.Log($"Missed stumps: {missedStumps} ({100f * missedStumps / totalSamples:F1}%)");
        Debug.Log($"Hit pad: {hitPad} ({100f * hitPad / totalSamples:F1}%)");
        Debug.Log($"Missed pad: {missedPad} ({100f * missedPad / totalSamples:F1}%)");
    }

    public void ClearData()
    {
        trainingData.Clear();
        Debug.Log("Cleared training data");
    }

    public int GetDatasetSize()
    {
        return trainingData.Count;
    }
}