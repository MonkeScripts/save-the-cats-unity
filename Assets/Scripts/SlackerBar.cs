// SlackerBar.cs
// Attach to XR Origin (same GameObject as overall_game_play).
// Motivation bar that tracks user's exercise consistency.
// Uses the mana bar asset's sliding image technique.
//
// LOGIC:
//   - Starts at 0/10 when game begins
//   - Every 2 correct reps → +1 state (max 10)
//   - Every 5 seconds no reps → -1 state (min 0)
//   - At 0/10: every 3 seconds → -5 cats (cant go below 0)
//   - Only visible during active gameplay
//   - Resets between rounds

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlackerBar : MonoBehaviour
{
    private const string TAG = "[SLACKER_BAR]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Slacker Bar UI")]
    [SerializeField] private GameObject    manaBarPanel;    // Parent ManaBar GameObject (show/hide)
    [SerializeField] private Image         currentManaBar;  // The ManaBar Image inside ManaBarMask
    [SerializeField] private TextMeshProUGUI slackerText;   // Text that appears with the bar

    [Header("Bar Settings")]
    [SerializeField] private float maxManaPoint        = 10f;  // Maximum bar value
    [SerializeField] private float inactivityThreshold = 5f;   // Seconds before -1 state
    [SerializeField] private float penaltyInterval     = 3f;   // Seconds between -5 cats at 0/10

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private float manaPoint       = 0f;
    private int   repCounter      = 0;    // Counts reps, resets every 2
    private float inactivityTimer = 0f;   // Counts up when no reps
    private float penaltyTimer    = 0f;   // Counts up when at 0/10
    private bool  isGameActive    = false;

    // Callbacks from overall_game_play
    private System.Func<int>    getCatCount;
    private System.Action<int>  setCatCount;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────
    void Awake()
    {
        // Hide bar and text on start
        if (manaBarPanel != null) manaBarPanel.SetActive(false);
        if (slackerText  != null) slackerText.gameObject.SetActive(false);
        Debug.Log($"{TAG} 🔄 Awake: Bar and text hidden.");
    }

    void Update()
    {
        if (!isGameActive) return;

        // ── Inactivity Timer (counts up, -1 every 5s) ──
        inactivityTimer += Time.deltaTime;

        if (inactivityTimer >= inactivityThreshold)
        {
            inactivityTimer = 0f;  // Reset timer but keep counting
            DecreaseMana(1f);
            Debug.Log($"{TAG} 😴 Inactivity! -1 state. Current: {manaPoint}/{maxManaPoint}");
        }

        // ── Penalty Timer (only active at 0/10) ──
        if (manaPoint <= 0f)
        {
            penaltyTimer += Time.deltaTime;

            if (penaltyTimer >= penaltyInterval)
            {
                penaltyTimer = 0f;
                ApplyCatPenalty();
            }
        }
        else
        {
            // Reset penalty timer when not at 0
            penaltyTimer = 0f;
        }
    }

    // ──────────────────────────────────────────────
    // Public API - called by overall_game_play
    // ──────────────────────────────────────────────

    /// <summary>
    /// Call when game starts. Pass cat count getter and setter.
    /// </summary>
    public void StartGame(System.Func<int> catCountGetter, System.Action<int> catCountSetter)
    {
        getCatCount = catCountGetter;
        setCatCount = catCountSetter;

        // Reset everything
        manaPoint       = 0f;
        repCounter      = 0;
        inactivityTimer = 0f;
        penaltyTimer    = 0f;
        isGameActive    = true;

        UpdateManaBar();

        // Show bar and text together
        if (manaBarPanel != null) manaBarPanel.SetActive(true);
        if (slackerText  != null) slackerText.gameObject.SetActive(true);

        Debug.Log($"{TAG} ✅ Game started. Bar and text shown. Reset to 0/{maxManaPoint}");
    }

    /// <summary>
    /// Call when round ends.
    /// </summary>
    public void EndGame()
    {
        isGameActive = false;

        // Hide bar and text together
        if (manaBarPanel != null) manaBarPanel.SetActive(false);
        if (slackerText  != null) slackerText.gameObject.SetActive(false);

        Debug.Log($"{TAG} 🏁 Game ended. Bar and text hidden.");
    }

    /// <summary>
    /// Call every time a correct rep is detected.
    /// </summary>
    public void OnRepCompleted()
    {
        if (!isGameActive) return;

        // Reset inactivity timer on every rep
        inactivityTimer = 0f;
        Debug.Log($"{TAG} 💪 Rep completed! Inactivity timer reset.");

        repCounter++;
        Debug.Log($"{TAG} 🔢 Rep counter: {repCounter}/2");

        if (repCounter >= 2)
        {
            repCounter = 0;  // Reset counter
            IncreaseMana(1f);
            Debug.Log($"{TAG} ⬆️ +1 state! Current: {manaPoint}/{maxManaPoint}");
        }
    }

    /// <summary>
    /// Call on Play Again to reset bar for new round.
    /// </summary>
    public void ResetForNewRound()
    {
        manaPoint       = 0f;
        repCounter      = 0;
        inactivityTimer = 0f;
        penaltyTimer    = 0f;
        isGameActive    = false;

        UpdateManaBar();

        // Hide bar and text together
        if (manaBarPanel != null) manaBarPanel.SetActive(false);
        if (slackerText  != null) slackerText.gameObject.SetActive(false);

        Debug.Log($"{TAG} 🔄 Reset for new round. Bar and text hidden.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private void IncreaseMana(float amount)
    {
        manaPoint += amount;
        if (manaPoint > maxManaPoint)
            manaPoint = maxManaPoint;

        UpdateManaBar();
    }

    private void DecreaseMana(float amount)
    {
        manaPoint -= amount;
        if (manaPoint < 0f)
            manaPoint = 0f;

        UpdateManaBar();
    }

    private void ApplyCatPenalty()
    {
        if (getCatCount == null || setCatCount == null)
        {
            Debug.LogWarning($"{TAG} ⚠️ Cat count callbacks not set!");
            return;
        }

        int currentCats = getCatCount();
        int newCats     = Mathf.Max(0, currentCats - 5);  // Cannot go below 0
        setCatCount(newCats);

        Debug.Log($"{TAG} 😱 At 0/10! -5 cats penalty! {currentCats} → {newCats} cats");
    }

    /// <summary>
    /// Slides the ManaBar image inside ManaBarMask to show fill level.
    /// Taken from HealthSystem asset - uses localPosition sliding technique.
    /// </summary>
    private void UpdateManaBar()
    {
        if (currentManaBar == null)
        {
            Debug.LogWarning($"{TAG} ⚠️ currentManaBar Image not assigned!");
            return;
        }

        float ratio = manaPoint / maxManaPoint;
        currentManaBar.rectTransform.localPosition = new Vector3(
            currentManaBar.rectTransform.rect.width * ratio - currentManaBar.rectTransform.rect.width,
            0f,
            0f);

        Debug.Log($"{TAG} 📊 Bar updated: {manaPoint}/{maxManaPoint} (ratio: {ratio:F2})");
    }
}