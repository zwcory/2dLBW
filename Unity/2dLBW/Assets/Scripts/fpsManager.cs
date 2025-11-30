using UnityEngine;

public class fpsManager : MonoBehaviour
{
    private void Awake()
    {
        // Lock to 60 FPS
        Application.targetFrameRate = 60;

        // Disable VSync (optional, ensures consistent 60fps)
        QualitySettings.vSyncCount = 0;

        Debug.Log("Game locked to 60 FPS");
    }
}