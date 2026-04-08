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
    /// Use this to poll spawn timers, check proximity, etc.
    /// </summary>
    void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition);

    /// <summary>
    /// Called when the round ends (time up or player quits).
    /// Clean up any spawned GameObjects here.
    /// </summary>
    void OnRoundEnd();

    /// <summary>
    /// Called when the player starts a brand-new round (PlayAgain).
    /// Reset ALL internal state flags so the effect can trigger again.
    /// </summary>
    void ResetForNewRound();

    /// <summary>
    /// Invoked by the effect when the player earns bonus cats (adds to counter without spawning).
    /// </summary>
    event System.Action<int> OnBonusCatsEarned;
    
    /// <summary>
    /// Invoked by the effect when time should be paused or resumed.
    /// true = pause time, false = resume time.
    /// </summary>
    event System.Action<bool> OnTimePause;
    
    /// <summary>
    /// Invoked by the effect when a cat should be spawned visually.
    /// </summary>
    event System.Action OnSpawnCat;
    
    /// <summary>
    /// Invoked by the effect when cat spawn multiplier should change.
    /// Parameters: (int multiplier, float duration)
    /// </summary>
    event System.Action<int, float> OnSetCatMultiplier;

    /// <summary>
    /// Invoked by the effect when an information panel should be shown.
    /// Parameters: (string panelName, float duration)
    /// panelName = "water", "ice", or "growth"
    /// duration = how long to show the panel
    /// </summary>
    event System.Action<string, float> OnShowInfoPanel;
}