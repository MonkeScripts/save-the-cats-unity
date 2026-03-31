using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Concurrent;

public class MessagePanel : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private TMP_Text displayText;

    private readonly ConcurrentQueue<string> pendingMessages = new();

    void Start()
    {
        MqttService.Instance.OnMessageReceived += HandleMqtt;
    }

    void Update()
    {
        while (pendingMessages.TryDequeue(out string line))
        {
            displayText.text += line;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    void OnDestroy()
    {
        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived -= HandleMqtt;
    }

    // Called from background thread — only enqueue, never touch UI here
    private void HandleMqtt(string topic, string payload)
    {
        pendingMessages.Enqueue($"[{topic}] {payload}\n");
    }
}
