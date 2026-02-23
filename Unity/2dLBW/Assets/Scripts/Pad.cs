using System.Collections.Generic;
using UnityEngine;

public class Pad : MonoBehaviour
{
    [SerializeField] private SpriteRenderer padRenderer;
    [SerializeField] private Collider2D padCollider;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color ghostColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Movement Settings")]
    [SerializeField] private float minPosition = 2f;
    [SerializeField] private float maxPosition = 8f;
    [SerializeField] private float moveVariation = 0.5f;

    private Vector3 startPosition;
    private bool isGhostMode = false;
    private bool wasHit = false;
    private HashSet<GameObject> ballsHit = new HashSet<GameObject>();

    [Header("References")]
    [SerializeField] private ImprovedDataCollector dataCollector;
    [SerializeField] private LBWPredictor lbwPredictor;

    [Header("Indicator Box")]
    [SerializeField] private SpriteRenderer indicatorBox;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.green;
    private void Awake()
    {
        startPosition = transform.position;
    }

    private void Start()
    {
        if (padRenderer == null)
            padRenderer = GetComponent<SpriteRenderer>();
        if (padCollider == null)
            padCollider = GetComponent<PolygonCollider2D>();
        UpdateGhostMode();
    }

    public void RandomizePosition()
    {
        float randomOffset = Random.Range(-moveVariation, moveVariation);
        float newX = Mathf.Clamp(
            startPosition.x + randomOffset,
            minPosition,
            maxPosition
        );

        transform.position = new Vector3(
            newX,
            transform.position.y,
            transform.position.z
        );

        ballsHit.Clear();
    }

    public void ToggleGhostMode()
    {
        isGhostMode = !isGhostMode;
        UpdateGhostMode();
        Debug.Log($"Pad Ghost Mode: {(isGhostMode ? "ON" : "OFF")}");
    }

    public void SetGhostMode(bool enabled)
    {
        isGhostMode = enabled;
        UpdateGhostMode();
    }

    void UpdateGhostMode()
    {
        if (isGhostMode)
        {
            padCollider.isTrigger = true;
            if (padRenderer != null)
                padRenderer.color = ghostColor;
        }
        else
        {
            padCollider.isTrigger = false;
            if (padRenderer != null)
                padRenderer.color = normalColor;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isGhostMode && collision.gameObject.CompareTag("Ball"))
        {
            HandleBallImpact(collision.gameObject, collision.rigidbody);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isGhostMode && collision.gameObject.CompareTag("Ball"))
        {
            Rigidbody2D rb = collision.GetComponent<Rigidbody2D>();
            HandleBallImpact(collision.gameObject, rb);
        }
    }

    // Calls data collector during training, or decision system during gameplay.
  
    private void HandleBallImpact(GameObject ball, Rigidbody2D ballRb)
    {
        // Prevent duplicate recording
        if (ballsHit.Contains(ball))
            return;

        ballsHit.Add(ball);
        Debug.Log($"PAD IMPACT at {ball.transform.position}");

        // If data collector is active, record this impact
        if (dataCollector != null && dataCollector.IsCollecting())
        {
            dataCollector.OnPadImpact(ball, ballRb);
        }
        else if (lbwPredictor != null && lbwPredictor.IsReady())
        {
            MakeLBWDecision(ball, ballRb);
        }
    }

    private void MakeLBWDecision(GameObject ball, Rigidbody2D ballRb)
    {
  
        Bowling bowling = ball.GetComponent<Bowling>();
        if (bowling == null)
        {
            Debug.LogError("Ball has no Bowling component!");
            return;
        }
        Bowling.SpinType spinType = bowling.GetCurrentSpin();

        // Make prediction
        LBWPredictor.LBWDecision decision = lbwPredictor.PredictLBW(
            ball,
            ballRb,
            spinType
        );

        // Display or handle the decision
        DisplayDecision(decision);
    }
    private void DisplayDecision(LBWPredictor.LBWDecision decision)
    {
        string result = decision.isOut ? "OUT" : "NOT OUT";
        Debug.Log($"=== LBW DECISION ===");
        Debug.Log($"Result: {result}");
        Debug.Log($"Probability: {decision.probability:P1}");
        Debug.Log($"Will Hit Stumps: {decision.willHitStumps}");

        if (indicatorBox != null)
        {
            if (decision.willHitStumps)
            {
                indicatorBox.color = hitColor;
            }
            else
            {
                indicatorBox.color = missColor;

            }

        }
    }


    public bool DidBallHit(GameObject ball)
    {
        return ballsHit.Contains(ball);
    }

    public void ResetPad()
    {
        ballsHit.Clear();
        transform.position = startPosition;
    }

    public bool WasHit()
    {
        return wasHit;
    }


    public bool IsGhostMode()
    {
        return isGhostMode;
    }

    public void ClearHitTracking()
    {
        ballsHit.Clear();
    }
}