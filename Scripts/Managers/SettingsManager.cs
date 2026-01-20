using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;

    [Header("Graphic Settings")]
    [SerializeField] private int targetFrameRate = 60;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }    
    }

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0; // disable VSync
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
