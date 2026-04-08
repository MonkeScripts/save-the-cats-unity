// IceEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the ice shard mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At specified time remaining, spawns a blue ice shard 0.15m away from the building.
//   • Plays crystal_appear sound while shard is visible.
//   • When the phone comes within trigger distance of the shard, it "collects" it.
//   • Collection: destroys shard, spawns ice VFX around building, PAUSES TIME for 15 seconds.
//   • During freeze time, user can do as many reps as possible.
//   • After freeze duration, time resumes and gameplay continues normally.
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
    [SerializeField] private GameObject iceShardPrefab;    // The collectible blue ice shard
    [SerializeField] private GameObject iceEffectPrefab;   // VFX that spawns around building when collected

    [Header("Ice Shard Settings")]
    [SerializeField] private float shardSpawnTime       = 30f;   // Seconds left when shard appears
    [SerializeField] private float shardSpawnDistance   = 0.15f; // Metres away from building
    [SerializeField] private float shardTriggerDistance = 0.30f; // Metres phone must be within to collect
    [SerializeField] private float freezeDuration       = 15f;   // Seconds time is paused

    [Header("Audio - USE A SEPARATE AUDIOSOURCE!")]
    [Tooltip("Drag a DIFFERENT AudioSource here, NOT the main game AudioSource. This prevents sound conflicts.")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   crystalAppearSound;  // Looping sound while shard is visible
    [SerializeField] private AudioClip   collectSound;        // One-shot SFX when shard collected
    [SerializeField] private AudioClip   freezeSound;         // Looping sound during freeze time

    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.5f;
    [SerializeField] private float freezeVolume  = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Ice Shard";
    public event System.Action<int> OnBonusCatsEarned;  // Not used by this effect, but required by interface
    public event System.Action<bool> OnTimePause;       // Used to pause/resume time
    public event System.Action OnSpawnCat;              // Not used by this effect, but required by interface

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedShard;
    private GameObject spawnedIceEffect;
    private bool       shardHasSpawned = false;
    private bool       shardCollected  = false;
    private bool       isFreezing      = false;

    // ──────────────────────────────────────────────
    // ISpecialEffect — Tick (called every frame by SpecialEffectManager)
    // ──────────────────────────────────────────────
    public void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition)
    {
        // Phase 1: Spawn the shard when the timer hits the threshold
        if (!shardHasSpawned && timeLeft <= shardSpawnTime)
        {
            SpawnShard(buildingPosition);
        }

        // Phase 2: Poll for phone proximity once shard is alive
        if (shardHasSpawned && !shardCollected && spawnedShard != null)
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
        isFreezing      = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnShard(Vector3 buildingPosition)
    {
        shardHasSpawned = true;
        
        // Spawn shard to the LEFT of the building (negative X) to differentiate from water bucket
        Vector3 shardPos = buildingPosition + new Vector3(-shardSpawnDistance, 0f, 0f);
        spawnedShard = Instantiate(iceShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — ice shard spawned at {shardPos}.");

        // Start looping crystal appear sound
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip = crystalAppearSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started (volume {crystalVolume}).");
        }
    }

    private void CollectShard(Vector3 buildingPosition)
    {
        shardCollected = true;

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

        // Play one-shot collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // Spawn ice effect at building position (same coordinates as building)
        spawnedIceEffect = Instantiate(iceEffectPrefab, buildingPosition, Quaternion.identity);
        Debug.Log($"{TAG} ❄️ Ice effect spawned at building: {buildingPosition}.");

        // Start freeze timer
        StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        isFreezing = true;
        Debug.Log($"{TAG} ❄️ TIME FROZEN for {freezeDuration} seconds! Do as many reps as you can!");

        // PAUSE TIME - notify the main game
        OnTimePause?.Invoke(true);
        Debug.Log($"{TAG} ⏸️ Time pause event fired (pause=true).");

        // Start looping freeze sound
        if (effectAudioSource != null && freezeSound != null)
        {
            effectAudioSource.clip = freezeSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = freezeVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Freeze sound started (volume {freezeVolume}).");
        }

        // Wait for freeze duration (using real time, not game time)
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

        // RESUME TIME - notify the main game
        OnTimePause?.Invoke(false);
        Debug.Log($"{TAG} ▶️ Time pause event fired (pause=false). Game resumes!");
    }

    private void CleanUp()
    {
        // Stop all coroutines
        StopAllCoroutines();

        // If we were freezing, make sure to resume time
        if (isFreezing)
        {
            OnTimePause?.Invoke(false);
            Debug.Log($"{TAG} ▶️ Cleanup: Resuming time.");
        }

        // Stop any playing audio and reset volume
        if (effectAudioSource != null)
        {
            if (effectAudioSource.isPlaying)
            {
                effectAudioSource.Stop();
            }
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