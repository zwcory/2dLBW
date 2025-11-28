using System.Collections.Generic;
using UnityEngine;

public class Pad : MonoBehaviour
{
    [SerializeField] private SpriteRenderer padRenderer;
    [SerializeField] private Collider2D padCollider;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color ghostColor = new Color(1f, 1f, 1f, 0.3f);

    [Header("Movement Settings")]
    [SerializeField] private float minPosition = 2f;  // Minimum X position
    [SerializeField] private float maxPosition = 8f;  // Maximum X position
    [SerializeField] private float moveVariation = 0.5f; // How much it moves

    private Vector3 startPosition;
    private bool isGhostMode = false;
    private bool wasHit = false;
    private HashSet<GameObject> ballsHit = new HashSet<GameObject>();

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
        // Random movement forward/backward
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
            ballsHit.Add(collision.gameObject);
            wasHit = true;
            Debug.Log("PAD HIT! (LBW Check)");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"TRIGGER ENTER: {collision.gameObject.name}, " +
                  $"Tag: {collision.tag}, GhostMode: {isGhostMode}");

        if (isGhostMode && collision.gameObject.CompareTag("Ball"))
        {
            ballsHit.Add(collision.gameObject);
            Debug.Log("Ball passed through ghost pad");
            wasHit = true;
        }
    }

    public bool DidBallHit(GameObject ball)
    {
        return ballsHit.Contains(ball);
    }

    public void ResetPad()
    {
        wasHit = false;
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

    public bool IsPadColliderEnabled()
    {
        return padCollider.isActiveAndEnabled;
    }
}