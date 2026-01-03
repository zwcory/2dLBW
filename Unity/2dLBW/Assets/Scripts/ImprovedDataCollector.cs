using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImprovedDataCollector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Stumps stumps;
    [SerializeField] private Pad pad;
    [SerializeField] private LBWDataV2 dataCollector;
    [SerializeField] private Bowling bowlingScript;

    [Header("Collection Settings")]
    [SerializeField] private int targetDeliveries = 10000;
    [SerializeField] private float spawnInterval = 0.3f;
    [SerializeField] private int ballsPerPadPosition = 20;

    [Header("Auto Bowl Parameters")]
    [SerializeField] private float autoBowlAngleMin = -22.5f;
    [SerializeField] private float autoBowlAngleMax = -5f;
    [SerializeField] private float forceMultiplier = 10f;
    [SerializeField] private float fixedDistance = 2f;

    [Header("Spin Parameters")]
    [SerializeField] private float topSpinSpeedMultiplier = 0.85f;
    [SerializeField] private float backSpinSpeedMultiplier = 1.0f;
    [SerializeField] private float speedVariation = 0.1f;
    [SerializeField] private float topSpinTorque = 10f;
    [SerializeField] private float backSpinTorque = -10f;


    private Vector2 releasePoint;
    private Dictionary<GameObject, BallInstance> ballLookup = new Dictionary<GameObject, BallInstance>();
    private List<BallInstance> activeBalls = new List<BallInstance>();
    private bool isCollecting = false;
    private int deliveriesRecorded = 0;
    private int ballsInCurrentSet = 0;
    private Vector2 currentPadPosition;
    private string trainingOrTestData;

    private class BallInstance
    {
        public GameObject ballObject;
        public Rigidbody2D rb;
        public Bowling.SpinType spinType;
        public float spinMagnitude;
        public bool hasRecordedImpact;
        public bool isFinalized;
        public PadImpactData impactData;
    }

    private class PadImpactData
    {
        public Vector2 position;
        public Vector2 velocity;
        public float angularVelocity;
    }

    void Start()
    {
        if (bowlingScript != null)
        {
            releasePoint = bowlingScript.transform.position;
        }
        else
        {
            releasePoint = new Vector2(-7, 1);
        }
    }

    void Update()
    {
        if (isCollecting)
        {
            CheckForStumpsOutcome();
            CleanupFinishedBalls();
        }
    }

    public bool IsCollecting()
    {
        return isCollecting;
    }

    // Called by Pad when a ball impacts it during data collection.
    // Records impact data immediately at the exact moment of contact.
    public void OnPadImpact(GameObject ball, Rigidbody2D ballRb)
    {
        if (!isCollecting || !ballLookup.ContainsKey(ball))
            return;

        BallInstance ballInstance = ballLookup[ball];

        // Only record once per ball
        if (ballInstance.hasRecordedImpact)
            return;

        ballInstance.hasRecordedImpact = true;

        // Capture impact data at this exact moment
        ballInstance.impactData = new PadImpactData
        {
            position = ball.transform.position,
            velocity = ballRb.linearVelocity,
            angularVelocity = ballRb.angularVelocity
        };

        Debug.Log($"IMPACT RECORDED: Pos={ballInstance.impactData.position}, Vel={ballInstance.impactData.velocity.magnitude:F2}");
    }

    public void StartFastCollection(string trainingOrTest)
    {
        if (isCollecting) return;
        trainingOrTestData = trainingOrTest;

        int actualTarget = trainingOrTest == "Test" ? 3000 : targetDeliveries;

        isCollecting = true;
        deliveriesRecorded = 0;
        ballsInCurrentSet = 0;
        dataCollector.ClearData();
        pad.SetGhostMode(true);

        pad.RandomizePosition();
        currentPadPosition = pad.transform.position;

        StartCoroutine(SpawnBallsRoutine(actualTarget));
        Debug.Log($"IMPROVED COLLECTION STARTED - Target: {actualTarget} deliveries");
        Debug.Log($"Recording pad impacts via event system");
    }

    public void StopFastCollection(string trainingOrTest)
    {
        isCollecting = false;
        StopAllCoroutines();

        foreach (var ball in activeBalls)
        {
            if (ball.ballObject != null)
                Destroy(ball.ballObject);
        }
        activeBalls.Clear();
        ballLookup.Clear();

        dataCollector.SaveAsCSV(trainingOrTest);
        dataCollector.SaveDataset(trainingOrTest);

        Debug.Log($"COLLECTION COMPLETE: {deliveriesRecorded} deliveries recorded");
    }

    IEnumerator SpawnBallsRoutine(int targetCount)
    {
        while (isCollecting && deliveriesRecorded < targetCount)
        {
            if (ballsInCurrentSet >= ballsPerPadPosition)
            {
                ballsInCurrentSet = 0;
                pad.RandomizePosition();
                currentPadPosition = pad.transform.position;
                Debug.Log($"=== NEW PAD POSITION === X={currentPadPosition.x:F2} | Progress: {deliveriesRecorded}/{targetCount}");
            }

            // Progressively focus on challenging deliveries
            float progress = (float)deliveriesRecorded / targetCount;
            if (progress >= 0.5f)
            {
                autoBowlAngleMax = -15f;
            }
            if (progress >= 0.75f)
            {
                autoBowlAngleMax = -17f;
                autoBowlAngleMin = -21.5f;
            }
            if (progress >= 0.9f)
            {
                autoBowlAngleMax = -18.5f;
                autoBowlAngleMin = -20.5f;
            }

            SpawnBall();
            ballsInCurrentSet++;

            yield return new WaitForSeconds(spawnInterval);
        }

        yield return new WaitForSeconds(3f);
        StopFastCollection(trainingOrTestData);
    }

    void SpawnBall()
    {
        GameObject ballObj = Instantiate(ballPrefab, releasePoint, Quaternion.identity);
        Rigidbody2D rb = ballObj.GetComponent<Rigidbody2D>();

        // Ignore collisions between balls
        foreach (var otherBall in activeBalls)
        {
            if (otherBall.ballObject != null)
            {
                Physics2D.IgnoreCollision(
                    ballObj.GetComponent<Collider2D>(),
                    otherBall.ballObject.GetComponent<Collider2D>()
                );
            }
        }

        // Calculate launch parameters
        float angle = Random.Range(autoBowlAngleMin, autoBowlAngleMax);
        float radians = angle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        Bowling.SpinType spin = (Bowling.SpinType)Random.Range(0, 2);
        float speedMult = spin == Bowling.SpinType.TopSpin ?
            topSpinSpeedMultiplier : backSpinSpeedMultiplier;

        float speedVar = Random.Range(1f - speedVariation, 1f + speedVariation);
        float currentSpeed = speedMult * speedVar;

        Vector2 force = direction.normalized * fixedDistance * forceMultiplier * currentSpeed;
        float torque = spin == Bowling.SpinType.TopSpin ? topSpinTorque : backSpinTorque;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.AddForce(force, ForceMode2D.Impulse);
        rb.AddTorque(torque);

        BallInstance instance = new BallInstance
        {
            ballObject = ballObj,
            rb = rb,
            spinType = spin,
            spinMagnitude = Mathf.Abs(torque),
            hasRecordedImpact = false,
            isFinalized = false,
            impactData = null
        };

        activeBalls.Add(instance);
        ballLookup[ballObj] = instance;
    }

    void CheckForStumpsOutcome()
    {
        float stumpsX = stumps.transform.position.x;

        foreach (var ball in activeBalls)
        {
            if (ball.isFinalized) continue;

            // Once ball passes stumps, finalize the delivery
            if (ball.ballObject.transform.position.x >= stumpsX + 0.5f)
            {
                FinalizeBall(ball);
            }
        }
    }

    void FinalizeBall(BallInstance ball)
    {
        if (ball.isFinalized) return;
        ball.isFinalized = true;

        // Only create a record if the ball hit the pad
        if (ball.hasRecordedImpact && ball.impactData != null)
        {
            bool hitStumps = stumps.DidBallHit(ball.ballObject);

            LBWDataV2.DeliveryRecord record = new LBWDataV2.DeliveryRecord
            {
                impactPosX = ball.impactData.position.x,
                impactPosY = ball.impactData.position.y,
                impactVelX = ball.impactData.velocity.x,
                impactVelY = ball.impactData.velocity.y,
                impactAngularVel = ball.impactData.angularVelocity,
                spinDirection = (int)ball.spinType,
                spinMagnitude = ball.spinMagnitude,
                distanceToStumps = Vector2.Distance(
                    ball.impactData.position,
                    stumps.transform.position
                ),
                willHitStumps = hitStumps ? 1 : 0
            };

            dataCollector.AddDelivery(record);
            deliveriesRecorded++;

            Debug.Log($"Delivery {deliveriesRecorded}: HitStumps={hitStumps}");
        }
    }

    void CleanupFinishedBalls()
    {
        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            var ball = activeBalls[i];

            bool isStopped = ball.rb.linearVelocity.magnitude < 0.1f;
            bool isFarAway = ball.ballObject.transform.position.x > 20f;

            if ((ball.isFinalized && isStopped) || isFarAway)
            {
                if (!ball.isFinalized)
                    FinalizeBall(ball);

                ballLookup.Remove(ball.ballObject);
                Destroy(ball.ballObject);
                activeBalls.RemoveAt(i);
            }
        }
    }
}