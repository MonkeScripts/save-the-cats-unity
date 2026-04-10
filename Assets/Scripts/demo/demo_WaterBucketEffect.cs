// demo_WaterBucketEffect.cs
// Attach to XR Origin in TUTORIAL SCENE.
// Same as WaterBucketEffect.cs but fires OnShardAppeared and OnShardGone
// so the tutorial panel shows/hides when water bucket appears/disappears.

using UnityEngine;
using System.Collections;

public class demo_WaterBucketEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[DEMO_WATER_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Water Bucket Prefabs")]
    [SerializeField] private GameObject waterBucketPrefab;
    [SerializeField] private GameObject rainEffectPrefab;

    [Header("Water Bucket Settings")]
    [SerializeField] private float bucketSpawnTime       = 15f;
    [SerializeField] private float bucketDuration        = 5f;
    [SerializeField] private float bucketSpawnDistance   = 0.15f;
    [SerializeField] private float bucketTriggerDistance = 0.30f;
    [SerializeField] private float rainDuration          = 5f;
    [SerializeField] private int   bonusCatSpawns        = 15;
    [SerializeField] private float catSpawnInterval      = 0.3f;
    [SerializeField] private float infoPanelDuration     = 5f;

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
    public string EffectName => "Demo Water Bucket";
    public event System.Action<int>           OnBonusCatsEarned;
    public event System.Action<bool>          OnTimePause;
    public event System.Action                OnSpawnCat;
    public event System.Action<int, float>    OnSetCatMultiplier;
    public event System.Action<string, float> OnShowInfoPanel;
    public event System.Action                OnShardAppeared;   // NEW
    public event System.Action                OnShardGone;       // NEW

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

        // Phase 2: Poll for phone proximity
        if (bucketHasSpawned && !bucketCollected && !bucketExpired && spawnedBucket != null)
        {
            float dx   = phonePosition.x - spawnedBucket.transform.position.x;
            float dz   = phonePosition.z - spawnedBucket.transform.position.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            Debug.Log($"{TAG} 📏 Phone↔Bucket XZ dist: {dist:F2} m (trigger < {bucketTriggerDistance} m)");

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
        Vector3 bucketPos = buildingPosition + new Vector3(bucketSpawnDistance, 0.2f, 0f);
        spawnedBucket = Instantiate(waterBucketPrefab, bucketPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {bucketSpawnTime}s left — bucket spawned at {bucketPos}.");
        Debug.Log($"{TAG} ⚠️ HURRY! Bucket will disappear in {bucketDuration} seconds!");

        // Start crystal appear sound
        if (effectAudioSource != null && crystalAppearSound != null)
        {
            effectAudioSource.clip   = crystalAppearSound;
            effectAudioSource.loop   = true;
            effectAudioSource.volume = crystalVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started.");
        }

        // NEW: Fire OnShardAppeared so tutorial panel shows
        OnShardAppeared?.Invoke();
        Debug.Log($"{TAG} 💎 OnShardAppeared fired.");

        // Start the bucket expiration timer
        bucketTimerCoroutine = StartCoroutine(BucketExpirationRoutine());
    }

    private IEnumerator BucketExpirationRoutine()
    {
        yield return new WaitForSeconds(bucketDuration);

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

            // NEW: Fire OnShardGone so tutorial panel hides
            OnShardGone?.Invoke();
            Debug.Log($"{TAG} 💎 OnShardGone fired (expired).");
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
            effectAudioSource.PlayOneShot(collectSound);

        // Show info panel
        OnShowInfoPanel?.Invoke("water", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Water info panel requested for {infoPanelDuration}s.");

        // NEW: Fire OnShardGone so tutorial panel hides
        OnShardGone?.Invoke();
        Debug.Log($"{TAG} 💎 OnShardGone fired (collected).");

        // Spawn rain
        Vector3 rainPos = buildingPosition + new Vector3(0f, -1.5f, 0f);
        spawnedRain = Instantiate(rainEffectPrefab, rainPos, Quaternion.identity);
        Debug.Log($"{TAG} 🌧️ Rain spawned at {rainPos}.");

        StartCoroutine(RainRoutine());
    }

    private IEnumerator RainRoutine()
    {
        isRaining = true;
        Debug.Log($"{TAG} 🌧️ Rain will last {rainDuration}s.");

        if (effectAudioSource != null && rainSound != null)
        {
            effectAudioSource.clip   = rainSound;
            effectAudioSource.loop   = true;
            effectAudioSource.volume = rainVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Rain sound started.");
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

        // Fire OnShardGone if bucket was still visible
        if (bucketHasSpawned && !bucketCollected && !bucketExpired)
        {
            OnShardGone?.Invoke();
            Debug.Log($"{TAG} 💎 OnShardGone fired (cleanup).");
        }

        if (effectAudioSource != null)
        {
            if (effectAudioSource.isPlaying)
                effectAudioSource.Stop();
            effectAudioSource.loop   = false;
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