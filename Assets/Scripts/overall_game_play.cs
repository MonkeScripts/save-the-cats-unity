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
    [SerializeField] private GameObject      goToStartLocationPanel;
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

    [Header("Power-Up Info Panels")]
    [SerializeField] private GameObject waterBucketInformationPanel;
    [SerializeField] private GameObject iceInformationPanel;
    [SerializeField] private GameObject growthInformationPanel;
    [SerializeField] private GameObject bonus3Panel;

    [Header("Start Location Settings")]
    [SerializeField] private GameObject startCirclePrefab;
    [SerializeField] private float      circleSpawnDistance = 0.8f;
    [SerializeField] private float      circleTriggerDistance = 0.6f;
    [SerializeField] private AudioSource startCircleAudioSource;
    [SerializeField] private AudioClip   startCircleSound;

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
    private int     catMultiplier   = 1;
    private GameObject currentActiveExerciseModel;
    private SlackerBar slackerBar;

    // Start location state
    private bool       isWaitingAtStartLocation = false;
    private bool       isReadyForExerciseSelection = false;  // NEW: Only true after reaching circle
    private GameObject spawnedStartCircle;

    // Info panel coroutines
    private Coroutine waterInfoCoroutine;
    private Coroutine iceInfoCoroutine;
    private Coroutine growthInfoCoroutine;

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
        slackerBar = GetComponent<SlackerBar>();

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
        if (goToStartLocationPanel != null) goToStartLocationPanel.SetActive(false);
        
        // Hide all info panels
        if (waterBucketInformationPanel != null) waterBucketInformationPanel.SetActive(false);
        if (iceInformationPanel         != null) iceInformationPanel.SetActive(false);
        if (growthInformationPanel      != null) growthInformationPanel.SetActive(false);
        if (bonus3Panel != null) bonus3Panel.SetActive(false);

        timeLeft        = gameDuration;
        catCount        = 0;
        catMultiplier   = 1;
        catCountText.text = "0";

        // Initialize SpecialEffectManager
        effectManager = GetComponent<SpecialEffectManager>();
        if (effectManager != null)
        {
            effectManager.Initialize(
                getBuildingPos:           GetBuildingPosition,
                addCatsCallback:          AddBonusCats,
                setTimePausedCallback:    SetTimePaused,
                spawnCatCallback:         SpawnCatFromCube,
                setCatMultiplierCallback: SetCatMultiplier,
                showInfoPanelCallback:    ShowInfoPanel,
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
        // Check if user has reached the start location circle
        if (isWaitingAtStartLocation && spawnedStartCircle != null)
        {
            CheckStartLocationProximity();
        }

        // Drain the thread-safe queue from MQTT callbacks
        if (mqttSpawnQueue.TryDequeue(out bool isCatSpawn))
        {
            if (isCatSpawn)
            {
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
            if (timeLeft > 0 && !isTimePaused)
            {
                timeLeft -= Time.deltaTime;
                timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s";
            }
            else if (isTimePaused)
            {
                timerText.text = $"FROZEN";
            }

            effectManager?.Tick();

            if (timeLeft <= 0 && !isTimePaused)
            {
                EndGame();
            }
        }

        if (triggerWrongMoveUI)
        {
            triggerWrongMoveUI = false;
            if (wrongMoveCoroutine == null)
                wrongMoveCoroutine = StartCoroutine(ShowWrongMoveRoutine());
        }
    }

    // ──────────────────────────────────────────────
    // Start Location Circle Logic
    // ──────────────────────────────────────────────
    
    private void SpawnStartCircle()
    {
        Vector3 buildingPos = GetBuildingPosition();
        Vector3 circlePos = buildingPos + new Vector3(0f, 0f, -circleSpawnDistance);
        circlePos.y = buildingPos.y;
        
        spawnedStartCircle = Instantiate(startCirclePrefab, circlePos, Quaternion.identity);

        //Save position for special mode
        if (StartCircleAnchor.Instance != null)
            StartCircleAnchor.Instance.SetStartCirclePosition(circlePos);
        
        Debug.Log($"{TAG} 🎯 Start circle spawned at {circlePos} ({circleSpawnDistance}m behind building)");

        // Start looping start circle sound
        if (startCircleAudioSource != null && startCircleSound != null)
        {
            startCircleAudioSource.clip = startCircleSound;
            startCircleAudioSource.loop = true;
            startCircleAudioSource.Play();
            Debug.Log($"{TAG} 🔊 Start circle sound started.");
        }
    }

    private void CheckStartLocationProximity()
    {
        Vector3 phonePos = Camera.main.transform.position;
        Vector3 circlePos = spawnedStartCircle.transform.position;
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(phonePos.x, phonePos.z),
            new Vector2(circlePos.x, circlePos.z)
        );
        
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

    private void OnReachedStartLocation()
    {
        isWaitingAtStartLocation = false;
        isReadyForExerciseSelection = true;  // NEW: Now MQTT commands will be accepted

        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        // Stop start circle sound
        if (startCircleAudioSource != null && startCircleAudioSource.isPlaying)
        {
            startCircleAudioSource.Stop();
            startCircleAudioSource.loop = false;
            Debug.Log($"{TAG} 🔇 Start circle sound stopped.");
        }

        if (spawnedStartCircle != null)
        {
            Destroy(spawnedStartCircle);
            Debug.Log($"{TAG} 🎯 Start circle destroyed.");
        }

        if (goToStartLocationPanel != null)
            goToStartLocationPanel.SetActive(false);

        if (waitingExercisePanel != null)
            waitingExercisePanel.SetActive(true);

        Debug.Log($"{TAG} ▶️ Ready for exercise selection via MQTT.");
    }

    // ──────────────────────────────────────────────
    // Info Panel Display
    // ──────────────────────────────────────────────

    private void ShowInfoPanel(string panelName, float duration)
    {
        Debug.Log($"{TAG} 📋 ShowInfoPanel called: panel='{panelName}', duration={duration}s");

        switch (panelName.ToLower())
        {
            case "water":
                if (waterBucketInformationPanel != null)
                {
                    if (waterInfoCoroutine != null)
                        StopCoroutine(waterInfoCoroutine);
                    waterInfoCoroutine = StartCoroutine(ShowInfoPanelRoutine(waterBucketInformationPanel, duration));
                }
                break;

            case "ice":
                if (iceInformationPanel != null)
                {
                    if (iceInfoCoroutine != null)
                        StopCoroutine(iceInfoCoroutine);
                    iceInfoCoroutine = StartCoroutine(ShowInfoPanelRoutine(iceInformationPanel, duration));
                }
                break;

            case "growth":
                if (growthInformationPanel != null)
                {
                    if (growthInfoCoroutine != null)
                        StopCoroutine(growthInfoCoroutine);
                    growthInfoCoroutine = StartCoroutine(ShowInfoPanelRoutine(growthInformationPanel, duration));
                }
                break;

            default:
                Debug.LogWarning($"{TAG} ⚠️ Unknown info panel name: '{panelName}'");
                break;
        }
    }

    private System.Collections.IEnumerator ShowInfoPanelRoutine(GameObject panel, float duration)
    {
        panel.SetActive(true);
        Debug.Log($"{TAG} 📋 Info panel shown for {duration}s");

        yield return new WaitForSeconds(duration);

        panel.SetActive(false);
        Debug.Log($"{TAG} 📋 Info panel hidden.");
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
                // ═══════════════════════════════════════════════════════════
                // FIX: Ignore ALL MQTT commands if user hasn't reached the start circle yet
                // ═══════════════════════════════════════════════════════════
                if (isWaitingAtStartLocation)
                {
                    Debug.Log($"{TAG} ⚠️ MQTT ignored - user still walking to start circle!");
                    return;
                }

                // Also ignore if not ready for exercise selection (before circle is reached)
                if (!isReadyForExerciseSelection && !isGameActive)
                {
                    Debug.Log($"{TAG} ⚠️ MQTT ignored - not ready for exercise selection yet!");
                    return;
                }
                // ═══════════════════════════════════════════════════════════

                if (selectedExercise == ExerciseType.None)
                {
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

                        //Tell special mode manager which exercise was selected
                        if (ClimberAltArmLegGameMode.Instance != null)
                            ClimberAltArmLegGameMode.Instance.SetSpecialMode(selectedExercise);

                        mqttSpawnQueue.Enqueue(false);
                    }
                }
                else if (isGameActive)
                {
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
        slackerBar?.StartGame(
            () => catCount,
            (newCount) => {
                catCount = newCount;
                if (catCountText != null) catCountText.text = $"{catCount}";
            }
        );

        isCountingDown = false;

        // NEW: Set duration based on exercise type
        bool isSpecialMode = ClimberAltArmLegGameMode.Instance != null &&
                            ClimberAltArmLegGameMode.Instance.IsSpecialMode;

        if (isSpecialMode)
        {
            timeLeft = 20f;
            Debug.Log($"{TAG} ⏱️ Special mode: game duration set to 20 seconds.");
        }
        else
        {
            timeLeft = gameDuration;
            Debug.Log($"{TAG} ⏱️ Normal mode: game duration set to {gameDuration} seconds.");
        }

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
        isReadyForExerciseSelection = false;
        slackerBar?.EndGame();

        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.Stop();
        }

        if (audioSource != null && victorySound != null)
            audioSource.PlayOneShot(victorySound);

        if (currentActiveExerciseModel != null)
            Destroy(currentActiveExerciseModel);

        effectManager?.OnRoundEnd();

        // Hide all info panels when game ends
        if (waterBucketInformationPanel != null) waterBucketInformationPanel.SetActive(false);
        if (iceInformationPanel         != null) iceInformationPanel.SetActive(false);
        if (growthInformationPanel      != null) growthInformationPanel.SetActive(false);
        if (bonus3Panel != null) bonus3Panel.SetActive(false);

        if (roundSummaryPanel != null)
        {
            roundSummaryPanel.SetActive(true);
            if (roundScoreText != null)
                roundScoreText.text = $"You saved {catCount} cats!";
        }

        timerPanel.SetActive(false);
        catCountPanel.SetActive(false);
        Debug.Log($"{TAG} Round ended. Cats: {catCount}");

        if (ClimberAltArmLegGameMode.Instance != null) ClimberAltArmLegGameMode.Instance.Reset();
        if (StartCircleAnchor.Instance != null) StartCircleAnchor.Instance.Reset();
        FreezeTimerBar freezeBar = FindFirstObjectByType<FreezeTimerBar>();
        if (freezeBar != null) freezeBar.HideBar();
    }

    // ──────────────────────────────────────────────
    // Public UI callbacks
    // ──────────────────────────────────────────────
    
    public void LockAnchor()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        isAnchored = true;
        isReadyForExerciseSelection = false;  // NEW: Not ready until circle is reached
        
        if (startButton != null) startButton.SetActive(false);
        if (startPanel != null) startPanel.SetActive(false);

        foreach (var entry in spawnedCubes)
            Debug.Log($"{TAG} Anchor locked at {entry.Value.transform.position}");

        trackedImageManager.enabled = false;

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
        isReadyForExerciseSelection = false;  // NEW: Not ready until circle is reached
        slackerBar?.ResetForNewRound();
        
        SpawnStartCircle();
        
        if (goToStartLocationPanel != null)
            goToStartLocationPanel.SetActive(true);
        
        isWaitingAtStartLocation = true;

        effectManager?.ResetForNewRound();

        Debug.Log($"{TAG} PlayAgain: Walk to start location for new round.");

        if (ClimberAltArmLegGameMode.Instance != null) ClimberAltArmLegGameMode.Instance.Reset();
        if (StartCircleAnchor.Instance != null) StartCircleAnchor.Instance.Reset();
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
        // NEW: Determine spawn origin based on special mode
        bool isSpecialMode = ClimberAltArmLegGameMode.Instance != null &&
                            ClimberAltArmLegGameMode.Instance.IsSpecialMode;

        bool useStartCircle = isSpecialMode &&
                            StartCircleAnchor.Instance != null &&
                            StartCircleAnchor.Instance.HasPosition();

        if (useStartCircle)
        {
            // ── Special mode: spawn cats from START CIRCLE position ──
            catCount++;
            if (catCountText != null) catCountText.text = $"{catCount}";

            if (audioSource != null && meowSound != null)
                audioSource.PlayOneShot(meowSound);

            Vector3    spawnPos      = StartCircleAnchor.Instance.GetStartCirclePosition();
            Quaternion spawnRotation = Quaternion.identity;
            int        choice        = Random.Range(0, 4);

            switch (choice)
            {
                case 0: spawnPos += Vector3.right   *  0.15f; spawnRotation = Quaternion.Euler(0,  90, 0); break;
                case 1: spawnPos += Vector3.right   * -0.15f; spawnRotation = Quaternion.Euler(0, -90, 0); break;
                case 2: spawnPos += Vector3.forward *  0.15f; spawnRotation = Quaternion.Euler(0,   0, 0); break;
                case 3: spawnPos += Vector3.forward * -0.15f; spawnRotation = Quaternion.Euler(0, 180, 0); break;
            }

            Instantiate(catPrefab, spawnPos, spawnRotation);
            Debug.Log($"{TAG} 🐱 Cat spawned at START CIRCLE. Total: {catCount}");
        }
        else
        {
            // ── Normal mode: spawn cats from QR code position ──
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
                Debug.Log($"{TAG} 🐱 Cat spawned at QR CODE. Total: {catCount}");
                return;
            }
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
            slackerBar?.OnRepCompleted();
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

        // NEW: Use start circle position for Climbers and AltArmLeg
        bool isSpecialMode = ClimberAltArmLegGameMode.Instance != null && 
                            ClimberAltArmLegGameMode.Instance.IsSpecialMode;

        foreach (var entry in spawnedCubes)
        {
            Vector3    spawnPos = isSpecialMode && StartCircleAnchor.Instance != null && 
                                StartCircleAnchor.Instance.HasPosition()
                                ? StartCircleAnchor.Instance.GetStartCirclePosition()
                                : entry.Value.transform.position;

            Quaternion spawnRot = entry.Value.transform.rotation;

            currentActiveExerciseModel = Instantiate(prefab, spawnPos, spawnRot);

            if (audioSource != null && fireSound != null)
            {
                audioSource.clip = fireSound;
                audioSource.loop = true;
                audioSource.Play();
            }

            Debug.Log($"{TAG} Exercise prefab spawned at {(isSpecialMode ? "START CIRCLE" : "BUILDING")}: {spawnPos}");
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

    private Vector3 GetBuildingPosition()
    {
        foreach (var entry in spawnedCubes)
            return entry.Value.transform.position;
        return Vector3.zero;
    }

    private void AddBonusCats(int amount)
    {
        catCount += amount;
        if (catCountText != null) catCountText.text = $"{catCount}";
        Debug.Log($"{TAG} +{amount} bonus cats! Total: {catCount}");
    }

    private void SetTimePaused(bool isPaused)
    {
        isTimePaused = isPaused;
        Debug.Log($"{TAG} ⏱️ Time is now {(isPaused ? "PAUSED ❄️" : "RESUMED ▶️")}");
    }

    private void SetCatMultiplier(int multiplier, float duration)
    {
        catMultiplier = multiplier;
        Debug.Log($"{TAG} 🚀 Cat multiplier set to x{multiplier}");

        if (bonus3Panel != null)
        {
            if (multiplier > 1)
            {
                bonus3Panel.SetActive(true);
                Debug.Log($"{TAG} 💚 Bonus3 panel shown.");
            }
            else
            {
                bonus3Panel.SetActive(false);
                Debug.Log($"{TAG} 💚 Bonus3 panel hidden.");
            }
        }
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