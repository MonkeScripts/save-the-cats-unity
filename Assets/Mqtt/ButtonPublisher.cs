using UnityEngine;

public class ButtonPublisher : MonoBehaviour
{
    // Assign this method to a UI Button's OnClick event in the Inspector
    public void OnButtonPressed()
    {
        MqttApi.ButtonPress();
    }
}
