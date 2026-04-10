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
    [SerializeField] private string demoSceneName = "more-demo-scene";
    [SerializeField] private string gameSceneName = "more-Special-Effect";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   clickSound;

    [Header("Background Music")]
    [SerializeField] private AudioSource musicAudioSource;  // Separate AudioSource for music
    [SerializeField] private AudioClip   backgroundMusic;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────
    void Start()
    {
        // Start background music when app launches
        if (musicAudioSource != null && backgroundMusic != null)
        {
            musicAudioSource.clip = backgroundMusic;
            musicAudioSource.loop = true;
            musicAudioSource.Play();
            Debug.Log($"{TAG} 🎵 Background music started.");
        }
        else
        {
            Debug.LogWarning($"{TAG} ⚠️ Music AudioSource or clip not assigned!");
        }
    }

    // ──────────────────────────────────────────────
    // Button callbacks
    // ──────────────────────────────────────────────
    public void OnDemoButtonClicked()
    {
        Debug.Log($"{TAG} Demo button clicked. Loading scene: {demoSceneName}");
        PlayClickSound();
        StartCoroutine(LoadSceneAfterClick(demoSceneName));
    }

    public void OnGameButtonClicked()
    {
        Debug.Log($"{TAG} Game button clicked. Loading scene: {gameSceneName}");
        PlayClickSound();
        StartCoroutine(LoadSceneAfterClick(gameSceneName));
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
            Debug.Log($"{TAG} 🔊 Click sound played.");
        }
        else
        {
            Debug.LogWarning($"{TAG} ⚠️ AudioSource or clickSound not assigned!");
        }
    }

    private System.Collections.IEnumerator LoadSceneAfterClick(string sceneName)
    {
        // Wait for click sound to finish before loading scene
        // This also naturally stops the music since the scene (and AudioSource) gets destroyed
        float clipLength = (clickSound != null) ? clickSound.length : 0.1f;
        yield return new WaitForSeconds(clipLength);

        // Stop music before scene transition
        if (musicAudioSource != null && musicAudioSource.isPlaying)
        {
            musicAudioSource.Stop();
            Debug.Log($"{TAG} 🔇 Background music stopped.");
        }

        Debug.Log($"{TAG} Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}