// GrowthEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the growth/multiplier mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 50 s remaining, spawns a green ice shard in front of the building.
//   • Plays crystal_appear sound while shard is visible.
//   • When the phone comes within trigger distance of the shard, it "collects" it.
//   • Collection: destroys shard, spawns green orb shield at building for 10 seconds.
//   • During shield time, every rep spawns 3 cats instead of 1!
//   • After shield duration, multiplier resets to 1x, gameplay continues normally.
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
    [SerializeField] private float shardSpawnTime       = 50f;   // Seconds left when shard appears
    [SerializeField] private float shardSpawnDistance   = 0.15f; // Metres in front of building
    [SerializeField] private float shardTriggerDistance = 0.30f; // Metres phone must be within to collect
    [SerializeField] private float shieldDuration       = 10f;   // Seconds the multiplier is active
    [SerializeField] private int   catMultiplier        = 3;     // How many cats per rep during shield
    [SerializeField] private float shardLifetime = 10f; // NEW: Shard disappears after 10s if not collected

    [Header("Audio")]
    [Tooltip("Drag a DIFFERENT AudioSource here, NOT the main game AudioSource. This prevents sound conflicts.")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   crystalAppearSound;  // Looping sound while shard is visible
    [SerializeField] private AudioClip   collectSound;        // One-shot SFX when shard collected
    [SerializeField] private AudioClip   shieldSound;         // Looping sound during shield time

    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.5f;
    [SerializeField] private float shieldVolume  = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Growth Shard";
    public event System.Action<int> OnBonusCatsEarned;   // Not used by this effect
    public event System.Action<bool> OnTimePause;        // Not used by this effect
    public event System.Action OnSpawnCat;               // Not used by this effect
    
    // NEW: Event to set cat multiplier (multiplier, duration)
    public event System.Action<int, float> OnSetCatMultiplier;

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedShard;
    private GameObject spawnedOrb;
    private bool       shardHasSpawned = false;
    private bool       shardCollected  = false;
    private bool       isShieldActive  = false;

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
        Vector3 shardPos = buildingPosition + new Vector3(0f, 0f, shardSpawnDistance);
        spawnedShard = Instantiate(greenShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — green shard spawned at {shardPos}.");

        // Start looping crystal appear sound
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip = crystalAppearSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started (volume {crystalVolume}).");
        }
        // --- NEW: Start the despawn timer ---
        StartCoroutine(ShardLifetimeRoutine());
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
        Debug.Log($"{TAG} 💚 Green shard collected and destroyed.");

        // Play one-shot collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // Spawn green orb shield at building position
        spawnedOrb = Instantiate(greenOrbPrefab, buildingPosition, Quaternion.identity);
        Debug.Log($"{TAG} 💚 Green orb shield spawned at building: {buildingPosition}.");

        // Start shield timer
        StartCoroutine(ShieldRoutine());
    }

    private IEnumerator ShieldRoutine()
    {
        isShieldActive = true;
        Debug.Log($"{TAG} 💚 GROWTH SHIELD ACTIVE for {shieldDuration} seconds! Every rep = {catMultiplier} cats!");

        // ACTIVATE MULTIPLIER - notify the main game
        OnSetCatMultiplier?.Invoke(catMultiplier, shieldDuration);
        Debug.Log($"{TAG} 🚀 Cat multiplier set to x{catMultiplier} for {shieldDuration}s.");

        // Start looping shield sound
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

        // RESET MULTIPLIER - notify the main game (multiplier back to 1)
        OnSetCatMultiplier?.Invoke(1, 0f);
        Debug.Log($"{TAG} ▶️ Cat multiplier reset to x1. Game resumes normally!");
    }

    private IEnumerator ShardLifetimeRoutine()
    {
        yield return new WaitForSeconds(shardLifetime);

        // If shard hasn't been collected yet, despawn it
        if (spawnedShard != null && !shardCollected)
        {
            Debug.Log($"{TAG} ⏱️ Growth Shard lifetime expired. Despawning!");

            // Stop the appear sound
            if (effectAudioSource != null && effectAudioSource.isPlaying)
            {
                effectAudioSource.Stop();
            }

            Destroy(spawnedShard);
            // shardHasSpawned stays true so it doesn't pop up again this round
        }
    }

    private void CleanUp()
    {
        // Stop all coroutines
        StopAllCoroutines();

        // If shield was active, reset multiplier
        if (isShieldActive)
        {
            OnSetCatMultiplier?.Invoke(1, 0f);
            Debug.Log($"{TAG} ▶️ Cleanup: Resetting multiplier to x1.");
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
        if (spawnedOrb != null)
        {
            Destroy(spawnedOrb);
            Debug.Log($"{TAG} 🧹 Orb cleaned up.");
        }
    }
}