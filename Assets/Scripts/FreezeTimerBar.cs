// FreezeTimerBar.cs
// Attach to XR Origin.
// Controls the freeze timer slider shown during ice power-up.
// Setup: UI -> Canvas -> Slider, delete the Handle child object.
// Set Min Value = 0, Max Value = 1, disable Interactable.

using UnityEngine;
using UnityEngine.UI;

public class FreezeTimerBar : MonoBehaviour
{
    private const string TAG = "[FREEZE_BAR]";

    [Header("Freeze Bar UI")]
    [SerializeField] private GameObject freezeBarPanel;  // Parent panel to show/hide
    [SerializeField] private Slider     freezeSlider;    // The slider component
    [SerializeField] private Image      fillImage;       // The fill area image for colour change

    [Header("Bar Colours")]
    [SerializeField] private Color fullColour  = new Color(0.4f, 0.8f, 1f);   // Light blue when full
    [SerializeField] private Color emptyColour = new Color(0.1f, 0.2f, 0.5f); // Dark blue when empty

    private float totalFreezeDuration = 0f;
    private float timeRemaining       = 0f;
    private bool  isActive            = false;

    void Awake()
    {
        // Make sure slider is set up correctly
        if (freezeSlider != null)
        {
            freezeSlider.minValue     = 0f;
            freezeSlider.maxValue     = 1f;
            freezeSlider.value        = 1f;
            freezeSlider.interactable = false;  // Player cannot drag it
        }

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

        // Update slider value (1 = full, 0 = empty)
        float fillAmount = timeRemaining / totalFreezeDuration;

        if (freezeSlider != null)
            freezeSlider.value = fillAmount;

        // Lerp colour from empty to full based on remaining time
        if (fillImage != null)
            fillImage.color = Color.Lerp(emptyColour, fullColour, fillAmount);
    }

    /// <summary>
    /// Call this when ice power-up is collected to start the bar countdown.
    /// </summary>
    public void StartBar(float duration)
    {
        totalFreezeDuration = duration;
        timeRemaining       = duration;
        isActive            = true;

        if (freezeSlider != null)
        {
            freezeSlider.value = 1f;
        }

        if (fillImage != null)
            fillImage.color = fullColour;

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
}