// SpecialEffectManager.cs
// Attach this to the SAME GameObject as scan_qrcode_exercise_final (XR Origin).
// It acts as the bridge between the main game script and all individual effect scripts.
// To add a new effect: create a new MonoBehaviour that implements ISpecialEffect,
// attach it to XR Origin, and it will be auto-discovered here.

using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class SpecialEffectManager : MonoBehaviour
{
    private const string TAG = "[EFFECT]";

    // ──────────────────────────────────────────────
    // Dependencies injected by the main game script
    // ──────────────────────────────────────────────
    private System.Func<Vector3> getBuildingPosition;   // Delegate to read anchor position
    private System.Func<int>     getCatCount;           // Delegate to read current cat count
    private System.Action<int>   addCats;               // Delegate to add bonus cats
    private System.Func<bool>    isGameActive;          // Delegate to check game state
    private System.Func<float>   getTimeLeft;           // Delegate to read timer

    private List<ISpecialEffect> effects = new();

    // ──────────────────────────────────────────────
    // Called ONCE by the main script during Awake/Start
    // ──────────────────────────────────────────────
    public void Initialize(
        System.Func<Vector3> getBuildingPos,
        System.Action<int>   addCatsCallback,
        System.Func<bool>    gameActiveGetter,
        System.Func<float>   timeLeftGetter)
    {
        getBuildingPosition = getBuildingPos;
        addCats             = addCatsCallback;
        isGameActive        = gameActiveGetter;
        getTimeLeft         = timeLeftGetter;

        // Auto-discover every ISpecialEffect component on this GameObject
        effects.Clear();
        Debug.Log($"[CHECK] Searching for effects on {gameObject.name}...");
        foreach (var effect in GetComponents<ISpecialEffect>())
        {
            effect.OnBonusCatsEarned += HandleBonusCats;
            effects.Add(effect);
            Debug.Log($"{TAG} Registered effect: {effect.EffectName}");
        }

        Debug.Log($"{TAG} Initialized with {effects.Count} effect(s).");
    }

    // ──────────────────────────────────────────────
    // Called every frame by the main script's Update
    // ──────────────────────────────────────────────
    public void Tick()
    {
        if (!isGameActive()) return;

        Vector3 buildingPos = getBuildingPosition();
        Vector3 phonePos    = Camera.main.transform.position;
        float   timeLeft    = getTimeLeft();

        foreach (var effect in effects)
            effect.Tick(timeLeft, buildingPos, phonePos);
    }

    // ──────────────────────────────────────────────
    // Lifecycle hooks forwarded from the main script
    // ──────────────────────────────────────────────
    public void OnRoundEnd()
    {
        foreach (var effect in effects)
            effect.OnRoundEnd();
    }

    public void ResetForNewRound()
    {
        foreach (var effect in effects)
            effect.ResetForNewRound();
        Debug.Log($"{TAG} All effects reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void HandleBonusCats(int amount)
    {
        addCats?.Invoke(amount);
        Debug.Log($"{TAG} Forwarded +{amount} bonus cats to game script.");
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        foreach (var effect in effects)
            effect.OnBonusCatsEarned -= HandleBonusCats;
    }
}