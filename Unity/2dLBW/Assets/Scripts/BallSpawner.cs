using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public GameObject ball;
    

    public void SpawnBall()
    {
        Instantiate(ball, new Vector3(transform.position.x, transform.position.y, 0), transform.rotation);
    }
}
