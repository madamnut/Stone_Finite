


using UnityEngine;
using TMPro;

using Game.Data;
using Game.World;
using Game.Lobby;
public class Debugger : MonoBehaviour
{
    [Header("Toggle Root")]

    public GameObject debugRoot; 

    [Header("UI Text")]
    public TMP_Text fpsText;     
    public TMP_Text timeText;    
    public TMP_Text seedText;    

    [Header("World Time Source")]
    public WorldManager worldManager;

    [Header("FPS Settings")]
    [Range(0.05f, 1f)] public float fpsUpdateInterval = 0.25f;

    float accum;
    int frames;
    float t;

    
    void Start()
    {
        if (debugRoot) debugRoot.SetActive(true);
        if (fpsText)  fpsText.text  = "FPS: 0";
        if (timeText) timeText.text = "Time: 00:00 [Unknown]";
        if (seedText) seedText.text = $"Seed: {WorldLoadContext.seed}";
    }

    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3) && debugRoot)
            debugRoot.SetActive(!debugRoot.activeSelf);

        accum += 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-6f);
        frames += 1;
        t      += Time.unscaledDeltaTime;

        if (t >= fpsUpdateInterval)
        {
            float fps = accum / Mathf.Max(frames, 1);
            if (fpsText) fpsText.text = $"FPS: {fps:F1}";
            accum = 0f; frames = 0; t = 0f;
        }

        if (timeText)
        {
            if (worldManager)
            {
                int hh = worldManager.worldHour;
                int mm = worldManager.worldMinute % 60;
                var band = worldManager.GetTimeBand(); 
                timeText.text = $"Time: {hh:00}:{mm:00} [{band}]";
            }
            else
            {
                timeText.text = "Time: 00:00 [Unknown]";
            }
        }

        if (seedText)
            seedText.text = $"Seed: {WorldLoadContext.seed}";
    }
}
