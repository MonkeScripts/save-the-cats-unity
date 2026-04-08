// WaterBucketEffect.cs
// Attach to the SAME GameObject as scan_qrcode_exercise_final (XR Origin).
// This is a self-contained MonoBehaviour that handles ONLY the water bucket mechanic.
// It implements ISpecialEffect so SpecialEffectManager can drive it automatically.
//
// WHAT IT DOES:
//   • At 15 s remaining, spawns a water bucket 15 cm to the right of the building.
//   • Plays crystal_appear sound while bucket is visible.
//   • When the phone comes within 0.3 m of the bucket, it "collects" it.
//   • Collection: destroys bucket, stops crystal sound, spawns rain VFX, plays rain sound.
//   • Rain auto-despawns after rainDuration seconds, then awards +5 bonus cats.

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
    [SerializeField] private float bucketSpawnTime      = 15f;   // Seconds left when bucket appears
    [SerializeField] private float bucketSpawnDistance  = 0.15f; // Metres right of building
    [SerializeField] private float bucketTriggerDistance= 0.30f; // Metres phone must be within
    [SerializeField] private float rainDuration         = 5f;    // Seconds rain lasts
    [SerializeField] private int   bonusCats            = 5;     // Cats awarded on collection

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   collectSound;           // One-shot SFX when bucket collected
    [SerializeField] private AudioClip   crystalAppearSound;     // Looping sound while bucket is visible
    [SerializeField] private AudioClip   rainSound;              // Looping sound while rain is active

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Water Bucket";
    public event System.Action<int> OnBonusCatsEarned;

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

        // Start looping crystal appear sound
        if (audioSource != null && crystalAppearSound != null)
        {
            audioSource.clip = crystalAppearSound;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log($"{TAG} 🔊 Crystal appear sound started.");
        }
    }

    private void CollectBucket(Vector3 buildingPosition)
    {
        bucketCollected = true;

        // Stop crystal appear sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Crystal appear sound stopped.");
        }

        // Destroy bucket
        Destroy(spawnedBucket);
        Debug.Log($"{TAG} 🪣 Bucket collected and destroyed.");

        // Play one-shot collect SFX
        if (audioSource != null && collectSound != null)
        {
            audioSource.PlayOneShot(collectSound);
        }

        // Spawn rain above building
        Vector3 rainPos = buildingPosition + new Vector3(0f, 1f, 0f);
        spawnedRain = Instantiate(rainEffectPrefab, rainPos, Quaternion.identity);
        Debug.Log($"{TAG} 🌧️ Rain spawned at {rainPos}.");

        // Start rain timer
        StartCoroutine(RainRoutine());
    }

    private IEnumerator RainRoutine()
    {
        isRaining = true;
        Debug.Log($"{TAG} 🌧️ Rain will last {rainDuration} s.");

        // Start looping rain sound
        if (audioSource != null && rainSound != null)
        {
            audioSource.clip = rainSound;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log($"{TAG} 🔊 Rain sound started.");
        }

        yield return new WaitForSeconds(rainDuration);

        // Stop rain sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Rain sound stopped.");
        }

        if (spawnedRain != null)
        {
            Destroy(spawnedRain);
            Debug.Log($"{TAG} ☀️ Rain ended.");
        }
        isRaining = false;

        // Award bonus cats via event (SpecialEffectManager forwards this to the main script)
        OnBonusCatsEarned?.Invoke(bonusCats);
        Debug.Log($"{TAG} 🐱 +{bonusCats} bonus cats fired.");
    }

    private void CleanUp()
    {
        // Stop all coroutines
        StopAllCoroutines();

        // Stop any playing audio
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
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