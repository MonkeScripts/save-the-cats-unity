// FreezeTimerBar.cs
// Attach to XR Origin.
// Controls the freeze timer bar shown during ice power-up.
// Uses the same ManaBar asset sliding technique as SlackerBar.
// Duplicate your ManaBar GameObject and name it FreezeBar,
// then assign the inner ManaBar Image to currentFreezeBar.

using UnityEngine;
using UnityEngine.UI;

public class FreezeTimerBar : MonoBehaviour
{
    private const string TAG = "[FREEZE_BAR]";

    [Header("Freeze Bar UI")]
    [SerializeField] private GameObject freezeBarPanel;   // The FreezeBar parent GameObject
    [SerializeField] private Image      currentFreezeBar; // The ManaBar Image inside FreezeBarMask

    private float totalFreezeDuration = 0f;
    private float timeRemaining       = 0f;
    private bool  isActive            = false;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────
    void Awake()
    {
        if (freezeBarPanel != null) freezeBarPanel.SetActive(false);
    }

    void Update()
    {
        if (!isActive) return;

        // Use unscaledDeltaTime so bar drains even when game timer is paused
        timeRemaining -= Time.unscaledDeltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            HideBar();
            return;
        }

        // Update bar fill
        UpdateFreezeBar();
    }

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Call this when ice power-up is collected to start the bar countdown.
    /// </summary>
    public void StartBar(float duration)
    {
        totalFreezeDuration = duration;
        timeRemaining       = duration;
        isActive            = true;

        // Show full bar immediately
        UpdateFreezeBar();

        if (freezeBarPanel != null) freezeBarPanel.SetActive(true);

        Debug.Log($"{TAG} ❄️ Freeze bar started for {duration}s");
    }

    /// <summary>
    /// Call this to force hide the bar (round end, cleanup).
    /// </summary>
    public void HideBar()
    {
        isActive = false;
        if (freezeBarPanel != null) freezeBarPanel.SetActive(false);
        Debug.Log($"{TAG} ❄️ Freeze bar hidden.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Slides the FreezeBar image inside FreezeBarMask to show remaining time.
    /// Same sliding technique as SlackerBar/ManaBar asset.
    /// </summary>
    private void UpdateFreezeBar()
    {
        if (currentFreezeBar == null)
        {
            Debug.LogWarning($"{TAG} ⚠️ currentFreezeBar Image not assigned!");
            return;
        }

        // 1.0 = full (just collected), 0.0 = empty (expired)
        float ratio = timeRemaining / totalFreezeDuration;

        currentFreezeBar.rectTransform.localPosition = new Vector3(
            currentFreezeBar.rectTransform.rect.width * ratio - currentFreezeBar.rectTransform.rect.width,
            0f,
            0f);

        Debug.Log($"{TAG} ❄️ Freeze bar: {timeRemaining:F1}/{totalFreezeDuration}s (ratio: {ratio:F2})");
    }
}