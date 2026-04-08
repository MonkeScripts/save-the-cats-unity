// WaterBucketEffect.cs
// Attach to the SAME GameObject as overall_game_play (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the water bucket mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 15 s remaining, spawns a water bucket for LIMITED TIME (5 seconds).
//   • Plays crystal_appear sound while bucket is visible.
//   • If user collects within 5 seconds: shows info panel, rain + bonus cats.
//   • If user misses: bucket disappears, power-up lost!
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
    [SerializeField] private float bucketDuration        = 5f;    // How long bucket stays (limited time!)
    [SerializeField] private float bucketSpawnDistance   = 0.15f; // Metres right of building
    [SerializeField] private float bucketTriggerDistance = 0.30f; // Metres phone must be within
    [SerializeField] private float rainDuration          = 5f;    // Seconds rain lasts
    [SerializeField] private int   bonusCatSpawns        = 15;    // Number of cats to spawn visually
    [SerializeField] private float catSpawnInterval      = 0.3f;  // Seconds between each cat spawn
    [SerializeField] private float infoPanelDuration     = 5f;    // How long info panel shows

    [Header("Audio - USE A SEPARATE AUDIOSOURCE!")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   collectSound;
    [SerializeField] private AudioClip   crystalAppearSound;
    [SerializeField] private AudioClip   rainSound;
    
    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.5f;
    [SerializeField] private float rainVolume    = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Water Bucket";
    public event System.Action<int> OnBonusCatsEarned;
    public event System.Action<bool> OnTimePause;
    public event System.Action OnSpawnCat;
    public event System.Action<int, float> OnSetCatMultiplier;
    public event System.Action<string, float> OnShowInfoPanel;  // NEW

    // ──────────────────────────────────────────────
    // Internal state
    // ──────────────────────────────────────────────
    private GameObject spawnedBucket;
    private GameObject spawnedRain;
    private bool       bucketHasSpawned = false;
    private bool       bucketCollected  = false;
    private bool       bucketExpired    = false;
    private bool       isRaining        = false;
    private Coroutine  bucketTimerCoroutine;

    // ──────────────────────────────────────────────
    // ISpecialEffect — Tick
    // ──────────────────────────────────────────────
    public void Tick(float timeLeft, Vector3 buildingPosition, Vector3 phonePosition)
    {
        // Phase 1: Spawn the bucket when the timer hits the threshold
        if (!bucketHasSpawned && timeLeft <= bucketSpawnTime)
        {
            SpawnBucket(buildingPosition);
        }

        // Phase 2: Poll for phone proximity (only if bucket exists and not expired)
        if (bucketHasSpawned && !bucketCollected && !bucketExpired && spawnedBucket != null)
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
        bucketExpired    = false;
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
        Debug.Log($"{TAG} ⚠️ HURRY! Bucket will disappear in {bucketDuration} seconds!");

        // Start crystal appear sound
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip = crystalAppearSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started (volume {crystalVolume}).");
        }

        // Start the bucket expiration timer
        bucketTimerCoroutine = StartCoroutine(BucketExpirationRoutine());
    }

    private IEnumerator BucketExpirationRoutine()
    {
        yield return new WaitForSeconds(bucketDuration);

        // If bucket wasn't collected, it expires
        if (!bucketCollected)
        {
            bucketExpired = true;
            Debug.Log($"{TAG} ⏰ TIME'S UP! Bucket expired - power-up missed!");

            // Stop crystal sound
            if (effectAudioSource != null && effectAudioSource.isPlaying)
            {
                effectAudioSource.Stop();
                effectAudioSource.loop = false;
                Debug.Log($"{TAG} 🔇 Crystal appear sound stopped (expired).");
            }

            // Destroy bucket
            if (spawnedBucket != null)
            {
                Destroy(spawnedBucket);
                Debug.Log($"{TAG} 🪣 Bucket disappeared (not collected in time).");
            }
        }
    }

    private void CollectBucket(Vector3 buildingPosition)
    {
        bucketCollected = true;

        // Stop the expiration timer
        if (bucketTimerCoroutine != null)
        {
            StopCoroutine(bucketTimerCoroutine);
            bucketTimerCoroutine = null;
        }

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

        // Play collect SFX
        if (effectAudioSource != null && collectSound != null)
        {
            effectAudioSource.PlayOneShot(collectSound);
        }

        // NEW: Show info panel
        OnShowInfoPanel?.Invoke("water", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Water info panel requested for {infoPanelDuration}s.");

        // Spawn rain
        Vector3 rainPos = buildingPosition + new Vector3(0f, -1.5f, 0f);
        spawnedRain = Instantiate(rainEffectPrefab, rainPos, Quaternion.identity);
        Debug.Log($"{TAG} 🌧️ Rain spawned at {rainPos}.");

        StartCoroutine(RainRoutine());
    }

    private IEnumerator RainRoutine()
    {
        isRaining = true;
        Debug.Log($"{TAG} 🌧️ Rain will last {rainDuration} s.");

        if (effectAudioSource != null && rainSound != null)
        {
            effectAudioSource.clip = rainSound;
            effectAudioSource.loop = true;
            effectAudioSource.volume = rainVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Rain sound started (volume {rainVolume}).");
        }

        yield return new WaitForSeconds(rainDuration);

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

        Debug.Log($"{TAG} 🐱 Starting to spawn {bonusCatSpawns} cats visually!");
        StartCoroutine(SpawnCatsRoutine());
    }

    private IEnumerator SpawnCatsRoutine()
    {
        for (int i = 0; i < bonusCatSpawns; i++)
        {
            OnSpawnCat?.Invoke();
            Debug.Log($"{TAG} 🐱 Spawned cat {i + 1}/{bonusCatSpawns}");
            yield return new WaitForSeconds(catSpawnInterval);
        }
        
        Debug.Log($"{TAG} 🐱 Finished spawning all {bonusCatSpawns} bonus cats!");
    }

    private void CleanUp()
    {
        StopAllCoroutines();
        bucketTimerCoroutine = null;

        if (effectAudioSource != null)
        {
            if (effectAudioSource.isPlaying)
                effectAudioSource.Stop();
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