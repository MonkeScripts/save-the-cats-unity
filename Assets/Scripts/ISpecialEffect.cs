// ISpecialEffect.cs
// Drop this file anywhere in your Assets/Scripts folder.
// Every special effect (Water, Ice, Growth, etc.) must implement this interface.
// The SpecialEffectManager uses this to communicate with all effects generically.

using UnityEngine;

public interface ISpecialEffect
{
    /// <summary>Human-readable name shown in logs and debug UI.</summary>
    string EffectName { get; }

    /// <summary>
    /// Called by SpecialEffectManager every Update() while the game is active.
    /// </summary>
    void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition);

    /// <summary>
    /// Called when the round ends (time up or player quits).
    /// </summary>
    void OnRoundEnd();

    /// <summary>
    /// Called when the player starts a brand-new round (PlayAgain).
    /// </summary>
    void ResetForNewRound();

    /// <summary>
    /// Invoked when the player earns bonus cats.
    /// </summary>
    event System.Action<int> OnBonusCatsEarned;

    /// <summary>
    /// Invoked when time should be paused or resumed.
    /// true = pause time, false = resume time.
    /// </summary>
    event System.Action<bool> OnTimePause;

    /// <summary>
    /// Invoked when a cat should be spawned visually.
    /// </summary>
    event System.Action OnSpawnCat;

    /// <summary>
    /// Invoked when cat spawn multiplier should change.
    /// Parameters: (int multiplier, float duration)
    /// </summary>
    event System.Action<int, float> OnSetCatMultiplier;

    /// <summary>
    /// Invoked when an information panel should be shown.
    /// Parameters: (string panelName, float duration)
    /// panelName = "water", "ice", or "growth"
    /// </summary>
    event System.Action<string, float> OnShowInfoPanel;

    /// <summary>
    /// Invoked when a shard/bucket appears on screen.
    /// Used by tutorial mode to show IceShardTutorialPanel.
    /// </summary>
    event System.Action OnShardAppeared;

    /// <summary>
    /// Invoked when a shard/bucket is collected or expired.
    /// Used by tutorial mode to hide IceShardTutorialPanel.
    /// </summary>
    event System.Action OnShardGone;
}