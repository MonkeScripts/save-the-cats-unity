// Attach to XR Origin.
// This script handles ONLY core game logic:
//   QR scanning → start button → walk to circle → exercise selection (via MQTT) →
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
    [SerializeField] private GameObject      goToStartLocationPanel;  // NEW: Panel telling user to walk to circle
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

    [Header("Start Location Settings")]
    [SerializeField] private GameObject startCirclePrefab;           // NEW: Circle prefab to spawn on ground
    [SerializeField] private float      circleSpawnDistance = 0.8f;  // NEW: Distance behind building (metres)
    [SerializeField] private float      circleTriggerDistance = 0.6f; // NEW: How close user must be to circle

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
    [SerializeField] private AudioClip startCircleLoopSound;

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
    private int     catMultiplier   = 1;
    private GameObject currentActiveExerciseModel;

    // NEW: Start location state
    private bool       isWaitingAtStartLocation = false;
    private GameObject spawnedStartCircle;

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
        if (goToStartLocationPanel != null) goToStartLocationPanel.SetActive(false);  // NEW

        timeLeft        = gameDuration;
        catCount        = 0;
        catMultiplier   = 1;
        catCountText.text = "0";

        // Initialize SpecialEffectManager — it auto-discovers ISpecialEffect components
        effectManager = GetComponent<SpecialEffectManager>();
        if (effectManager != null)
        {
            effectManager.Initialize(
                getBuildingPos:           GetBuildingPosition,
                addCatsCallback:          AddBonusCats,
                setTimePausedCallback:    SetTimePaused,
                spawnCatCallback:         SpawnCatFromCube,
                setCatMultiplierCallback: SetCatMultiplier,
                gameActiveGetter:         () => isGameActive,
                timeLeftGetter:           () => timeLeft
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
        // NEW: Check if user has reached the start location circle
        if (isWaitingAtStartLocation && spawnedStartCircle != null)
        {
            CheckStartLocationProximity();
        }

        // Drain the thread-safe queue from MQTT callbacks
        if (mqttSpawnQueue.TryDequeue(out bool isCatSpawn))
        {
            if (isCatSpawn)
            {
                // SPAWN MULTIPLE CATS BASED ON MULTIPLIER
                for (int i = 0; i < catMultiplier; i++)
                {
                    SpawnCatFromCube();
                }
                Debug.Log($"{TAG} 🐱 Spawned {catMultiplier} cat(s) for this rep (multiplier x{catMultiplier})");
            }
            else
            {
                StartCoroutine(StartCountdownRoutine());
            }
        }

        // Core timer
        if (isGameActive)
        {
            // Only count down if time is NOT paused
            if (timeLeft > 0 && !isTimePaused)
            {
                timeLeft -= Time.deltaTime;
                
                // Show multiplier in timer if active
                if (catMultiplier > 1)
                {
                    timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s 💚x{catMultiplier}";
                }
                else
                {
                    timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s";
                }
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
    // NEW: Start Location Circle Logic
    // ──────────────────────────────────────────────
    
    /// <summary>
    /// Spawns the start circle and shows the "go to start location" panel.
    /// Called when user presses the Start button.
    /// </summary>
    private void SpawnStartCircle()
    {
        Vector3 buildingPos = GetBuildingPosition();
        
        // Spawn circle 0.8m BEHIND the building (negative Z direction)
        // Also place it on the ground (y = 0 or slightly above)
        Vector3 circlePos = buildingPos + new Vector3(0f, 0f, -circleSpawnDistance);
        
        // Adjust Y to be on the ground (you may need to tweak this based on your AR setup)
        circlePos.y = buildingPos.y;  // Same height as building anchor, or set to 0
        
        spawnedStartCircle = Instantiate(startCirclePrefab, circlePos, Quaternion.identity);
        
        // --- NEW: Start Looping Sound ---
        if (audioSource != null && startCircleLoopSound != null)
        {
            audioSource.clip = startCircleLoopSound;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log($"{TAG} 🔊 Start circle looping sound started.");
        }
        
        Debug.Log($"{TAG} 🎯 Start circle spawned at {circlePos} ({circleSpawnDistance}m behind building)");
        Debug.Log($"{TAG} 📍 Building position: {buildingPos}");
        Debug.Log($"{TAG} 📏 User needs to be within {circleTriggerDistance}m of circle");
    }

    /// <summary>
    /// Checks if the phone/user is close enough to the start circle.
    /// Called every frame while waiting.
    /// </summary>
    private void CheckStartLocationProximity()
    {
        Vector3 phonePos = Camera.main.transform.position;
        Vector3 circlePos = spawnedStartCircle.transform.position;
        
        // Calculate horizontal distance only (ignore Y/height difference)
        float horizontalDistance = Vector2.Distance(
            new Vector2(phonePos.x, phonePos.z),
            new Vector2(circlePos.x, circlePos.z)
        );
        
        // Debug log every 30 frames (twice per second)
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"{TAG} 📏 Phone↔Circle dist: {horizontalDistance:F2}m (need < {circleTriggerDistance}m)");
        }

        if (horizontalDistance < circleTriggerDistance)
        {
            Debug.Log($"{TAG} ✅ User reached start location!");
            OnReachedStartLocation();
        }
    }

    /// <summary>
    /// Called when user reaches the start circle.
    /// Destroys circle, hides panel, continues to exercise selection.
    /// </summary>
    private void OnReachedStartLocation()
    {
        isWaitingAtStartLocation = false;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false; // Reset loop to false for one-shots
        }
        // Destroy the circle
        if (spawnedStartCircle != null)
        {
            Destroy(spawnedStartCircle);
            Debug.Log($"{TAG} 🎯 Start circle destroyed.");
        }

        // Hide the "go to start location" panel
        if (goToStartLocationPanel != null)
            goToStartLocationPanel.SetActive(false);

        // Show waiting for exercise panel
        if (waitingExercisePanel != null)
            waitingExercisePanel.SetActive(true);

        Debug.Log($"{TAG} ▶️ Ready for exercise selection via MQTT.");
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
        catMultiplier = 1;
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
    
    /// <summary>
    /// Called when user presses the Start button after QR detection.
    /// Now spawns the start circle instead of going directly to exercise selection.
    /// </summary>
    public void LockAnchor()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        isAnchored = true;
        
        // Hide start button and start panel
        if (startButton != null) startButton.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);

        // Lock the anchor position
        foreach (var entry in spawnedCubes)
            Debug.Log($"{TAG} Anchor locked at {entry.Value.transform.position}");

        // Disable AR tracking
        trackedImageManager.enabled = false;

        // NEW: Spawn the start circle and show "go to start location" panel
        SpawnStartCircle();
        
        if (goToStartLocationPanel != null)
            goToStartLocationPanel.SetActive(true);
        
        isWaitingAtStartLocation = true;
        
        Debug.Log($"{TAG} 🎯 Waiting for user to walk to start location...");
    }

    public void PlayAgain()
    {
        if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
        if (roundSummaryPanel != null) roundSummaryPanel.SetActive(false);

        selectedExercise = ExerciseType.None;
        timeLeft         = gameDuration;
        isGameActive     = false;
        isTimePaused     = false;
        catMultiplier    = 1;
        
        // NEW: Spawn circle again for new round
        SpawnStartCircle();
        
        if (goToStartLocationPanel != null)
            goToStartLocationPanel.SetActive(true);
        
        isWaitingAtStartLocation = true;

        // Reset all effects for the new round
        effectManager?.ResetForNewRound();

        Debug.Log($"{TAG} PlayAgain: Walk to start location for new round.");
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

    // ──────────────────────────────────────────────
    // Cat spawning
    // ──────────────────────────────────────────────
    public void SpawnCatFromCube()
    {
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

    /// <summary>Sets the cat spawn multiplier. Called by SpecialEffectManager (from GrowthEffect).</summary>
    private void SetCatMultiplier(int multiplier, float duration)
    {
        catMultiplier = multiplier;
        Debug.Log($"{TAG} 🚀 Cat multiplier set to x{multiplier}");
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