using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FastDataCollector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Stumps stumps;
    [SerializeField] private Pad pad;
    [SerializeField] private LBWData dataCollector;
    [SerializeField] private Bowling bowlingScript;

    [Header("Collection Settings")]
    [SerializeField] private int ballsPerSet = 5;
    [SerializeField] private float spawnInterval = 0.3f;
    [SerializeField] private int targetSamples = 10000;
    [SerializeField] private float sampleRate = 0.05f; // Sample every 0.05 seconds

    [Header("Auto Bowl Parameters")]
    [SerializeField] private float autoBowlAngleMin = -35f;
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
    private List<BallInstance> activeBalls = new List<BallInstance>();
    private bool isCollecting = false;
    private int totalSamples = 0;
    private int ballsSpawned = 0;
    private int ballsInCurrentSet = 0;
    private Vector2 currentPadPosition;

    private class BallInstance
    {
        public GameObject ballObject;
        public Rigidbody2D rb;
        public float spawnTime;
        public float lastSampleTime;
        public Bowling.SpinType spinType;
        public float speed;
        public float spinAmount;
        public Vector2 padPositionAtSpawn;
        public List<SampleData> samples;
        public bool reachedPad;
        public bool finalized;
    }

    private class SampleData
    {
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
            RecordAllActiveBalls();
            CleanupFinishedBalls();
        }
    }

    public void StartFastCollection()
    {
        if (isCollecting) return;

        isCollecting = true;
        totalSamples = 0;
        ballsSpawned = 0;
        ballsInCurrentSet = 0;
        dataCollector.ClearData();
        pad.SetGhostMode(true);

        pad.RandomizePosition();
        currentPadPosition = pad.transform.position;

        StartCoroutine(SpawnBallsRoutine());
        Debug.Log($"FAST COLLECTION STARTED - Target: {targetSamples} samples");
        Debug.Log($"Recording until pad contact, sample rate: {sampleRate}s");
    }

    public void StopFastCollection(string trainingOrTest)
    {
        isCollecting = false;
        StopAllCoroutines();

        foreach (var ball in activeBalls)
        {
            if (!ball.finalized)
                FinalizeBall(ball);
            if (ball.ballObject != null)
                Destroy(ball.ballObject);
        }
        activeBalls.Clear();

        dataCollector.SaveAsCSV(trainingOrTest);
        dataCollector.SaveDataset(trainingOrTest);

        Debug.Log($"COLLECTION COMPLETE: {totalSamples} samples from {ballsSpawned} balls");
    }


    IEnumerator SpawnBallsRoutine()
    {
        while (isCollecting && totalSamples < targetSamples)
        {
            if (ballsInCurrentSet >= ballsPerSet)
            {
                pad.RandomizePosition();
                currentPadPosition = pad.transform.position;
                ballsInCurrentSet = 0;

                Debug.Log($"=== NEW SET === Pad position: X={currentPadPosition.x:F2}");
            }

            SpawnBall();
            ballsSpawned++;
            ballsInCurrentSet++;

            yield return new WaitForSeconds(spawnInterval);
        }

        yield return new WaitForSeconds(3f);
        StopFastCollection("Training");
    }

    void SpawnBall()
    {


        GameObject ballObj = Instantiate(ballPrefab, releasePoint, Quaternion.identity);
        Rigidbody2D rb = ballObj.GetComponent<Rigidbody2D>();


        // Verify ball setup
        if (!ballObj.CompareTag("Ball"))
        {
            Debug.LogError($"Ball {ballObj.name} doesn't have 'Ball' tag!");
        }

        Collider2D ballCollider = ballObj.GetComponent<Collider2D>();
        if (ballCollider == null)
        {
            Debug.LogError($"Ball {ballObj.name} has no collider!");
        }
        else
        {
            Debug.Log($"Ball spawned: isTrigger={ballCollider.isTrigger}");
        }


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
            spawnTime = Time.time,
            lastSampleTime = Time.time,
            spinType = spin,
            speed = currentSpeed,
            spinAmount = Mathf.Abs(torque),
            padPositionAtSpawn = currentPadPosition,
            samples = new List<SampleData>(),
            reachedPad = false,
            finalized = false
        };

        activeBalls.Add(instance);
    }

    void RecordAllActiveBalls()
    {
        foreach (var ball in activeBalls)
        {
            if (ball.finalized) continue;

            float ballX = ball.ballObject.transform.position.x;
            float padX = pad.transform.position.x;
            float stumpsX = stumps.transform.position.x;

            // ONLY record samples BEFORE pad contact
            if (ballX < padX && !ball.reachedPad)
            {
                if (Time.time - ball.lastSampleTime >= sampleRate)
                {
                    RecordSample(ball);
                    ball.lastSampleTime = Time.time;
                }
            }
            else if (!ball.reachedPad)
            {
                // Mark as reached pad, stop recording
                ball.reachedPad = true;

                // Record ONE FINAL sample AT pad position
                RecordSample(ball);

                Debug.Log($"Ball reached pad. Recorded {ball.samples.Count} samples");
            }

            // Wait until ball passes stumps to finalize and label
            if (ballX >= stumpsX + 0.5f)
            {
                FinalizeBall(ball);
            }
        }
    }

    void RecordSample(BallInstance ball)
    {
        float timeSinceRelease = Time.time - ball.spawnTime;

        bool hitPadSoFar = pad.DidBallHit(ball.ballObject);
        bool reachedPadX = ball.ballObject.transform.position.x >= pad.transform.position.x;

        if (ball.samples.Count % 10 == 0)
        {
            Debug.Log($"Ball {ball.ballObject.name}: X={ball.ballObject.transform.position.x:F2}, " +
                      $"PadX={pad.transform.position.x:F2}, HitPad={hitPadSoFar}");
        }


        SampleData sample = new SampleData
        {
            timeSinceRelease = timeSinceRelease,
            ballPosX = ball.ballObject.transform.position.x,
            ballPosY = ball.ballObject.transform.position.y,
            ballVelX = ball.rb.linearVelocity.x,
            ballVelY = ball.rb.linearVelocity.y,
            ballAngularVel = ball.rb.angularVelocity,
            distanceToStumps = Vector2.Distance(
                ball.ballObject.transform.position,
                stumps.transform.position),
            distanceToPad = Vector2.Distance(
                ball.ballObject.transform.position,
                pad.transform.position),
            hitPad = hitPadSoFar ? 1 : 0,
            reachedPadPosition = reachedPadX ? 1 : 0
            
        };

        ball.samples.Add(sample);
    }

    void FinalizeBall(BallInstance ball)
    {
        if (ball.finalized) return;
        ball.finalized = true;

        // Check final outcomes
        bool hitStumps = stumps.DidBallHit(ball.ballObject);
        bool hitPad = pad.DidBallHit(ball.ballObject);

        int stumpLabel = hitStumps ? 1 : 0;

        Debug.Log($"Ball finalized: {ball.samples.Count} samples, " +
                 $"HitPad={hitPad}, WillHitStumps={hitStumps}");

        // Add all recorded samples with final label
        foreach (var sample in ball.samples)
        {
            LBWData.TrainingExample example = new LBWData.TrainingExample
            {
                spinType = (int)ball.spinType,
                speed = ball.speed,
                spinAmount = ball.spinAmount,
                timeSinceRelease = sample.timeSinceRelease,
                ballPosX = sample.ballPosX,
                ballPosY = sample.ballPosY,
                ballVelX = sample.ballVelX,
                ballVelY = sample.ballVelY,
                ballAngularVel = sample.ballAngularVel,
                distanceToStumps = sample.distanceToStumps,
                distanceToPad = sample.distanceToPad,
                hitPad = sample.hitPad, // Use the sample's hitPad status
                reachedPadPosition = sample.reachedPadPosition,
                willHitStumps = stumpLabel // Same label for all samples
            };

            dataCollector.AddSampleDirectly(example);
        }

        totalSamples += ball.samples.Count;
    }

    void CleanupFinishedBalls()
    {
        for (int i = activeBalls.Count - 1; i >= 0; i--)
        {
            var ball = activeBalls[i];

            bool isStopped = ball.rb.linearVelocity.magnitude < 0.1f;
            bool isOld = Time.time - ball.spawnTime > 5f;
            bool isFarAway = ball.ballObject.transform.position.x > 20f;

            if ((ball.finalized && isStopped) || isOld || isFarAway)
            {
                if (!ball.finalized)
                    FinalizeBall(ball);

                Destroy(ball.ballObject);
                activeBalls.RemoveAt(i);
            }
        }
    }
}