using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;


public class Bowling : MonoBehaviour
{
    [Header("Bowl Settings")]
    [SerializeField] private float forceMultiplier = 10f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float aimRangeRadius = 2f;

    [Header("Spin Settings")]
    [SerializeField] private float topSpinTorque = 50f;
    [SerializeField] private float backSpinTorque = -50f;
    [SerializeField] private float topSpinDownForce = 5f; // Dips ball down
    [SerializeField] private float backSpinLiftForce = 3f; // Keeps ball up

    [Header("References")]
    [SerializeField] private Stumps stumps;

    private Rigidbody2D rb;
    private LineRenderer lineRenderer;
    private Vector2 startPosition;
    private bool hasLaunched = false;
    private SpinType currentSpin = SpinType.None;

    public enum SpinType
    {
        None,
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
    }

    private void Update()
    {
        if (!hasLaunched)
        {
            UpdateArrow();

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            {
                BowlBall();
            }
        }
        else
        {
            ApplySpinEffects();
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

        float distance = Mathf.Min(direction.magnitude, maxDistance);
        Vector2 bowlDirection = direction.normalized * distance;

        // Draw arrow
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
            // Restrict mouse to range around start position after launch
            Vector2 offset = mousePos - startPosition;
            if (offset.magnitude > aimRangeRadius)
            {
                offset = offset.normalized * aimRangeRadius;
            }
            return startPosition + offset;
        }

        return mousePos;
    }

    void BowlBall()
    {
        Vector2 mousePos = GetClampedMousePosition();
        Vector2 direction = mousePos - (Vector2)transform.position;

        float distance = Mathf.Min(direction.magnitude, maxDistance);
        Vector2 force = direction.normalized * distance * forceMultiplier;

        // Enable physics
        rb.bodyType = RigidbodyType2D.Dynamic;
        hasLaunched = true;

        // Apply force
        rb.AddForce(force, ForceMode2D.Impulse);

        // Apply spin
        ApplyInitialSpin();

        // Hide arrow
        lineRenderer.enabled = false;

        Debug.Log($"Bowled with {currentSpin} spin!");
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


    private bool isGrounded = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we hit the ground/floor
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            Debug.Log("Ball hit ground - stopping spin effects");
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
    public void SetSpinNone()
    {
        currentSpin = SpinType.None;
        Debug.Log("Spin: None");
    }

    public void SetTopSpin()
    {
        currentSpin = SpinType.TopSpin;
        Debug.Log("Spin: Top Spin (dips down)");
    }

    public void SetBackSpin()
    {
        currentSpin = SpinType.BackSpin;
        Debug.Log("Spin: Back Spin (seam)");
    }

    public void ResetBall()
    {
        // Reset for next bowl
        transform.position = startPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        hasLaunched = false;
        lineRenderer.enabled = true;

        if (stumps != null)
        {
            stumps.ResetStumps();
        }
    }
}

// Extension method for drawing circles
public static class DebugDrawExtensions
{
    public static void DrawCircle(Vector2 center, float radius, Color color)
    {
        int segments = 32;
        float angle = 0f;
        Vector2 lastPoint = center + new Vector2(radius, 0);

        for (int i = 0; i <= segments; i++)
        {
            angle = (i / (float)segments) * 2f * Mathf.PI;
            Vector2 newPoint = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
            Debug.DrawLine(lastPoint, newPoint, color);
            lastPoint = newPoint;
        }
    }
}