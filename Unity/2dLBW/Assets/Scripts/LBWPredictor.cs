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

        Debug.Log(" LBW Model loaded successfully");
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
    }

    public LBWDecision PredictLBW(
        Bowling.SpinType spinType,
        float speed,
        float spinAmount,
        float timeSinceRelease,
        Vector2 ballPos,
        Vector2 ballVel,
        float ballAngularVel,
        float distanceToStumps,
        float distanceToPad,
        bool hitPad
    )
    {
        float[] features = new float[13]
        {
            (int)spinType,           // 0: spinType
            speed,                   // 1: speed
            spinAmount,              // 2: spinAmount
            timeSinceRelease,        // 3: timeSinceRelease
            ballPos.x,               // 4: ballPosX
            ballPos.y,               // 5: ballPosY
            ballVel.x,               // 6: ballVelX
            ballVel.y,               // 7: ballVelY
            ballAngularVel,          // 8: ballAngularVel
            distanceToStumps,        // 9: distanceToStumps
            distanceToPad,           // 10: distanceToPad
            hitPad ? 1f : 0f,        // 11: hitPad
            1f                       // 12: reachedPad (always 1 at decision time)
        };



        float[] normalizedFeatures = NormalizeFeatures(features);

     
        Tensor inputTensor = new Tensor(1, 13, normalizedFeatures);
        worker.Execute(inputTensor);
        Tensor outputTensor = worker.PeekOutput();

        float probability = outputTensor[0];


        inputTensor.Dispose();
        outputTensor.Dispose();

        // Make decision
        bool willHitStumps = probability >= decisionThreshold;

        return new LBWDecision
        {
            willHitStumps = willHitStumps,
            probability = probability,
            isOut = willHitStumps
        };
    }

   
    float[] NormalizeFeatures(float[] features)
    {
        if (scalerParams == null || scalerParams.mean == null)
        {
            Debug.LogError("Scaler params not loaded!");
            return features;
        }

        float[] normalized = new float[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            normalized[i] = (features[i] - scalerParams.mean[i]) /
                           scalerParams.scale[i];

            if (float.IsNaN(normalized[i]) || float.IsInfinity(normalized[i]))
            {
                Debug.LogError($"Feature {i} normalized to {normalized[i]}! " +
                              $"Raw={features[i]}, Mean={scalerParams.mean[i]}, " +
                              $"Scale={scalerParams.scale[i]}");
            }
        }
        return normalized;
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


    [ContextMenu("Test Model with Known Data")]
    public void TestModelWithKnownData()
    {
        Debug.Log("=== TESTING MODEL WITH KNOWN DATA ===");

        // Check if everything is loaded
        if (worker == null)
        {
            Debug.LogError("Worker is null! Model not loaded.");
            return;
        }

        if (scalerParams == null)
        {
            Debug.LogError("Scaler params is null! Not loaded.");
            return;
        }

        if (scalerParams.mean == null || scalerParams.scale == null)
        {
            Debug.LogError("Scaler mean/scale arrays are null!");
            return;
        }

        Debug.Log($"Scaler has {scalerParams.mean.Length} features");

        // Close miss
        float[] testFeatures = new float[13]
        {
         0f,
         0.831415f,
         10f,
         0.850223f, 
         5.112407f,
         -0.77087f,
         13.58121f,
         3.864911f,   
         99.5542f,      
         3.647906f,
         2.117695f, 
        0, 0f
        };

        Debug.Log($"Test features: [{string.Join(", ", testFeatures)}]");

        float[] normalized = NormalizeFeatures(testFeatures);

        Debug.Log($"Normalized: [{string.Join(", ", normalized)}]");

        Tensor inputTensor = new Tensor(1, 13, normalized);
        worker.Execute(inputTensor);
        Tensor outputTensor = worker.PeekOutput();
        float probability = outputTensor[0];


        inputTensor.Dispose();
        outputTensor.Dispose();
    }
}


