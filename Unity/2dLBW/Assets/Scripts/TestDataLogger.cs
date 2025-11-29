using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.InputSystem;

public class TestDataLogger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ball;
    [SerializeField] private Stumps stumps;
    [SerializeField] private Pad pad;
    [SerializeField] private Bowling bowlingScript;

    private List<string> capturedSamples = new List<string>();
    private Rigidbody2D ballRb;
    private float releaseTime;

    void Start()
    {
        if (ball != null)
            ballRb = ball.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Capture current state with L
        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            CaptureSample();
        }

        // Save all captured samples with K
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            SaveSamples();
        }
    }

    void CaptureSample()
    {
        if (ballRb == null || bowlingScript == null) return;

        // Get delivery data
        DeliveryData delivery = bowlingScript.GetCurrentDeliveryData();

        float timeSinceRelease = Time.time - delivery.timestamp;
        Vector2 ballPos = ball.transform.position;
        Vector2 ballVel = ballRb.linearVelocity;
        float ballAngVel = ballRb.angularVelocity;

        float distToStumps = Vector2.Distance(ballPos, stumps.transform.position);
        float distToPad = Vector2.Distance(ballPos, pad.transform.position);

        bool hitPad = pad.DidBallHit(ball);
        bool reachedPad = ballPos.x >= pad.transform.position.x;
        bool willHitStumps = stumps.DidBallHit(ball);

        // Format as Python list
        string sample = $"[{(int)delivery.spinType}, {delivery.speed:F6}, " +
                       $"{delivery.spinAmount:F6}, {timeSinceRelease:F6}, " +
                       $"{ballPos.x:F6}, {ballPos.y:F6}, " +
                       $"{ballVel.x:F6}, {ballVel.y:F6}, {ballAngVel:F6}, " +
                       $"{distToStumps:F6}, {distToPad:F6}, " +
                       $"{(hitPad ? 1 : 0)}, {(reachedPad ? 1 : 0)}]  " +
                       $"# willHitStumps={willHitStumps}";

        capturedSamples.Add(sample);

        Debug.Log($"Captured sample #{capturedSamples.Count}");
        Debug.Log(sample);

        // Also copy to clipboard
        GUIUtility.systemCopyBuffer = sample;
    }

    void SaveSamples()
    {
        if (capturedSamples.Count == 0)
        {
            Debug.LogWarning("No samples to save!");
            return;
        }

        string path = Application.dataPath + "/test_samples.txt";

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("# Test samples for LBW prediction");
            writer.WriteLine("# Format: [spinType, speed, spinAmount, timeSinceRelease,");
            writer.WriteLine("#          ballPosX, ballPosY, ballVelX, ballVelY,");
            writer.WriteLine("#          ballAngularVel, distanceToStumps, distanceToPad,");
            writer.WriteLine("#          hitPad, reachedPad]");
            writer.WriteLine();
            writer.WriteLine("test_samples = [");

            for (int i = 0; i < capturedSamples.Count; i++)
            {
                writer.Write("    " + capturedSamples[i]);
                if (i < capturedSamples.Count - 1)
                    writer.WriteLine(",");
                else
                    writer.WriteLine();
            }

            writer.WriteLine("]");
        }

        Debug.Log($"Saved {capturedSamples.Count} samples to {path}");
        capturedSamples.Clear();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 100));
        GUILayout.Label("Test Data Logger");
        GUILayout.Label($"Captured: {capturedSamples.Count} samples");
        GUILayout.EndArea();
    }
}