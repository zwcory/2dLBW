using UnityEngine;

public class Stumps : MonoBehaviour
{
    [SerializeField] private SpriteRenderer indicatorBox;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color missColor = Color.green;

    private bool wasHit = false;

    private void Start()
    {
        if (indicatorBox != null)
        {
            indicatorBox.color = missColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            wasHit = true;
            if (indicatorBox != null)
            {
                indicatorBox.color = hitColor;
            }
            Debug.Log("STUMPS HIT!");
        }
    }

    public void ResetStumps()
    {
        wasHit = false;
        if (indicatorBox != null)
        {
            indicatorBox.color = missColor;
        }
    }

    public bool WasHit()
    {
        return wasHit;
    }
}