using UnityEngine;
using UnityEngine.InputSystem;

public class DummyPublisher : MonoBehaviour
{
    void Start()
    {
        Debug.Log("DummyPublisher ready! Press Space to publish a test message.");
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            MqttApi.BuzzSuccess();
        }
    }
}
