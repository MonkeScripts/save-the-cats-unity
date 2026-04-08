// Attach to XR Origin.
// This script handles ONLY core game logic:
//   QR scanning → anchor locking → exercise selection (via MQTT) →
//   countdown → gameplay timer → cat spawning → round end/restart.
//
// All special effects are handled by SpecialEffectManager + individual effect scripts.
// To add a new effect: create a MonoBehaviour implementing ISpecialEffect, attach it
// to XR Origin, and SpecialEffectManager will discover it automatically.

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Collections.Concurrent;

public enum ExerciseType { None, Squat, Lunge, HighKnee, Climbers, AltArmLeg, ChairTricep }

public class overall_game_play : MonoBehaviour
{
    private readonly ConcurrentQueue<bool> mqttSpawnQueue = new();
    private Coroutine wrongMoveCoroutine;
    private ARTrackedImageManager trackedImageManager;
    private const string TAG = "[AR_DEBUG]";

    [Header("Prefabs")]
    [SerializeField] private GameObject catPrefab;

    [Header("UI Elements")]
    [SerializeField] private GameObject      scanningPanel;
    [SerializeField] private GameObject      startPanel;
    [SerializeField] private GameObject      waitingExercisePanel;
    [SerializeField] private GameObject      exerciseSelectedPanel;
    [SerializeField] private TextMeshProUGUI exerciseText;
    [SerializeField] private GameObject      catCountPanel;
    [SerializeField] private TextMeshProUGUI catCountText;
    [SerializeField] private Button          spawnCatButton;
    [SerializeField] private GameObject      timerPanel;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject      gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject      startButton;
    [SerializeField] private GameObject      wrongMovePanel;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f;

    [Header("Exercise Prefabs")]
    [SerializeField] private GameObject squatPrefab;
    [SerializeField] private GameObject lungePrefab;
    [SerializeField] private GameObject highKneePrefab;
    [SerializeField] private GameObject climbersPrefab;
    [SerializeField] private GameObject altArmLegPrefab;
    [SerializeField] private GameObject chairTricepPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   tickSound;
    [SerializeField] private AudioClip   goSound;
    [SerializeField] private AudioClip   meowSound;
    [SerializeField] private AudioClip   victorySound;
    [SerializeField] private AudioClip   clickSound;
    [SerializeField] private AudioClip   wrongMoveSound;
    [SerializeField] private AudioClip   fireSound;

    [Header("Round Transition UI")]
    [SerializeField] private GameObject      roundSummaryPanel;
    [SerializeField] private TextMeshProUGUI roundScoreText;
    [SerializeField] private Button          playAgainButton;
    [SerializeField] private Button          finalEndButton;

    // ──────────────────────────────────────────────
    // Core game state
    // ──────────────────────────────────────────────
    private ExerciseType selectedExercise = ExerciseType.None;
    private Dictionary<string, GameObject> spawnedCubes = new();
    private float   timeLeft;
    private int     catCount        = 0;
    private bool    isGameActive    = false;
    private bool    isAnchored      = false;
    private bool    isCountingDown  = false;
    private bool    triggerWrongMoveUI = false;
    private bool    isTimePaused    = false;
    private GameObject currentActiveExerciseModel;

    // ──────────────────────────────────────────────
    // Reference to the effect coordinator
    // ──────────────────────────────────────────────
    private SpecialEffectManager effectManager;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────
    void Awake()
    {
        Debug.Log($"{TAG} Awake: Initializing AR Exercise Game.");
        trackedImageManager = GetComponent<ARTrackedImageManager>();

        // Hide all UI except scanning panel
        if (startButton            != null) startButton.SetActive(false);
        if (spawnCatButton         != null) spawnCatButton.gameObject.SetActive(false);
        if (timerText              != null) timerText.gameObject.SetActive(false);
        if (countdownText          != null) countdownText.gameObject.SetActive(false);
        if (gameOverPanel          != null) gameOverPanel.SetActive(false);
        if (startPanel             != null) startPanel.SetActive(false);
        if (scanningPanel          != null) scanningPanel.SetActive(true);
        if (waitingExercisePanel   != null) waitingExercisePanel.SetActive(false);
        if (exerciseSelectedPanel  != null) exerciseSelectedPanel.SetActive(false);

        timeLeft        = gameDuration;
        catCount        = 0;
        catCountText.text = "0";

        // Initialize SpecialEffectManager — it auto-discovers ISpecialEffect components
        effectManager = GetComponent<SpecialEffectManager>();
        if (effectManager != null)
        {
            effectManager.Initialize(
                getBuildingPos:        GetBuildingPosition,
                addCatsCallback:       AddBonusCats,
                setTimePausedCallback: SetTimePaused,
                spawnCatCallback:      SpawnCatFromCube,  // NEW: Pass cat spawn callback
                gameActiveGetter:      () => isGameActive,
                timeLeftGetter:        () => timeLeft
            );
            Debug.Log($"{TAG} SpecialEffectManager found and initialized.");
        }
        else
        {
            Debug.LogWarning($"{TAG} No SpecialEffectManager found on this GameObject. Special effects disabled.");
        }
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);

        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived += HandleMqttMessage;
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);

        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived -= HandleMqttMessage;
    }

    void Update()
    {
        // Drain the thread-safe queue from MQTT callbacks
        if (mqttSpawnQueue.TryDequeue(out bool isCatSpawn))
        {
            if (isCatSpawn)
                SpawnCatFromCube();
            else
                StartCoroutine(StartCountdownRoutine());
        }

        // Core timer
        if (isGameActive)
        {
            // Only count down if time is NOT paused
            if (timeLeft > 0 && !isTimePaused)
            {
                timeLeft -= Time.deltaTime;
                timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s";
            }
            // Show "FROZEN" or similar when paused
            else if (isTimePaused)
            {
                timerText.text = $"❄️ FROZEN ❄️";
            }

            // Tick all special effects (even when time is paused, so effects can track state)
            effectManager?.Tick();

            // End game only if time ran out and not paused
            if (timeLeft <= 0 && !isTimePaused)
            {
                EndGame();
            }
        }

        // Wrong move UI flag (set from MQTT thread via CheckExerciseCompletion)
        if (triggerWrongMoveUI)
        {
            triggerWrongMoveUI = false;
            if (wrongMoveCoroutine == null)
                wrongMoveCoroutine = StartCoroutine(ShowWrongMoveRoutine());
        }
    }

    // ──────────────────────────────────────────────
    // MQTT
    // ──────────────────────────────────────────────
    private void HandleMqttMessage(string topic, string payload)
    {
        try
        {
            MqttPayload data    = JsonUtility.FromJson<MqttPayload>(payload);
            string      message = data.action;
            Debug.Log($"{TAG} MQTT on '{topic}': action={message}");

            if (topic == "ultra/action1")
            {
                if (selectedExercise == ExerciseType.None)
                {
                    // Exercise selection phase
                    switch (message)
                    {
                        case "0": selectedExercise = ExerciseType.Squat;       break;
                        case "1": selectedExercise = ExerciseType.Lunge;       break;
                        case "2": selectedExercise = ExerciseType.HighKnee;    break;
                        case "3": selectedExercise = ExerciseType.Climbers;    break;
                        case "4": selectedExercise = ExerciseType.AltArmLeg;   break;
                        case "5": selectedExercise = ExerciseType.ChairTricep; break;
                    }

                    if (selectedExercise != ExerciseType.None)
                    {
                        Debug.Log($"{TAG} Exercise {selectedExercise} selected. Queuing countdown.");
                        mqttSpawnQueue.Enqueue(false);
                    }
                }
                else if (isGameActive)
                {
                    // Can still do reps even when time is paused!
                    CheckExerciseCompletion(message);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{TAG} MQTT parse error: {e.Message}");
        }
    }

    // ──────────────────────────────────────────────
    // Game flow
    // ──────────────────────────────────────────────
    private System.Collections.IEnumerator StartCountdownRoutine()
    {
        isCountingDown = true;
        Debug.Log($"{TAG} Countdown starting for {selectedExercise}");

        if (waitingExercisePanel != null) waitingExercisePanel.SetActive(false);
        exerciseSelectedPanel.SetActive(true);
        exerciseText.text = $"{selectedExercise}";
        countdownText.gameObject.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            countdownText.text = $"{i}...";
            if (audioSource != null && tickSound != null)
                audioSource.PlayOneShot(tickSound);
            yield return new WaitForSeconds(1f);
        }

        countdownText.text = "GO!";
        if (audioSource != null && goSound != null)
            audioSource.PlayOneShot(goSound);

        yield return new WaitForSeconds(0.5f);
        countdownText.gameObject.SetActive(false);
        exerciseSelectedPanel.SetActive(false);

        SpawnExercisePrefab();
        isGameActive   = true;
        isCountingDown = false;
        timerPanel.SetActive(true);
        catCountPanel.SetActive(true);
        timerText.gameObject.SetActive(true);

        Debug.Log($"{TAG} Game STARTED.");
    }

    private void EndGame()
    {
        isGameActive = false;
        isTimePaused = false;
        timeLeft     = 0;

        // Stop looping sounds
        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.Stop();
        }

        // Victory sting
        if (audioSource != null && victorySound != null)
            audioSource.PlayOneShot(victorySound);

        // Clean up exercise model
        if (currentActiveExerciseModel != null)
            Destroy(currentActiveExerciseModel);

        // Tell all effects to clean up
        effectManager?.OnRoundEnd();

        // Show round summary
        if (roundSummaryPanel != null)
        {
            roundSummaryPanel.SetActive(true);
            if (roundScoreText != null)
                roundScoreText.text = $"You saved {catCount} cats!";
        }

        timerPanel.SetActive(false);
        catCountPanel.SetActive(false);
        Debug.Log($"{TAG} Round ended. Cats: {catCount}");
    }

    // ──────────────────────────────────────────────
    // Public UI callbacks
    // ──────────────────────────────────────────────
    public void PlayAgain()
    {
        if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
        if (roundSummaryPanel != null) roundSummaryPanel.SetActive(false);

        selectedExercise = ExerciseType.None;
        timeLeft         = gameDuration;
        isGameActive     = false;
        isTimePaused     = false;
        waitingExercisePanel.SetActive(true);

        // Reset all effects for the new round
        effectManager?.ResetForNewRound();

        Debug.Log($"{TAG} PlayAgain: Ready for new MQTT command.");
    }

    public void ShowFinalResults()
    {
        if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
        if (roundSummaryPanel != null) roundSummaryPanel.SetActive(false);
        if (gameOverPanel     != null) gameOverPanel.SetActive(true);
        if (finalScoreText    != null) finalScoreText.text = $"{catCount}";

        if (audioSource != null && victorySound != null)
            audioSource.PlayOneShot(victorySound);

        Debug.Log($"{TAG} Final score: {catCount}");
    }

    public void LockAnchor()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        isAnchored = true;
        startButton.SetActive(false);
        startPanel.SetActive(false);
        waitingExercisePanel.SetActive(true);

        foreach (var entry in spawnedCubes)
            Debug.Log($"{TAG} Anchor locked at {entry.Value.transform.position}");

        trackedImageManager.enabled = false;
    }

    // ──────────────────────────────────────────────
    // Cat spawning
    // ──────────────────────────────────────────────
    public void SpawnCatFromCube()
    {
        // NOTE: Removed "if (!isGameActive) return;" so effects can spawn cats even after game ends
        // The effect scripts handle their own timing

        foreach (var cubeEntry in spawnedCubes)
        {
            GameObject cube = cubeEntry.Value;
            if (cube == null) continue;

            catCount++;
            if (catCountText != null) catCountText.text = $"{catCount}";

            if (audioSource != null && meowSound != null)
                audioSource.PlayOneShot(meowSound);

            Vector3    spawnPos      = cube.transform.position;
            Quaternion spawnRotation = cube.transform.rotation;
            int        choice        = Random.Range(0, 4);

            switch (choice)
            {
                case 0: spawnPos += cube.transform.right   *  0.15f; spawnRotation *= Quaternion.Euler(0,  90, 0); break;
                case 1: spawnPos += cube.transform.right   * -0.15f; spawnRotation *= Quaternion.Euler(0, -90, 0); break;
                case 2: spawnPos += cube.transform.forward *  0.15f; spawnRotation *= Quaternion.Euler(0,   0, 0); break;
                case 3: spawnPos += cube.transform.forward * -0.15f; spawnRotation *= Quaternion.Euler(0, 180, 0); break;
            }

            Instantiate(catPrefab, spawnPos, spawnRotation);
            Debug.Log($"{TAG} Cat spawned. Total: {catCount}");
            return;
        }
    }

    // ──────────────────────────────────────────────
    // AR image tracking
    // ──────────────────────────────────────────────
    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"{TAG} QR detected: {trackedImage.referenceImage.name}");
            if (!isAnchored && !spawnedCubes.ContainsKey(trackedImage.referenceImage.name))
            {
                GameObject tempAnchor = new GameObject("TempAnchor");
                tempAnchor.transform.position = trackedImage.transform.position;
                spawnedCubes.Add(trackedImage.referenceImage.name, tempAnchor);

                if (audioSource != null && clickSound != null)
                    audioSource.PlayOneShot(clickSound);

                startButton.SetActive(true);
                scanningPanel.SetActive(false);
                startPanel.SetActive(true);
            }
        }

        if (!isAnchored)
        {
            foreach (var trackedImage in eventArgs.updated)
            {
                if (spawnedCubes.TryGetValue(trackedImage.referenceImage.name, out GameObject cube))
                    cube.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
            }
        }
    }

    // ──────────────────────────────────────────────
    // Exercise helpers
    // ──────────────────────────────────────────────
    void CheckExerciseCompletion(string message)
    {
        bool isCorrect = selectedExercise switch
        {
            ExerciseType.Squat       => message == "0",
            ExerciseType.Lunge       => message == "1",
            ExerciseType.HighKnee    => message == "2",
            ExerciseType.Climbers    => message == "3",
            ExerciseType.AltArmLeg   => message == "4",
            ExerciseType.ChairTricep => message == "5",
            _ => false
        };

        if (isCorrect)
        {
            Debug.Log($"{TAG} ✅ Correct {selectedExercise} rep!");
            mqttSpawnQueue.Enqueue(true);
        }
        else
        {
            Debug.Log($"{TAG} ❌ Wrong move (got {message}, expected {(int)selectedExercise - 1})");
            if (int.TryParse(message, out int move) && move >= 0 && move <= 5)
                triggerWrongMoveUI = true;
        }
    }

    void SpawnExercisePrefab()
    {
        if (currentActiveExerciseModel != null)
        {
            Destroy(currentActiveExerciseModel);
            Debug.Log($"{TAG} Previous exercise model destroyed.");
        }

        GameObject prefab = GetSelectedPrefab();
        foreach (var entry in spawnedCubes)
        {
            currentActiveExerciseModel = Instantiate(prefab, entry.Value.transform.position, entry.Value.transform.rotation);
            if (audioSource != null && fireSound != null)
            {
                audioSource.clip = fireSound;
                audioSource.loop = true;
                audioSource.Play();
            }
            break;
        }
    }

    private GameObject GetSelectedPrefab() => selectedExercise switch
    {
        ExerciseType.Squat       => squatPrefab,
        ExerciseType.Lunge       => lungePrefab,
        ExerciseType.HighKnee    => highKneePrefab,
        ExerciseType.Climbers    => climbersPrefab,
        ExerciseType.AltArmLeg   => altArmLegPrefab,
        ExerciseType.ChairTricep => chairTricepPrefab,
        _                        => null
    };

    // ──────────────────────────────────────────────
    // Helpers used by SpecialEffectManager delegates
    // ──────────────────────────────────────────────

    /// <summary>Returns the world position of the locked AR anchor (building).</summary>
    private Vector3 GetBuildingPosition()
    {
        foreach (var entry in spawnedCubes)
            return entry.Value.transform.position;
        return Vector3.zero;
    }

    /// <summary>Adds bonus cats and updates the UI. Called by SpecialEffectManager.</summary>
    private void AddBonusCats(int amount)
    {
        catCount += amount;
        if (catCountText != null) catCountText.text = $"{catCount}";
        Debug.Log($"{TAG} +{amount} bonus cats! Total: {catCount}");
    }

    /// <summary>Pauses or resumes the game timer. Called by SpecialEffectManager (from IceEffect).</summary>
    private void SetTimePaused(bool isPaused)
    {
        isTimePaused = isPaused;
        Debug.Log($"{TAG} ⏱️ Time is now {(isPaused ? "PAUSED ❄️" : "RESUMED ▶️")}");
    }

    // ──────────────────────────────────────────────
    // Wrong move UI
    // ──────────────────────────────────────────────
    private System.Collections.IEnumerator ShowWrongMoveRoutine()
    {
        if (wrongMovePanel == null)
        {
            Debug.LogError($"{TAG} wrongMovePanel not assigned!");
            yield break;
        }

        if (audioSource != null && wrongMoveSound != null)
            audioSource.PlayOneShot(wrongMoveSound);

        wrongMovePanel.SetActive(true);
        yield return new WaitForSeconds(1f);
        wrongMovePanel.SetActive(false);
        wrongMoveCoroutine = null;
    }
}

// ──────────────────────────────────────────────
// MQTT payload model
// ──────────────────────────────────────────────
[System.Serializable]
public class MqttPayload
{
    public string action;
}