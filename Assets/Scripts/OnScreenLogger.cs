using UnityEngine;
using TMPro; // Make sure this is at the top!

public class OnScreenLogger : MonoBehaviour
{
    // Adding [SerializeField] ensures the "slot" appears in the Inspector
    [SerializeField] public TextMeshProUGUI logDisplay; 
    
    public int maxLines = 10;
    private System.Collections.Generic.List<string> logLines = new System.Collections.Generic.List<string>();

    void OnEnable() { Application.logMessageReceived += HandleLog; }
    void OnDisable() { Application.logMessageReceived -= HandleLog; }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logLines.Add(logString);
        if (logLines.Count > maxLines) logLines.RemoveAt(0);
        if (logDisplay != null) logDisplay.text = string.Join("\n", logLines);
    }
}