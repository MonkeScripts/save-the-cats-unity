using UnityEngine;

// extend this class for both IMU windows and Ultra gestures and override the HandleMqtt
// the payloads will be exactly the same as defined in the ideation docs
// in the HandleMqtt part: make sure to filter out the exact topic name using the if (!topic.StartsWith)
public class BaseListener : MonoBehaviour
{
    void OnEnable() {
        MqttService.Instance.OnMessageReceived += HandleMqtt;
    }
    
    void OnDisable() {
        MqttService.Instance.OnMessageReceived -= HandleMqtt;
    }

    void HandleMqtt (string topic, string payload) {
        // if (!topic.StartsWith("ultra/")) return;
        Debug.Log($"[DummyListener] {topic}: {payload}");
    }
}