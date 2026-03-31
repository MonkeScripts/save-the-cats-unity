using UnityEngine;
using UnityEngine.UI; // Required for the Image component
using System.Collections.Concurrent;

public class MessageTrigger : MonoBehaviour
{
    [SerializeField] private Image imageToShow; // Drag your UI Image here in the Inspector
    private readonly ConcurrentQueue<bool> pendingTrigger = new();

    void OnEnable() {
        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived += CheckForHehe;
    }

    void OnDisable() {
        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived -= CheckForHehe;
    }

    private void CheckForHehe(string topic, string payload) {
        // Match the topic and message seen in your logs
        if (topic == "ultra/action1" && payload.Trim() == "hehe") {
            pendingTrigger.Enqueue(true);
        }
    }

    void Update() {
        // This runs on the Main Thread, so it is safe to change UI
        if (pendingTrigger.TryDequeue(out bool shouldShow)) {
            if (imageToShow != null) {
                imageToShow.enabled = true; // Shows the image
                // Or: imageToShow.gameObject.SetActive(true);
                Debug.Log("Hehe received! Image is now visible.");
            }
        }
    }
}