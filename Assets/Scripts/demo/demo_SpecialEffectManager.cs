// demo_SpecialEffectManager.cs
// Attach to XR Origin in TUTORIAL SCENE.
// Same as SpecialEffectManager but with added tutorial panel callbacks
// for showing/hiding the IceShardTutorialPanel when any shard appears.

using UnityEngine;
using System.Collections.Generic;

public class demo_SpecialEffectManager : MonoBehaviour
{
    private const string TAG = "[DEMO_EFFECT_MGR]";

    // ──────────────────────────────────────────────
    // Dependencies injected by the main game script
    // ──────────────────────────────────────────────
    private System.Func<Vector3>         getBuildingPosition;
    private System.Action<int>           addCats;
    private System.Action<bool>          setTimePaused;
    private System.Action                spawnCat;
    private System.Action<int, float>    setCatMultiplier;
    private System.Action<string, float> showInfoPanel;
    private System.Action                showShardTutorial;  // NEW
    private System.Action                hideShardTutorial;  // NEW
    private System.Func<bool>            isGameActive;
    private System.Func<float>           getTimeLeft;

    private List<ISpecialEffect> effects = new();
    private bool isInitialized = false;

    // ──────────────────────────────────────────────
    // Called ONCE by the main script during Awake
    // ──────────────────────────────────────────────
    public void Initialize(
        System.Func<Vector3>         getBuildingPos,
        System.Action<int>           addCatsCallback,
        System.Action<bool>          setTimePausedCallback,
        System.Action                spawnCatCallback,
        System.Action<int, float>    setCatMultiplierCallback,
        System.Action<string, float> showInfoPanelCallback,
        System.Action                showShardTutorialCallback,  // NEW
        System.Action                hideShardTutorialCallback,  // NEW
        System.Func<bool>            gameActiveGetter,
        System.Func<float>           timeLeftGetter)
    {
        getBuildingPosition = getBuildingPos;
        addCats             = addCatsCallback;
        setTimePaused       = setTimePausedCallback;
        spawnCat            = spawnCatCallback;
        setCatMultiplier    = setCatMultiplierCallback;
        showInfoPanel       = showInfoPanelCallback;
        showShardTutorial   = showShardTutorialCallback;  // NEW
        hideShardTutorial   = hideShardTutorialCallback;  // NEW
        isGameActive        = gameActiveGetter;
        getTimeLeft         = timeLeftGetter;

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
            effect.OnBonusCatsEarned    += HandleBonusCats;
            effect.OnTimePause          += HandleTimePause;
            effect.OnSpawnCat           += HandleSpawnCat;
            effect.OnSetCatMultiplier   += HandleSetCatMultiplier;
            effect.OnShowInfoPanel      += HandleShowInfoPanel;
            effect.OnShardAppeared      += HandleShardAppeared;  // NEW
            effect.OnShardGone          += HandleShardGone;      // NEW

            effects.Add(effect);
            Debug.Log($"{TAG} ✅ Registered effect: '{effect.EffectName}'");
        }

        Debug.Log($"{TAG} 📊 Total registered effects: {effects.Count}");

        if (effects.Count == 0)
        {
            Debug.LogError($"{TAG} ❌ NO EFFECTS FOUND!");
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

        // Disable all power-ups for PushUp and SitUp
        bool isSpecialMode = PushUpSitUpGameMode.Instance != null &&
                             PushUpSitUpGameMode.Instance.IsSpecialMode;
        if (isSpecialMode)
        {
            Debug.Log($"{TAG} ⚠️ Special mode active - power-ups disabled.");
            return;
        }

        Vector3 buildingPos = getBuildingPosition();
        Vector3 phonePos    = Camera.main.transform.position;
        float   timeLeft    = getTimeLeft();

        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"{TAG} 🕐 Tick: timeLeft={timeLeft:F1}s, effects={effects.Count}");
        }

        foreach (var effect in effects)
        {
            if (effect != null)
                effect.Tick(timeLeft, buildingPos, phonePos);
        }
    }

    // ──────────────────────────────────────────────
    // Lifecycle hooks
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
    // Event handlers
    // ──────────────────────────────────────────────
    private void HandleBonusCats(int amount)
    {
        Debug.Log($"{TAG} 🐱 HandleBonusCats: +{amount}");
        addCats?.Invoke(amount);
    }

    private void HandleTimePause(bool isPaused)
    {
        Debug.Log($"{TAG} ⏱️ HandleTimePause: isPaused={isPaused}");
        setTimePaused?.Invoke(isPaused);
    }

    private void HandleSpawnCat()
    {
        Debug.Log($"{TAG} 🐱 HandleSpawnCat received!");
        spawnCat?.Invoke();
    }

    private void HandleSetCatMultiplier(int multiplier, float duration)
    {
        Debug.Log($"{TAG} 🚀 HandleSetCatMultiplier: x{multiplier} for {duration}s");
        setCatMultiplier?.Invoke(multiplier, duration);
    }

    private void HandleShowInfoPanel(string panelName, float duration)
    {
        Debug.Log($"{TAG} 📋 HandleShowInfoPanel: panel='{panelName}' for {duration}s");
        showInfoPanel?.Invoke(panelName, duration);
    }

    // NEW: Shard appeared — show tutorial panel
    private void HandleShardAppeared()
    {
        Debug.Log($"{TAG} 💎 Shard appeared! Showing tutorial panel.");
        showShardTutorial?.Invoke();
    }

    // NEW: Shard gone (collected or expired) — hide tutorial panel
    private void HandleShardGone()
    {
        Debug.Log($"{TAG} 💎 Shard gone! Hiding tutorial panel.");
        hideShardTutorial?.Invoke();
    }

    private void OnDestroy()
    {
        foreach (var effect in effects)
        {
            if (effect != null)
            {
                effect.OnBonusCatsEarned  -= HandleBonusCats;
                effect.OnTimePause        -= HandleTimePause;
                effect.OnSpawnCat         -= HandleSpawnCat;
                effect.OnSetCatMultiplier -= HandleSetCatMultiplier;
                effect.OnShowInfoPanel    -= HandleShowInfoPanel;
                effect.OnShardAppeared    -= HandleShardAppeared;  // NEW
                effect.OnShardGone        -= HandleShardGone;      // NEW
            }
        }
    }
}