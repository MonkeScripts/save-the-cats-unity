// demo_GrowthEffect.cs
// Attach to XR Origin in TUTORIAL SCENE.
// Same as GrowthEffect.cs but fires OnShardAppeared and OnShardGone
// so the tutorial panel shows/hides when green shard appears/disappears.

using UnityEngine;
using System.Collections;

public class demo_GrowthEffect : MonoBehaviour, ISpecialEffect
{
    private const string TAG = "[DEMO_GROWTH_EFFECT]";

    // ──────────────────────────────────────────────
    // Inspector fields
    // ──────────────────────────────────────────────
    [Header("Growth Shard Prefabs")]
    [SerializeField] private GameObject greenShardPrefab;
    [SerializeField] private GameObject greenOrbPrefab;

    [Header("Growth Shard Settings")]
    [SerializeField] private float shardSpawnTime       = 35f;
    [SerializeField] private float shardDuration        = 7f;
    [SerializeField] private float shardSpawnDistance   = 1.0f;
    [SerializeField] private float shardTriggerDistance = 0.70f;
    [SerializeField] private float shieldDuration       = 10f;
    [SerializeField] private int   catMultiplier        = 3;
    [SerializeField] private float infoPanelDuration    = 5f;

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
    public string EffectName => "Demo Growth Shard";
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
        isShieldActive  = false;
        Debug.Log($"{TAG} 🔄 Reset for new round.");
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────
    private void SpawnShard(Vector3 buildingPosition)
    {
        shardHasSpawned = true;

        Vector3 shardPos = buildingPosition + new Vector3(0f, 0.2f, shardSpawnDistance);
        spawnedShard = Instantiate(greenShardPrefab, shardPos, Quaternion.identity);
        Debug.Log($"{TAG} ⏱️ {shardSpawnTime}s left — green shard spawned at {shardPos}.");
        Debug.Log($"{TAG} ⚠️ HURRY! Green shard will disappear in {shardDuration} seconds!");

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
        Debug.Log($"{TAG} 💚 Green shard collected and destroyed.");

        // Play collect SFX
        if (effectAudioSource != null && collectSound != null)
            effectAudioSource.PlayOneShot(collectSound);

        // Show info panel
        OnShowInfoPanel?.Invoke("growth", infoPanelDuration);
        Debug.Log($"{TAG} 📋 Growth info panel requested for {infoPanelDuration}s.");

        // NEW: Fire OnShardGone so tutorial panel hides
        OnShardGone?.Invoke();
        Debug.Log($"{TAG} 💎 OnShardGone fired (collected).");

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
            effectAudioSource.clip   = shieldSound;
            effectAudioSource.loop   = true;
            effectAudioSource.volume = shieldVolume;
            effectAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Shield sound started.");
        }

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
        Debug.Log($"{TAG} ▶️ Cat multiplier reset to x1.");
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

        if (spawnedOrb != null)
        {
            Destroy(spawnedOrb);
            Debug.Log($"{TAG} 🧹 Orb cleaned up.");
        }
    }
}