// ISpecialEffect.cs
// Drop this file anywhere in your Assets/Scripts folder.
// Every special effect (Water, Ice, Fire, etc.) must implement this interface.
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
    /// <param name="timeLeft">Seconds remaining in the current round.</param>
    /// <param name="buildingPosition">World position of the locked AR anchor.</param>
    /// <param name="phonePosition">Current position of Camera.main.</param>
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
    /// Invoked by the effect when the player earns bonus cats.
    /// Subscribe to this in SpecialEffectManager to forward the bonus to the main game.
    /// </summary>
    event System.Action<int> OnBonusCatsEarned;
}