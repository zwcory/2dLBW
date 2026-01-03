using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public class LBWDataV2 : MonoBehaviour
{
    private List<DeliveryRecord> deliveryData = new List<DeliveryRecord>();

    [System.Serializable]
    public class DeliveryRecord
    {
        // Impact features (captured at moment ball hits pad)
        public float impactPosX;
        public float impactPosY;
        public float impactVelX;
        public float impactVelY;
        public float impactAngularVel;
        public int spinDirection;      // 0 = backspin, 1 = topspin
        public float spinMagnitude;
        public float distanceToStumps;

        // Output label
        public int willHitStumps;      // 0 = no, 1 = yes
    }

    [System.Serializable]
    public class DeliveryDataset
    {
        public List<DeliveryRecord> deliveries;
    }

    public void AddDelivery(DeliveryRecord record)
    {
        deliveryData.Add(record);
    }

    public void SaveDataset(string trainingOrTest)
    {
        if (deliveryData.Count == 0)
        {
            Debug.LogWarning("No delivery data to save!");
            return;
        }

        DeliveryDataset dataset = new DeliveryDataset
        {
            deliveries = deliveryData
        };

        string json = JsonUtility.ToJson(dataset, true);
        string filename = trainingOrTest == "Test" ?
            "/LBWTestData_V2.json" : "/LBWTrainingData_V2.json";
        string path = Application.dataPath + filename;

        File.WriteAllText(path, json);

        Debug.Log($"Saved {deliveryData.Count} deliveries to {path}");
    }

    public void SaveAsCSV(string trainingOrTest)
    {
        if (deliveryData.Count == 0)
        {
            Debug.LogWarning("No delivery data to save!");
            return;
        }

        string filename = trainingOrTest == "Test" ?
            "/LBWTestData_V2.csv" : "/LBWTrainingData_V2.csv";
        string path = Application.dataPath + filename;

        using (StreamWriter writer = new StreamWriter(path))
        {
            // Header
            writer.WriteLine("impactPosX,impactPosY,impactVelX,impactVelY," +
                           "impactAngularVel,spinDirection,spinMagnitude," +
                           "distanceToStumps,willHitStumps");

            // Data rows
            foreach (var record in deliveryData)
            {
                writer.WriteLine($"{record.impactPosX:F6},{record.impactPosY:F6}," +
                               $"{record.impactVelX:F6},{record.impactVelY:F6}," +
                               $"{record.impactAngularVel:F6},{record.spinDirection}," +
                               $"{record.spinMagnitude:F6},{record.distanceToStumps:F6}," +
                               $"{record.willHitStumps}");
            }
        }

        Debug.Log($"Saved CSV with {deliveryData.Count} deliveries to {path}");
        PrintDatasetStatistics();
    }

    private void PrintDatasetStatistics()
    {
        int totalDeliveries = deliveryData.Count;
        int hitStumps = deliveryData.Count(x => x.willHitStumps == 1);
        int missedStumps = deliveryData.Count(x => x.willHitStumps == 0);
        int topspin = deliveryData.Count(x => x.spinDirection == 1);
        int backspin = deliveryData.Count(x => x.spinDirection == 0);

        Debug.Log("=== DATASET STATISTICS V2 ===");
        Debug.Log($"Total deliveries: {totalDeliveries}");
        Debug.Log($"Hit stumps: {hitStumps} ({100f * hitStumps / totalDeliveries:F1}%)");
        Debug.Log($"Missed stumps: {missedStumps} ({100f * missedStumps / totalDeliveries:F1}%)");
        Debug.Log($"Topspin: {topspin} ({100f * topspin / totalDeliveries:F1}%)");
        Debug.Log($"Backspin: {backspin} ({100f * backspin / totalDeliveries:F1}%)");

        float avgDistanceToStumps = deliveryData.Average(x => x.distanceToStumps);
        Debug.Log($"Average distance to stumps at impact: {avgDistanceToStumps:F2}");
    }

    public void ClearData()
    {
        deliveryData.Clear();
        Debug.Log("Cleared delivery data");
    }

    public int GetDatasetSize()
    {
        return deliveryData.Count;
    }
}