using UnityEngine;

public class BallTracker : MonoBehaviour
{
    private LBWPredictor lbwPredictor;
    private Pad pad;
    private Stumps stumps;

    private Rigidbody2D rb;
    private float spawnTime;
    private bool decisionMade = false;

    private Bowling.SpinType spinType;
    private float speed;
    private float spinAmount;

    [SerializeField] private SpriteRenderer indicatorBox;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.green;

    public void Initialize(
        Bowling.SpinType spin,
        float spd,
        float spinAmt
    )
    {
        rb = GetComponent<Rigidbody2D>();
        spawnTime = Time.time;
        spinType = spin;
        speed = spd;
        spinAmount = spinAmt;
        indicatorBox = GameObject.FindGameObjectWithTag("Indicator").GetComponent<SpriteRenderer>();


        lbwPredictor = GameObject.FindGameObjectWithTag("Predictor").GetComponent<LBWPredictor>();

        pad = FindFirstObjectByType<Pad>();
        stumps = FindFirstObjectByType<Stumps>();

        if (lbwPredictor == null)
            Debug.LogError("LBWPredictor not found in scene!");
        if (pad == null)
            Debug.LogError("Pad not found in scene!");
        if (stumps == null)
            Debug.LogError("Stumps not found in scene!");
    }

    private void Update()
    {
        if (lbwPredictor == null || pad == null || stumps == null)
            return;

  
        if (decisionMade)
            return;

        float ballX = transform.position.x;
        float padX = pad.transform.position.x;


        // make a decision before the pad is hit
        // this has been the simplest approach, ideally check impact at pad
        if (((ballX + 1) >= padX))
        {
            MakeLBWDecision();
            decisionMade = true;
        }
    }

    void MakeLBWDecision()
    {
        bool hitPad = pad.WasHit();



        // Calculate features at moment of decision
        float timeSinceRelease = Time.time - spawnTime;
        Vector2 ballPos = transform.position;
        Vector2 ballVel = rb.linearVelocity;
        float ballAngularVel = rb.angularVelocity;
        float distanceToStumps = Vector2.Distance(
            transform.position,
            stumps.transform.position
        );
        float distanceToPad = Vector2.Distance(
            transform.position,
            pad.transform.position
        );


        LBWPredictor.LBWDecision decision = lbwPredictor.PredictLBW(
            spinType,
            speed,
            spinAmount,
            timeSinceRelease,
            ballPos,
            ballVel,
            ballAngularVel,
            distanceToStumps,
            distanceToPad,
            hitPad
        );


        DisplayLBWResult(decision);
    }

    void DisplayLBWResult(LBWPredictor.LBWDecision decision)
    {
        if (indicatorBox != null)
        {
            if (decision.willHitStumps) 
            { 
                indicatorBox.color = hitColor;
            } else
            {
                indicatorBox.color = missColor;

            }

        }


        string result = decision.isOut ? "OUT!" : "NOT OUT";

        Debug.Log($"=== LBW DECISION===");
        Debug.Log($"Result: {result}");
        Debug.Log($"Probability: {decision.probability:P1}");
        Debug.Log($"Would hit stumps: {decision.willHitStumps}");
    }
}