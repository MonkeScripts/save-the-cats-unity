// IceEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the ice shard mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 30 s remaining, spawns a blue ice shard for LIMITED TIME (5 seconds).
//   • Plays crystal_appear sound while shard is visible.
//   • If user collects within 5 seconds: shows info panel, ice effect + time freeze.
//   • If user misses: shard disappears, power-up lost!
//
// IMPORTANT: This script uses its OWN AudioSource so it doesn't interrupt the fire sound!

using UnityEngine;
using System.Collections;

public class IceEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[ICE_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Ice Shard Prefabs")]
    [SerializeField] private GameObject iceShardPrefab;
    [SerializeField] private GameObject iceEffectPrefab;

    [Header("Ice Shard Settings")]
    [SerializeField] private float shardSpawnTime       = 30f;   // Seconds left when shard appears
    [SerializeField] private float shardDuration        = 5f;    // How long shard stays (limited time!)
    [SerializeField] private float shardSpawnDistance   = 0.15f; // Metres away from building
    [SerializeField] private float shardTriggerDistance = 0.30f; // Metres phone must be within to collect
    [SerializeField] private float freezeDuration       = 15f;   // Seconds time is paused
    [SerializeField] private float infoPanelDuration    = 5f;    // How long info panel shows

    [Header("Audio - USE A SEPARATE AUDIOSOURCE!")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   crystalAppearSound;
    [SerializeField] private AudioClip   collectSound;
    [SerializeField] private AudioClip   freezeSound;

    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.5f;
    [SerializeField] private float freezeVolume  = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Ice Shard";
    public event System.Action<int> OnBonusCatsEarned;
    public event System.Action<bool> OnTimePause;
    public event System.Action OnSpawnCat;
    public event System.Action<int, float> OnSetCatMultiplier;
    public event System.Action<string, float> OnShowInfoPanel;  // NEW

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedShard;
    private GameObject spawnedIceEffect;
    private bool       shardHasSpawned = false;
    private bool       shardCollected  = false;
    private bool       shardExpired    = false;
    private bool       isFreezing      = false;
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
            float dist = Vector3.Distance(phonePosition, spawnedShard.transform.position);
            Debug.Log($"{TAG} 📏 Phone↔Shard dist: {dist:F2} m (trigger < {shardTriggerDistance} m)");

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
        isFreezing      = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnShard(Vector3 buildingPosition)
    {
        shardHasSpawned = true;
        
        // Spawn shard to the LEFT of the building (negative X)
        Vector3 shardPos = buildingPosition + new Vector3(-shardSpawnDistance, 0f, 0f);
        spawnedShard = Instantiate(iceShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — ice shard spawned at {shardPos}.");
        Debug.Log($"{TAG} ⚠️ HURRY! Ice shard will disappear in {shardDuration} seconds!");

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
            Debug.Log($"{TAG} ⏰ TIME'S UP! Ice shard expired - power-up missed!");

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
                Debug.Log($"{TAG} ❄️ Ice shard disappeared (not collected in time).");
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
        Debug.Log($"{TAG} ❄️ Ice shard collected and destroyed.");

        // Play collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // NEW: Show info panel
        OnShowInfoPanel?.Invoke("ice", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Ice info panel requested for {infoPanelDuration}s.");

        // Spawn ice effect at building position
        spawnedIceEffect = Instantiate(iceEffectPrefab, buildingPosition, Quaternion.identity);
        Debug.Log($"{TAG} ❄️ Ice effect spawned at building: {buildingPosition}.");

        StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        isFreezing = true;
        Debug.Log($"{TAG} ❄️ TIME FROZEN for {freezeDuration} seconds! Do as many reps as you can!");

        // PAUSE TIME
        OnTimePause?.Invoke(true);
        Debug.Log($"{TAG} ⏸️ Time pause event fired (pause=true).");

        // Start freeze sound
        if (effectAudioSource != null && freezeSound != null)
        {
            effectAudioSource.clip = freezeSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = freezeVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Freeze sound started (volume {freezeVolume}).");
        }

        // Wait for freeze duration (using real time)
        yield return new WaitForSecondsRealtime(freezeDuration);

        // Stop freeze sound
        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Freeze sound stopped.");
        }

        // Destroy ice effect
        if (spawnedIceEffect != null)
        {
            Destroy(spawnedIceEffect);
            Debug.Log($"{TAG} ☀️ Ice effect ended.");
        }

        isFreezing = false;

        // RESUME TIME
        OnTimePause?.Invoke(false);
        Debug.Log($"{TAG} ▶️ Time pause event fired (pause=false). Game resumes!");
    }

    private void CleanUp()
    {
        StopAllCoroutines();
        shardTimerCoroutine = null;

        if (isFreezing)
        {
            OnTimePause?.Invoke(false);
            Debug.Log($"{TAG} ▶️ Cleanup: Resuming time.");
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
        if (spawnedIceEffect != null)
        {
            Destroy(spawnedIceEffect);
            Debug.Log($"{TAG} 🧹 Ice effect cleaned up.");
        }
    }
}