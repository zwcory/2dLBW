using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


public class Bowling : MonoBehaviour
{
    [Header("Bowl Settings")]
    [SerializeField] private float forceMultiplier = 10f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float aimRangeRadius = 2f;

    [Header("Aim Constraints")]
    [SerializeField] private float minAimAngle = -35f;      // Degrees from right (0° = straight right)
    [SerializeField] private float maxAimAngle = -5f;     // Max angle upward
    [SerializeField] private float minAimDistance = 1f;


    [Header("Speed Variations")]
    [SerializeField] private float topSpinSpeedMultiplier = 0.85f;
    [SerializeField] private float backSpinSpeedMultiplier = 1.0f;
    [SerializeField] private float speedVariation = 0.1f;

    [Header("Spin Settings")]
    [SerializeField] private float topSpinTorque = 10f;
    [SerializeField] private float backSpinTorque = -10f;
    [SerializeField] private float topSpinDownForce = 0.1f; 
    [SerializeField] private float backSpinLiftForce = 0.1f; 


    [Header("References")]
    [SerializeField] private Stumps stumps;
    [SerializeField] private SpriteRenderer decisionIndicator;
    [SerializeField] private Color waitColor = Color.yellow;



    private Rigidbody2D rb;
    private LineRenderer lineRenderer;
    private Vector2 startPosition;
    private bool hasLaunched = false;
    private bool isGrounded = false;
    private SpinType currentSpin = SpinType.BackSpin;


    private float currentSpeed;
    private float currentSpinAmount;

    [SerializeField] private Collider2D stumpCollider;
    [SerializeField] private Pad padScript;



    public enum SpinType
    {
        TopSpin,   // Ball dips down
        BackSpin   // Ball stays up longer
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lineRenderer = GetComponent<LineRenderer>();

        startPosition = transform.position;

        // Keep ball frozen until launched
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (stumpCollider == null)
            stumpCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!hasLaunched)
        {
            UpdateArrow();

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                BowlBallManual();
            }
        }
        else
        {
            ApplySpinEffects();
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame) 
        { 
            ResetBall();
        }


    }

    bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void UpdateArrow()
    {
        Vector2 mousePos = GetClampedMousePosition();
        Vector2 direction = mousePos - (Vector2)transform.position;

        // Apply aim constraints
        direction = ApplyAimConstraints(direction);

        float distance = Mathf.Min(direction.magnitude, maxDistance);
        Vector2 bowlDirection = direction.normalized * distance;

        // Draw arrow (line)
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, Vector3.zero);
        lineRenderer.SetPosition(1, bowlDirection);
        lineRenderer.enabled = true;
    }

    Vector2 GetClampedMousePosition()
    {
        Vector2 mousePos =
            Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        if (hasLaunched)
        {

            Vector2 offset = mousePos - startPosition;
            if (offset.magnitude > aimRangeRadius)
            {
                offset = offset.normalized * aimRangeRadius;
            }
            return startPosition + offset;
        }

        return mousePos;
    }

    Vector2 ApplyAimConstraints(Vector2 direction)
    {

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Clamp angle
        angle = Mathf.Clamp(angle, minAimAngle, maxAimAngle);

        float magnitude = direction.magnitude;
        magnitude = Mathf.Max(magnitude, minAimDistance);

        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(radians) * magnitude,
            Mathf.Sin(radians) * magnitude
        );
    }

    void BowlBall(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - (Vector2)transform.position;


        // Apply aim constraints
        direction = ApplyAimConstraints(direction);

        // changed from direction magnitude to make it consistant
        float distance = 2f;

        // Apply speed multiplier based on spin type
        float speedMult = currentSpin == SpinType.TopSpin ?
            topSpinSpeedMultiplier : backSpinSpeedMultiplier;


        float speedVar = Random.Range(1f - speedVariation, 1f + speedVariation);
        currentSpeed = speedMult * speedVar;

        Vector2 force = direction.normalized * distance * forceMultiplier;
        rb.bodyType = RigidbodyType2D.Dynamic;
        hasLaunched = true;
        rb.AddForce(force, ForceMode2D.Impulse);
        ApplyInitialSpin();
        lineRenderer.enabled = false;


        BallTracker existingTracker = gameObject.GetComponent<BallTracker>();
        if (existingTracker != null)
        {
            Debug.LogWarning("Found existing BallTracker, destroying it");
            Destroy(existingTracker);
        }


        float currentSpinAmount = Mathf.Abs(
        currentSpin == SpinType.TopSpin ? topSpinTorque : backSpinTorque
           );




        padScript.RandomizePosition();



        BallTracker tracker = gameObject.AddComponent<BallTracker>();
        tracker.Initialize(currentSpin, currentSpeed, currentSpinAmount);

        float radians = Mathf.Atan2(direction.y, direction.x);
        float angle = radians * Mathf.Rad2Deg;

    }




    void ApplyInitialSpin()
    {
        switch (currentSpin)
        {
            case SpinType.TopSpin:
                rb.AddTorque(topSpinTorque);
                break;
            case SpinType.BackSpin:
                rb.AddTorque(backSpinTorque);
                break;
        }
    }



    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isGrounded) 
        {
            // Check if we hit the ground/floor
            if (collision.gameObject.CompareTag("Ground"))
            {
                isGrounded = true;
                
            }
        }
        
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }


    void ApplySpinEffects()
    {
        // Only apply Magnus effect while ball is moving
        if (isGrounded || rb.linearVelocity.magnitude < 0.5f) return;

        switch (currentSpin)
        {
            case SpinType.TopSpin:
                // Extra downward force
                rb.AddForce(Vector2.down * topSpinDownForce, ForceMode2D.Force);
                break;

            case SpinType.BackSpin:
                // Upward force (anti-gravity)
                rb.AddForce(Vector2.up * backSpinLiftForce, ForceMode2D.Force);
                break;
        }
    }



    // Public methods for UI buttons
    public void SetTopSpin()
    {
        currentSpin = SpinType.TopSpin;
    }

    public void SetBackSpin()
    {
        currentSpin = SpinType.BackSpin;
    }

    public void ResetBall()
    {
        // Reset for next bowl
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        rb.rotation = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        hasLaunched = false;
        lineRenderer.enabled = true;

        if (stumps != null)
        {
            stumps.ResetStumps();
        }
        stumpCollider.enabled = true;
        if (padScript != null)
        {
            padScript.ResetPad();
        };
        if(decisionIndicator != null)
        {
            decisionIndicator.color = waitColor;
        }
    }

    void BowlBallManual()
    {
        Vector2 targetPosition = GetClampedMousePosition();
        BowlBall(targetPosition);
    }

    public DeliveryData GetCurrentDeliveryData()
    {
        return new DeliveryData
        {
            spinType = currentSpin,
            speed = currentSpeed,
            spinAmount = currentSpinAmount,
            startPosition = startPosition,
            releaseVelocity = rb.linearVelocity,
            timestamp = Time.time
        };
    }

}


[System.Serializable]
public struct DeliveryData
{
    public Bowling.SpinType spinType;
    public float speed;
    public float spinAmount;
    public Vector2 startPosition;
    public Vector2 releaseVelocity;
    public float timestamp;
}