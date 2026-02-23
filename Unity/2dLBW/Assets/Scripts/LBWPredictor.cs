using UnityEngine;
using Unity.Barracuda;

public class LBWPredictor : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private NNModel modelAsset;
    [SerializeField] private TextAsset scalerParamsJson;
    [SerializeField] private float decisionThreshold = 0.5f;

    [Header("References")]
    [SerializeField] private Stumps stumps;
    [SerializeField] private Pad pad;

    private Model runtimeModel;
    private IWorker worker;
    private ScalerParams scalerParams;

    [System.Serializable]
    private class ScalerParams
    {
        public float[] mean;
        public float[] scale;
        public string[] feature_names;
    }

    private void Start()
    {
        LoadModel();
        LoadScalerParams();
    }

    void LoadModel()
    {
        if (modelAsset == null)
        {
            Debug.LogError("ONNX model not assigned!");
            return;
        }

        runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(
            WorkerFactory.Type.CSharpBurst,
            runtimeModel
        );

        Debug.Log("LBW Model V2 loaded successfully");
    }

    void LoadScalerParams()
    {
        if (scalerParamsJson == null)
        {
            Debug.LogError("Scaler params JSON not assigned!");
            return;
        }

        scalerParams = JsonUtility.FromJson<ScalerParams>(
            scalerParamsJson.text
        );

        Debug.Log($"Scaler params loaded: {scalerParams.mean.Length} features");

        // Verify we have 7 features (without spinMagnitude)
        if (scalerParams.mean.Length != 7)
        {
            Debug.LogWarning($"Expected 7 features, got {scalerParams.mean.Length}");
        }
    }

    // Predicts LBW outcome based on pad impact data.
    // This should be called at the moment the ball hits the pad.
    public LBWDecision PredictLBW(
        Vector2 impactPos,
        Vector2 impactVel,
        float impactAngularVel,
        int spinDirection,
        float distanceToStumps
    )
    {
        // Build feature array matching training data format (7 features, no spinMagnitude)
        float[] features = new float[7]
        {
            impactPos.x,           // 0: impactPosX
            impactPos.y,           // 1: impactPosY
            impactVel.x,           // 2: impactVelX
            impactVel.y,           // 3: impactVelY
            impactAngularVel,      // 4: impactAngularVel
            spinDirection,         // 5: spinDirection (0 or 1)
            distanceToStumps       // 6: distanceToStumps
        };

        Debug.Log($"Raw features: impactPos=({impactPos.x:F3},{impactPos.y:F3}), " +
                  $"impactVel=({impactVel.x:F3},{impactVel.y:F3}), " +
                  $"angularVel={impactAngularVel:F3}, spinDir={spinDirection}");

        // Normalize features
        float[] normalizedFeatures = NormalizeFeatures(features);

        // Create input tensor and run inference
        Tensor inputTensor = new Tensor(1, 7, normalizedFeatures);
        worker.Execute(inputTensor);
        Tensor outputTensor = worker.PeekOutput();

        float probability = outputTensor[0];

        // Clean up tensors
        inputTensor.Dispose();
        outputTensor.Dispose();

        // Make decision
        bool willHitStumps = probability >= decisionThreshold;

        Debug.Log($"PREDICTION: P(Hit Stumps)={probability:F3}, Decision={willHitStumps}");

        return new LBWDecision
        {
            willHitStumps = willHitStumps,
            probability = probability,
            isOut = willHitStumps
        };
    }

    // Overload that accepts a GameObject ball for easier integration.
    // Extracts all necessary data from the ball and its rigidbody.
    public LBWDecision PredictLBW(
        GameObject ball,
        Rigidbody2D ballRb,
        Bowling.SpinType spinType
    )
    {
        Vector2 impactPos = ball.transform.position;
        Vector2 impactVel = ballRb.linearVelocity;
        float impactAngularVel = ballRb.angularVelocity;
        int spinDirection = (int)spinType;
        float distanceToStumps = Vector2.Distance(impactPos, stumps.transform.position);

        return PredictLBW(
            impactPos,
            impactVel,
            impactAngularVel,
            spinDirection,
            distanceToStumps
        );
    }

    float[] NormalizeFeatures(float[] features)
    {
        if (scalerParams == null || scalerParams.mean == null)
        {
            Debug.LogError("Scaler params not loaded!");
            return features;
        }

        if (features.Length != scalerParams.mean.Length)
        {
            Debug.LogError($"Feature length mismatch! Expected {scalerParams.mean.Length}, got {features.Length}");
            return features;
        }

        float[] normalized = new float[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            // Handle potential divide by zero
            if (Mathf.Approximately(scalerParams.scale[i], 0f))
            {
                normalized[i] = 0f;
                Debug.LogWarning($"Feature {i} has scale=0, setting normalized value to 0");
            }
            else
            {
                normalized[i] = (features[i] - scalerParams.mean[i]) / scalerParams.scale[i];
            }

            // Check for invalid values
            if (float.IsNaN(normalized[i]) || float.IsInfinity(normalized[i]))
            {
                Debug.LogError($"Feature {i} ({GetFeatureName(i)}) normalized to {normalized[i]}! " +
                              $"Raw={features[i]:F6}, Mean={scalerParams.mean[i]:F6}, " +
                              $"Scale={scalerParams.scale[i]:F6}");
                normalized[i] = 0f; // Fallback to 0
            }
        }
        return normalized;
    }

    string GetFeatureName(int index)
    {
        string[] names = {
            "impactPosX", "impactPosY", "impactVelX", "impactVelY",
            "impactAngularVel", "spinDirection", "distanceToStumps"
        };
        return index < names.Length ? names[index] : "unknown";
    }

    private void OnDestroy()
    {
        worker?.Dispose();
    }

    [System.Serializable]
    public struct LBWDecision
    {
        public bool willHitStumps;
        public float probability;
        public bool isOut;
    }
    public float GetDecisionThreshold()
    {
        return decisionThreshold;
    }

   
    // Update decision threshold at runtime
    public void SetDecisionThreshold(float threshold)
    {
        decisionThreshold = Mathf.Clamp01(threshold);
        Debug.Log($"Decision threshold updated to {decisionThreshold:F2}");
    }

    // Check if the model is ready to make predictions
    public bool IsReady()
    {
        return worker != null && scalerParams != null &&
               scalerParams.mean != null && scalerParams.scale != null;
    }
}