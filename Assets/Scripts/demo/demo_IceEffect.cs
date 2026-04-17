// demo_IceEffect.cs
// Attach to XR Origin in TUTORIAL SCENE.
// Same as IceEffect.cs but fires OnShardAppeared and OnShardGone
// so the tutorial panel shows/hides when ice shard appears/disappears.

using UnityEngine;
using System.Collections;

public class demo_IceEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[DEMO_ICE_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Ice Shard Prefabs")]
    [SerializeField] private GameObject iceShardPrefab;
    [SerializeField] private GameObject iceEffectPrefab;

    [Header("Ice Shard Settings")]
    [SerializeField] private float shardSpawnTime       = 15f;
    [SerializeField] private float shardDuration        = 7f;
    [SerializeField] private float shardSpawnDistance   = 1.0f;
    [SerializeField] private float shardTriggerDistance = 0.70f;
    [SerializeField] private float freezeDuration       = 7f;
    [SerializeField] private float infoPanelDuration    = 5f;

    [Header("Audio - USE A SEPARATE AUDIOSOURCE!")]
    [SerializeField] private AudioSource effectAudioSource;
    [SerializeField] private AudioClip   crystalAppearSound;
    [SerializeField] private AudioClip   collectSound;
    [SerializeField] private AudioClip   freezeSound;

    [Header("Volume Settings")]
    [SerializeField] private float crystalVolume = 0.9f;
    [SerializeField] private float freezeVolume  = 1.0f;

    // ──────────────────────────────────────────────
    // ISpecialEffect — public API
    // ──────────────────────────────────────────────
    public string EffectName => "Demo Ice Shard";
    public event System.Action<int>          OnBonusCatsEarned;
    public event System.Action<bool>         OnTimePause;
    public event System.Action               OnSpawnCat;
    public event System.Action<int, float>   OnSetCatMultiplier;
    public event System.Action<string, float> OnShowInfoPanel;
    public event System.Action               OnShardAppeared;   // NEW
    public event System.Action               OnShardGone;       // NEW

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

        // Phase 2: Poll for phone proximity
        if (shardHasSpawned && !shardCollected && !shardExpired && spawnedShard != null)
        {
            float dx   = phonePosition.x - spawnedShard.transform.position.x;
            float dz   = phonePosition.z - spawnedShard.transform.position.z;
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
        isFreezing      = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnShard(Vector3 buildingPosition)
    {
        shardHasSpawned = true;

        Vector3 shardPos = buildingPosition + new Vector3(-shardSpawnDistance, 0.2f, 0f);
        spawnedShard = Instantiate(iceShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — ice shard spawned at {shardPos}.");
        Debug.Log($"{TAG} ⚠️ HURRY! Ice shard will disappear in {shardDuration} seconds!");

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

        // Start the shard expiration timer
        shardTimerCoroutine = StartCoroutine(ShardExpirationRoutine());
    }

    private IEnumerator ShardExpirationRoutine()
    {
        yield return new WaitForSeconds(shardDuration);

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

            // NEW: Fire OnShardGone so tutorial panel hides
            OnShardGone?.Invoke();
            Debug.Log($"{TAG} 💎 OnShardGone fired (expired).");
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
            effectAudioSource.PlayOneShot(collectSound);

        // Show info panel
        OnShowInfoPanel?.Invoke("ice", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Ice info panel requested for {infoPanelDuration}s.");

        // NEW: Fire OnShardGone so tutorial panel hides
        OnShardGone?.Invoke();
        Debug.Log($"{TAG} 💎 OnShardGone fired (collected).");

        // Spawn ice effect at building position
        spawnedIceEffect = Instantiate(iceEffectPrefab, buildingPosition, Quaternion.identity);
        Debug.Log($"{TAG} ❄️ Ice effect spawned at building: {buildingPosition}.");

        StartCoroutine(FreezeRoutine());
    }

    private IEnumerator FreezeRoutine()
    {
        isFreezing = true;

        // PAUSE TIME
        OnTimePause?.Invoke(true);

        // Start freeze bar
        FreezeTimerBar freezeBar = FindFirstObjectByType<FreezeTimerBar>();
        if (freezeBar != null)
            freezeBar.StartBar(freezeDuration);
        else
            Debug.LogWarning($"{TAG} ⚠️ FreezeTimerBar not found!");

        // Start freeze sound
        if (effectAudioSource != null && freezeSound != null)
        {
            effectAudioSource.clip   = freezeSound;
            effectAudioSource.loop   = true;
            effectAudioSource.volume = freezeVolume;
            effectAudioSource.Play();
        }

        yield return new WaitForSecondsRealtime(freezeDuration);

        if (effectAudioSource != null && effectAudioSource.isPlaying)
        {
            effectAudioSource.Stop();
            effectAudioSource.loop = false;
        }

        if (spawnedIceEffect != null)
            Destroy(spawnedIceEffect);

        isFreezing = false;

        // RESUME TIME
        OnTimePause?.Invoke(false);

        Debug.Log($"{TAG} ▶️ Time resumed!");
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

        // Fire OnShardGone if shard was still visible
        if (shardHasSpawned && !shardCollected && !shardExpired)
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