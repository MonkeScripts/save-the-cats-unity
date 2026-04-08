// WaterBucketEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the water bucket mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 15 s remaining, spawns a water bucket 15 cm to the right of the building.
//   • Plays crystal_appear sound while bucket is visible (on its OWN AudioSource).
//   • When the phone comes within 0.3 m of the bucket, it "collects" it.
//   • Collection: destroys bucket, stops crystal sound, spawns rain VFX, plays rain sound.
//   • Rain auto-despawns after rainDuration seconds, then SPAWNS CATS VISUALLY (not just +5).
//
// IMPORTANT: This script uses its OWN AudioSource so it doesn't interrupt the fire sound!

using UnityEngine;
using System.Collections;

public class WaterBucketEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[WATER_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Water Bucket Prefabs")]
    [SerializeField] private GameObject waterBucketPrefab;
    [SerializeField] private GameObject rainEffectPrefab;

    [Header("Water Bucket Settings")]
    [SerializeField] private float bucketSpawnTime       = 15f;   // Seconds left when bucket appears
    [SerializeField] private float bucketSpawnDistance   = 0.15f; // Metres right of building
    [SerializeField] private float bucketTriggerDistance = 0.30f; // Metres phone must be within
    [SerializeField] private float rainDuration          = 5f;    // Seconds rain lasts
    [SerializeField] private int   bonusCatSpawns        = 15;    // Number of cats to spawn visually
    [SerializeField] private float catSpawnInterval      = 0.2f;  // Seconds between each cat spawn

    [Header("Audio")]
    [Tooltip("Drag a DIFFERENT AudioSource here, NOT the main game AudioSource. This prevents sound conflicts.")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   collectSound;           // One-shot SFX when bucket collected
    [SerializeField] private AudioClip   crystalAppearSound;     // Looping sound while bucket is visible
    [SerializeField] private AudioClip   rainSound;              // Looping sound while rain is active
    
    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.5f;
    [SerializeField] private float rainVolume    = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Water Bucket";
    public event System.Action<int> OnBonusCatsEarned;      // Not used anymore, but required by interface
    public event System.Action<bool> OnTimePause;           // Not used by this effect, but required by interface
    
    // NEW: Event to trigger visual cat spawning
    public event System.Action OnSpawnCat;

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedBucket;
    private GameObject spawnedRain;
    private bool       bucketHasSpawned = false;
    private bool       bucketCollected  = false;
    private bool       isRaining        = false;

    // ──────────────────────────────────────────────
    // ISpecialEffect — Tick (called every frame by SpecialEffectManager)
    // ──────────────────────────────────────────────
    public void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition)
    {
        // Phase 1: Spawn the bucket when the timer hits the threshold
        if (!bucketHasSpawned && timeLeft <= bucketSpawnTime)
        {
            SpawnBucket(buildingPosition);
        }

        // Phase 2: Poll for phone proximity once bucket is alive
        if (bucketHasSpawned && !bucketCollected && spawnedBucket != null)
        {
            float dist = Vector3.Distance(phonePosition, spawnedBucket.transform.position);
            Debug.Log($"{TAG} 📏 Phone↔Bucket dist: {dist:F2} m (trigger < {bucketTriggerDistance} m)");

            if (dist < bucketTriggerDistance)
            {
                Debug.Log($"{TAG} ✅ Phone near bucket — collecting!");
                CollectBucket(buildingPosition);
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
        bucketHasSpawned = false;
        bucketCollected  = false;
        isRaining        = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnBucket(Vector3 buildingPosition)
    {
        bucketHasSpawned = true;
        Vector3 bucketPos = buildingPosition + new Vector3(bucketSpawnDistance, 0f, 0f);
        spawnedBucket = Instantiate(waterBucketPrefab, bucketPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {bucketSpawnTime}s left — bucket spawned at {bucketPos}.");

        // Start looping crystal appear sound at reduced volume
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip = crystalAppearSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started (volume {crystalVolume}).");
        }
    }

    private void CollectBucket(Vector3 buildingPosition)
    {
        bucketCollected = true;

        // Stop crystal appear sound
        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Crystal appear sound stopped.");
        }

        // Destroy bucket
        Destroy(spawnedBucket);
        Debug.Log($"{TAG} 🪣 Bucket collected and destroyed.");

        // Play one-shot collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // Spawn rain above building
        Vector3 rainPos = buildingPosition + new Vector3(0f, -1.5f, 0f);
        spawnedRain = Instantiate(rainEffectPrefab, rainPos, Quaternion.identity);
        Debug.Log($"{TAG} 🌧️ Rain spawned at {rainPos}.");

        // Start rain timer
        StartCoroutine(RainRoutine());
    }

    private IEnumerator RainRoutine()
    {
        isRaining = true;
        Debug.Log($"{TAG} 🌧️ Rain will last {rainDuration} s.");

        // Start looping rain sound at full volume
        if (effectAudioSource != null && rainSound != null)
        {
            effectAudioSource.clip = rainSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = rainVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Rain sound started (volume {rainVolume}).");
        }

        yield return new WaitForSeconds(rainDuration);

        // Stop rain sound
        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Rain sound stopped.");
        }

        if (spawnedRain != null)
        {
            Destroy(spawnedRain);
            Debug.Log($"{TAG} ☀️ Rain ended.");
        }
        isRaining = false;

        // SPAWN CATS VISUALLY instead of just adding bonus
        Debug.Log($"{TAG} 🐱 Starting to spawn {bonusCatSpawns} cats visually!");
        StartCoroutine(SpawnCatsRoutine());
    }

    private IEnumerator SpawnCatsRoutine()
    {
        for (int i = 0; i < bonusCatSpawns; i++)
        {
            // Trigger cat spawn via event
            OnSpawnCat?.Invoke();
            Debug.Log($"{TAG} 🐱 Spawned cat {i + 1}/{bonusCatSpawns}");
            
            // Wait before spawning next cat
            yield return new WaitForSeconds(catSpawnInterval);
        }
        
        Debug.Log($"{TAG} 🐱 Finished spawning all {bonusCatSpawns} bonus cats!");
    }

    private void CleanUp()
    {
        // Stop all coroutines
        StopAllCoroutines();

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

        if (spawnedBucket != null)
        {
            Destroy(spawnedBucket);
            Debug.Log($"{TAG} 🧹 Bucket cleaned up.");
        }
        if (spawnedRain != null)
        {
            Destroy(spawnedRain);
            Debug.Log($"{TAG} 🧹 Rain cleaned up.");
        }
    }
}