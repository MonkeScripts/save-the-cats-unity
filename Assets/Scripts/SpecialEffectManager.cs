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
    private System.Func<Vector3>         getBuildingPosition;
    private System.Action<int>           addCats;
    private System.Action<bool>          setTimePaused;
    private System.Action                spawnCat;
    private System.Action<int, float>    setCatMultiplier;
    private System.Action<string, float> showInfoPanel;  // NEW: Callback to show info panel
    private System.Func<bool>            isGameActive;
    private System.Func<float>           getTimeLeft;

    private List<ISpecialEffect> effects = new();
    private bool isInitialized = false;

    // ──────────────────────────────────────────────
    // Called ONCE by the main script during Awake/Start
    // ──────────────────────────────────────────────
    public void Initialize(
        System.Func<Vector3>         getBuildingPos,
        System.Action<int>           addCatsCallback,
        System.Action<bool>          setTimePausedCallback,
        System.Action                spawnCatCallback,
        System.Action<int, float>    setCatMultiplierCallback,
        System.Action<string, float> showInfoPanelCallback,  // NEW parameter
        System.Func<bool>            gameActiveGetter,
        System.Func<float>           timeLeftGetter)
    {
        getBuildingPosition = getBuildingPos;
        addCats             = addCatsCallback;
        setTimePaused       = setTimePausedCallback;
        spawnCat            = spawnCatCallback;
        setCatMultiplier    = setCatMultiplierCallback;
        showInfoPanel       = showInfoPanelCallback;  // NEW
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
        
        var allComponents = GetComponents<MonoBehaviour>();
        Debug.Log($"{TAG} 📦 Found {allComponents.Length} MonoBehaviour components total.");
        
        foreach (var component in allComponents)
        {
            Debug.Log($"{TAG}    - {component.GetType().Name}");
        }
        
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
            effect.OnSpawnCat += HandleSpawnCat;
            effect.OnSetCatMultiplier += HandleSetCatMultiplier;
            effect.OnShowInfoPanel += HandleShowInfoPanel;  // NEW
            
            effects.Add(effect);
            Debug.Log($"{TAG} ✅ Registered effect: '{effect.EffectName}'");
        }

        Debug.Log($"{TAG} 📊 Total registered effects: {effects.Count}");
        
        if (effects.Count == 0)
        {
            Debug.LogError($"{TAG} ❌ NO EFFECTS FOUND! Make sure effect scripts are attached to the same GameObject as this script!");
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

        //Disable all power-ups for Climbers and AltArmLeg
        bool isSpecialMode = ClimberAltArmLegGameMode.Instance != null && 
                            ClimberAltArmLegGameMode.Instance.IsSpecialMode;
        if (isSpecialMode)
        {
            Debug.Log($"[EFFECT_MGR] ⚠️ Special mode active - power-ups disabled.");
            return;
        }

        Vector3 buildingPos = getBuildingPosition();
        Vector3 phonePos    = Camera.main.transform.position;
        float   timeLeft    = getTimeLeft();

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

    private void HandleSetCatMultiplier(int multiplier, float duration)
    {
        Debug.Log($"{TAG} 🚀 HandleSetCatMultiplier received: x{multiplier} for {duration}s");
        setCatMultiplier?.Invoke(multiplier, duration);
        Debug.Log($"{TAG} 🚀 Forwarded multiplier x{multiplier} to game script.");
    }

    // NEW: Handler for info panel display
    private void HandleShowInfoPanel(string panelName, float duration)
    {
        Debug.Log($"{TAG} 📋 HandleShowInfoPanel received: panel='{panelName}' for {duration}s");
        showInfoPanel?.Invoke(panelName, duration);
        Debug.Log($"{TAG} 📋 Forwarded info panel request to game script.");
    }

    private void OnDestroy()
    {
        foreach (var effect in effects)
        {
            if (effect != null)
            {
                effect.OnBonusCatsEarned -= HandleBonusCats;
                effect.OnTimePause -= HandleTimePause;
                effect.OnSpawnCat -= HandleSpawnCat;
                effect.OnSetCatMultiplier -= HandleSetCatMultiplier;
                effect.OnShowInfoPanel -= HandleShowInfoPanel;  // NEW
            }
        }
    }
}