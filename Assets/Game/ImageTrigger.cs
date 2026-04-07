using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Concurrent;

public class ImageTrigger : MonoBehaviour
{
    [SerializeField] private Image imageToShow; 
    [SerializeField] private float displayDuration = 2f; 
    
    private readonly ConcurrentQueue<bool> pendingTrigger = new();

    void Start() {
        // We move this here to give MqttService time to run its Awake() first
        if (MqttService.Instance != null) {
            MqttService.Instance.OnMessageReceived += CheckForMessage;
            Debug.Log("[ImageTrigger] Successfully subscribed to MqttService in Start().");
        } else {
            Debug.LogError("[ImageTrigger] MqttService is STILL null. Check if the script is in your Scene Hierarchy!");
        }
    }

    void OnDisable() {
        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived -= CheckForMessage;
    }

    private void CheckForMessage(string topic, string payload) {
        // DEBUG LOG 1: See every message that hits this script
        Debug.Log($"[ImageTrigger] Received raw broadcast - Topic: {topic}, Payload: {payload}");

        // Match the topic and message seen in your previous logs
        if (topic == "ultra/action1" && payload.Trim() == "hehe") {
            Debug.Log("[ImageTrigger] MATCH FOUND! Enqueueing trigger.");
            pendingTrigger.Enqueue(true);
        } else {
            // DEBUG LOG 2: If it doesn't match, show why
            Debug.Log($"[ImageTrigger] No match. Expected 'ultra/action1' and 'hehe'. Got '{topic}' and '{payload.Trim()}'");
        }
    }

    void Update() {
        if (pendingTrigger.TryDequeue(out bool shouldShow)) {
            Debug.Log("[ImageTrigger] Main thread dequeued trigger. Starting Coroutine.");
            StopAllCoroutines(); 
            StartCoroutine(ShowImageRoutine());
        }
    }

    private IEnumerator ShowImageRoutine() {
        if (imageToShow != null) {
            imageToShow.enabled = true; 
            Debug.Log("[ImageTrigger] Image Component ENABLED.");
            
            yield return new WaitForSeconds(displayDuration); 
            
            imageToShow.enabled = false; 
            Debug.Log("[ImageTrigger] Image Component DISABLED.");
        } else {
            Debug.LogError("[ImageTrigger] Cannot show image because imageToShow is MISSING in the Inspector!");
        }
    }
}