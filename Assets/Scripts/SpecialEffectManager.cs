// SpecialEffectManager.cs
// Attach this to the SAME GameObject as overall_game_play (XR Origin).
// It acts as the bridge between the main game script and all individual effect scripts.
// To add a new effect: create a new MonoBehaviour that implements ISpecialEffect,
// attach it to XR Origin, and it will be auto-discovered here.

using UnityEngine;
using System.Collections.Generic;

public class SpecialEffectManager : MonoBehaviour
{
    private const string TAG = "[EFFECT_MGR]";

    // ──────────────────────────────────────────────
    // Dependencies injected by the main game script
    // ──────────────────────────────────────────────
    private System.Func<Vector3> getBuildingPosition;
    private System.Action<int>   addCats;
    private System.Action<bool>  setTimePaused;
    private System.Action        spawnCat;        // NEW: Callback to spawn a cat visually
    private System.Func<bool>    isGameActive;
    private System.Func<float>   getTimeLeft;

    private List<ISpecialEffect> effects = new();
    private bool isInitialized = false;

    // ──────────────────────────────────────────────
    // Called ONCE by the main script during Awake/Start
    // ──────────────────────────────────────────────
    public void Initialize(
        System.Func<Vector3> getBuildingPos,
        System.Action<int>   addCatsCallback,
        System.Action<bool>  setTimePausedCallback,
        System.Action        spawnCatCallback,      // NEW parameter
        System.Func<bool>    gameActiveGetter,
        System.Func<float>   timeLeftGetter)
    {
        getBuildingPosition = getBuildingPos;
        addCats             = addCatsCallback;
        setTimePaused       = setTimePausedCallback;
        spawnCat            = spawnCatCallback;     // NEW
        isGameActive        = gameActiveGetter;
        getTimeLeft         = timeLeftGetter;

        // Auto-discover every ISpecialEffect component on this GameObject
        DiscoverEffects();
        
        isInitialized = true;
        Debug.Log($"{TAG} ✅ Initialization complete.");
    }

    private void DiscoverEffects()
    {
        effects.Clear();
        
        Debug.Log($"{TAG} 🔍 Searching for ISpecialEffect components on '{gameObject.name}'...");
        
        // Get ALL components on this GameObject
        var allComponents = GetComponents<MonoBehaviour>();
        Debug.Log($"{TAG} 📦 Found {allComponents.Length} MonoBehaviour components total.");
        
        foreach (var component in allComponents)
        {
            Debug.Log($"{TAG}    - {component.GetType().Name}");
        }
        
        // Now specifically look for ISpecialEffect
        var foundEffects = GetComponents<ISpecialEffect>();
        Debug.Log($"{TAG} 🎯 Found {foundEffects.Length} ISpecialEffect components.");
        
        foreach (var effect in foundEffects)
        {
            if (effect == null) 
            {
                Debug.LogWarning($"{TAG} ⚠️ Null effect found, skipping.");
                continue;
            }
            
            // Subscribe to ALL events
            effect.OnBonusCatsEarned += HandleBonusCats;
            effect.OnTimePause += HandleTimePause;
            effect.OnSpawnCat += HandleSpawnCat;  // NEW
            
            effects.Add(effect);
            Debug.Log($"{TAG} ✅ Registered effect: '{effect.EffectName}'");
        }

        Debug.Log($"{TAG} 📊 Total registered effects: {effects.Count}");
        
        if (effects.Count == 0)
        {
            Debug.LogError($"{TAG} ❌ NO EFFECTS FOUND! Make sure WaterBucketEffect and IceEffect scripts are attached to the same GameObject as this script!");
        }
    }

    // ──────────────────────────────────────────────
    // Called every frame by the main script's Update
    // ──────────────────────────────────────────────
    public void Tick()
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"{TAG} ⚠️ Tick called but not initialized!");
            return;
        }
        
        if (!isGameActive()) return;

        Vector3 buildingPos = getBuildingPosition();
        Vector3 phonePos    = Camera.main.transform.position;
        float   timeLeft    = getTimeLeft();

        // Debug: Log every 60 frames (roughly once per second)
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"{TAG} 🕐 Tick: timeLeft={timeLeft:F1}s, effects={effects.Count}, building={buildingPos}");
        }

        foreach (var effect in effects)
        {
            if (effect != null)
            {
                effect.Tick(timeLeft, buildingPos, phonePos);
            }
        }
    }

    // ──────────────────────────────────────────────
    // Lifecycle hooks forwarded from the main script
    // ──────────────────────────────────────────────
    public void OnRoundEnd()
    {
        Debug.Log($"{TAG} 🏁 OnRoundEnd called.");
        foreach (var effect in effects)
            effect?.OnRoundEnd();
    }

    public void ResetForNewRound()
    {
        Debug.Log($"{TAG} 🔄 ResetForNewRound called.");
        foreach (var effect in effects)
            effect?.ResetForNewRound();
    }

    // ──────────────────────────────────────────────
    // Private helpers - Event handlers
    // ──────────────────────────────────────────────
    private void HandleBonusCats(int amount)
    {
        Debug.Log($"{TAG} 🐱 HandleBonusCats received: +{amount}");
        addCats?.Invoke(amount);
        Debug.Log($"{TAG} 🐱 Forwarded +{amount} bonus cats to game script.");
    }

    private void HandleTimePause(bool isPaused)
    {
        Debug.Log($"{TAG} ⏱️ HandleTimePause received: isPaused={isPaused}");
        setTimePaused?.Invoke(isPaused);
        Debug.Log($"{TAG} ⏱️ Forwarded time pause ({isPaused}) to game script.");
    }

    private void HandleSpawnCat()
    {
        Debug.Log($"{TAG} 🐱 HandleSpawnCat received - spawning cat visually!");
        spawnCat?.Invoke();
    }

    private void OnDestroy()
    {
        foreach (var effect in effects)
        {
            if (effect != null)
            {
                effect.OnBonusCatsEarned -= HandleBonusCats;
                effect.OnTimePause -= HandleTimePause;
                effect.OnSpawnCat -= HandleSpawnCat;  // NEW
            }
        }
    }
}