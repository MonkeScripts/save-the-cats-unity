// GrowthEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the growth/multiplier mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 50 s remaining, spawns a green ice shard for LIMITED TIME (5 seconds).
//   • Plays crystal_appear sound while shard is visible.
//   • If user collects within 5 seconds: shows info panel, green orb + x3 multiplier.
//   • If user misses: shard disappears, power-up lost!
//
// IMPORTANT: This script uses its OWN AudioSource so it doesn't interrupt the fire sound!

using UnityEngine;
using System.Collections;

public class GrowthEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[GROWTH_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Growth Shard Prefabs")]
    [SerializeField] private GameObject greenShardPrefab;    // The collectible green ice shard
    [SerializeField] private GameObject greenOrbPrefab;      // Green orb shield VFX around building

    [Header("Growth Shard Settings")]
    [SerializeField] private float shardSpawnTime       = 35f;   // Seconds left when shard appears
    [SerializeField] private float shardDuration        = 7f;    // How long shard stays (limited time!)
    [SerializeField] private float shardSpawnDistance   = 1.0f; // Metres in front of building
    [SerializeField] private float shardTriggerDistance = 0.70f; // Metres phone must be within to collect
    [SerializeField] private float shieldDuration       = 10f;   // Seconds the multiplier is active
    [SerializeField] private int   catMultiplier        = 3;     // How many cats per rep during shield
    [SerializeField] private float infoPanelDuration    = 5f;    // How long info panel shows

    [Header("Audio - USE A SEPARATE AUDIOSOURCE!")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   crystalAppearSound;
    [SerializeField] private AudioClip   collectSound;
    [SerializeField] private AudioClip   shieldSound;

    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.8f;
    [SerializeField] private float shieldVolume  = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Growth Shard";
    public event System.Action<int> OnBonusCatsEarned;
    public event System.Action<bool> OnTimePause;
    public event System.Action OnSpawnCat;
    public event System.Action<int, float> OnSetCatMultiplier;
    public event System.Action<string, float> OnShowInfoPanel;  // NEW
    public event System.Action OnShardAppeared;
    public event System.Action OnShardGone;

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedShard;
    private GameObject spawnedOrb;
    private bool       shardHasSpawned = false;
    private bool       shardCollected  = false;
    private bool       shardExpired    = false;
    private bool       isShieldActive  = false;
    private Coroutine  shardTimerCoroutine;

    // ──────────────────────────────────────────────
    // ISpecialEffect — Tick
    // ──────────────────────────────────────────────
    public void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition)
    {
        // Phase 1: Spawn the shard when the timer hits the threshold
        if (!shardHasSpawned && timeLeft <= shardSpawnTime)
        {
            SpawnShard(buildingPosition);
        }

        // Phase 2: Poll for phone proximity (only if shard exists and not expired)
        if (shardHasSpawned && !shardCollected && !shardExpired && spawnedShard != null)
        {
            float dx = phonePosition.x - spawnedShard.transform.position.x;
            float dz = phonePosition.z - spawnedShard.transform.position.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            Debug.Log($"{TAG} 📏 Phone↔Shard XZ dist: {dist:F2} m (trigger < {shardTriggerDistance} m)");

            if (dist < shardTriggerDistance)
            {
                Debug.Log($"{TAG} ✅ Phone near shard — collecting!");
                CollectShard(buildingPosition);
            }
        }
    }

    // ──────────────────────────────────────────────
    // ISpecialEffect — lifecycle hooks
    // ──────────────────────────────────────────────
    public void OnRoundEnd()
    {
        CleanUp();
    }

    public void ResetForNewRound()
    {
        CleanUp();
        shardHasSpawned = false;
        shardCollected  = false;
        shardExpired    = false;
        isShieldActive  = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnShard(Vector3 buildingPosition)
    {
        shardHasSpawned = true;
        
        // Spawn shard in FRONT of the building (positive Z)
        Vector3 shardPos = buildingPosition + new Vector3(0f, 0.4f, shardSpawnDistance);
        spawnedShard = Instantiate(greenShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — green shard spawned at {shardPos}.");
        Debug.Log($"{TAG} ⚠️ HURRY! Green shard will disappear in {shardDuration} seconds!");

        // Start crystal appear sound
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip = crystalAppearSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started (volume {crystalVolume}).");
        }

        // Start the shard expiration timer
        shardTimerCoroutine = StartCoroutine(ShardExpirationRoutine());
    }

    private IEnumerator ShardExpirationRoutine()
    {
        yield return new WaitForSeconds(shardDuration);

        // If shard wasn't collected, it expires
        if (!shardCollected)
        {
            shardExpired = true;
            Debug.Log($"{TAG} ⏰ TIME'S UP! Green shard expired - power-up missed!");

            // Stop crystal sound
            if (effectAudioSource != null && effectAudioSource.isPlaying)
            {
                effectAudioSource.Stop();
                effectAudioSource.loop = false;
                Debug.Log($"{TAG} 🔇 Crystal appear sound stopped (expired).");
            }

            // Destroy shard
            if (spawnedShard != null)
            {
                Destroy(spawnedShard);
                Debug.Log($"{TAG} 💚 Green shard disappeared (not collected in time).");
            }
        }
    }

    private void CollectShard(Vector3 buildingPosition)
    {
        shardCollected = true;

        // Stop the expiration timer
        if (shardTimerCoroutine != null)
        {
            StopCoroutine(shardTimerCoroutine);
            shardTimerCoroutine = null;
        }

        // Stop crystal appear sound
        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Crystal appear sound stopped.");
        }

        // Destroy shard
        Destroy(spawnedShard);
        Debug.Log($"{TAG} 💚 Green shard collected and destroyed.");

        // Play collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // NEW: Show info panel
        OnShowInfoPanel?.Invoke("growth", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Growth info panel requested for {infoPanelDuration}s.");

        // Spawn green orb shield at building position
        spawnedOrb = Instantiate(greenOrbPrefab, buildingPosition, Quaternion.identity);
        Debug.Log($"{TAG} 💚 Green orb shield spawned at building: {buildingPosition}.");

        StartCoroutine(ShieldRoutine());
    }

    private IEnumerator ShieldRoutine()
    {
        isShieldActive = true;
        Debug.Log($"{TAG} 💚 GROWTH SHIELD ACTIVE for {shieldDuration} seconds! Every rep = {catMultiplier} cats!");

        // ACTIVATE MULTIPLIER
        OnSetCatMultiplier?.Invoke(catMultiplier, shieldDuration);
        Debug.Log($"{TAG} 🚀 Cat multiplier set to x{catMultiplier} for {shieldDuration}s.");

        // Start shield sound
        if (effectAudioSource != null && shieldSound != null)
        {
            effectAudioSource.clip = shieldSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = shieldVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Shield sound started (volume {shieldVolume}).");
        }

        // Wait for shield duration
        yield return new WaitForSeconds(shieldDuration);

        // Stop shield sound
        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Shield sound stopped.");
        }

        // Destroy orb
        if (spawnedOrb != null)
        {
            Destroy(spawnedOrb);
            Debug.Log($"{TAG} 💚 Green orb shield disappeared.");
        }

        isShieldActive = false;

        // RESET MULTIPLIER
        OnSetCatMultiplier?.Invoke(1, 0f);
        Debug.Log($"{TAG} ▶️ Cat multiplier reset to x1. Game resumes normally!");
    }

    private void CleanUp()
    {
        StopAllCoroutines();
        shardTimerCoroutine = null;

        // If shield was active, reset multiplier
        if (isShieldActive)
        {
            OnSetCatMultiplier?.Invoke(1, 0f);
            Debug.Log($"{TAG} ▶️ Cleanup: Resetting multiplier to x1.");
        }

        if (effectAudioSource != null)
        {
            if (effectAudioSource.isPlaying)
                effectAudioSource.Stop();
            effectAudioSource.loop = false;
            effectAudioSource.volume = 1f;
            Debug.Log($"{TAG} 🔇 Audio stopped during cleanup.");
        }

        if (spawnedShard != null)
        {
            Destroy(spawnedShard);
            Debug.Log($"{TAG} 🧹 Shard cleaned up.");
        }
        if (spawnedOrb != null)
        {
            Destroy(spawnedOrb);
            Debug.Log($"{TAG} 🧹 Orb cleaned up.");
        }
    }
}