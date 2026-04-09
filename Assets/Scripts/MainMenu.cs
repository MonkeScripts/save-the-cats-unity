// MainMenu.cs
// Attach to any GameObject in your Main Menu scene (e.g. Canvas or MainMenuManager).
// Handles navigation from main menu to other scenes.
// Make sure to add all scenes to Build Settings!

using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private const string TAG = "[MAIN_MENU]";

    [Header("Scene Names")]
    [SerializeField] private string demoSceneName = "demo-scene";
    [SerializeField] private string gameSceneName = "more-Special-Effect";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   clickSound;

    public void OnDemoButtonClicked()
    {
        Debug.Log($"{TAG} Demo button clicked. Loading scene: {demoSceneName}");
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        SceneManager.LoadScene(demoSceneName);
    }

    public void OnGameButtonClicked()
    {
        Debug.Log($"{TAG} Game button clicked. Loading scene: {gameSceneName}");
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        SceneManager.LoadScene(gameSceneName);
    }
}