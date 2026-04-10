// demo_overall_game_play.cs
// Attach to XR Origin in TUTORIAL SCENE.
// Copy of overall_game_play.cs with tutorial additions.
// Tutorial flow:
//   1. Welcome panel → OK
//   2. QR guide panel → OK → scan QR
//   3. Start button → normal flow
//   4. Walk to circle panel → OK → walk to circle
//   5. Gameplay starts → SlackerBar explanation → OK → resume
//   6. Crystal shard appears → IceShardTutorialPanel shows
//   7. Round ends → Round summary panel as normal

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.Collections.Concurrent;
using UnityEngine.SceneManagement;

public class demo_overall_game_play : MonoBehaviour
{
    private readonly ConcurrentQueue<bool> mqttSpawnQueue = new();
    private Coroutine wrongMoveCoroutine;
    private ARTrackedImageManager trackedImageManager;
    private const string TAG = "[DEMO_AR_DEBUG]";

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
    [SerializeField] private GameObject  startCirclePrefab;
    [SerializeField] private float       circleSpawnDistance   = 0.8f;
    [SerializeField] private float       circleTriggerDistance = 0.6f;
    [SerializeField] private AudioSource startCircleAudioSource;
    [SerializeField] private AudioClip   startCircleSound;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f;

    [Header("Exercise Prefabs")]
    [SerializeField] private GameObject highKneePrefab;
    [SerializeField] private GameObject pushUpPrefab;
    [SerializeField] private GameObject sitUpPrefab;
    [SerializeField] private GameObject lungePrefab;
    [SerializeField] private GameObject squatPrefab;
    [SerializeField] private GameObject overheadHoldPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   tickSound;
    [SerializeField] private AudioClip   goSound;
    [SerializeField] private AudioClip   meowSound;
    [SerializeField] private AudioClip   victorySound;
    [SerializeField] private AudioClip   clickSound;
    [SerializeField] private AudioClip   wrongMoveSound;

    [Header("Fire Audio")]
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private float       fireVolume = 0.6f;
    [SerializeField] private AudioClip   fireSound;

    [Header("Round Transition UI")]
    [SerializeField] private GameObject   roundSummaryPanel;
    [SerializeField] private TextMeshProUGUI   roundScoreText;
    [SerializeField] private Button   mainMenuButton;

    [Header("Tutorial Panels")]
    [SerializeField] private GameObject welcomePanel;
    [SerializeField] private GameObject qrGuidePanel;
    [SerializeField] private GameObject walkToCirclePanel;
    [SerializeField] private GameObject slackerBarTutorialPanel;
    [SerializeField] private GameObject iceShardTutorialPanel;
    [SerializeField] private GameObject tutorialOKButton;

    // ──────────────────────────────────────────────
    // Core game state
    // ──────────────────────────────────────────────
    private ExerciseType selectedExercise  = ExerciseType.None;
    private Dictionary<string, GameObject> spawnedCubes = new();
    private float       timeLeft;
    private int         catCount           = 0;
    private bool        isGameActive       = false;
    private bool        isAnchored         = false;
    private bool        isCountingDown     = false;
    private bool        triggerWrongMoveUI = false;
    private bool        isTimePaused       = false;
    private int         catMultiplier      = 1;
    private GameObject  currentActiveExerciseModel;
    private SlackerBar  slackerBar;

    // Start location state
    private bool       isWaitingAtStartLocation    = false;
    private bool       isReadyForExerciseSelection = false;
    private GameObject spawnedStartCircle;

    // Info panel coroutines
    private Coroutine waterInfoCoroutine;
    private Coroutine iceInfoCoroutine;
    private Coroutine growthInfoCoroutine;

    // ──────────────────────────────────────────────
    // Tutorial state
    // ──────────────────────────────────────────────
    private enum TutorialStep
    {
        Welcome,
        QRGuide,
        Scanning,
        WalkToCircle,
        Walking,
        SlackerBarExplain,
        Gameplay
    }

    private TutorialStep currentTutorialStep = TutorialStep.Welcome;
    private bool isTutorialPaused = false;

    // ──────────────────────────────────────────────
    // Reference to the effect coordinator
    // ──────────────────────────────────────────────
    private demo_SpecialEffectManager effectManager;

    // ──────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────
    void Awake()
    {
        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            arSession.Reset();
            Debug.Log($"{TAG} ✅ AR Session reset.");
        }

        trackedImageManager = GetComponent<ARTrackedImageManager>();
        trackedImageManager.enabled = false;
        trackedImageManager.enabled = true;
        Debug.Log($"{TAG} ✅ ARTrackedImageManager re-enabled.");
        slackerBar = GetComponent<SlackerBar>();

        // Hide all UI except scanning panel
        if (startButton            != null) startButton.SetActive(false);
        if (spawnCatButton         != null) spawnCatButton.gameObject.SetActive(false);
        if (timerText              != null) timerText.gameObject.SetActive(false);
        if (countdownText          != null) countdownText.gameObject.SetActive(false);
        if (gameOverPanel          != null) gameOverPanel.SetActive(false);
        if (startPanel             != null) startPanel.SetActive(false);
        if (scanningPanel          != null) scanningPanel.SetActive(false); // Hide until tutorial step
        if (waitingExercisePanel   != null) waitingExercisePanel.SetActive(false);
        if (exerciseSelectedPanel  != null) exerciseSelectedPanel.SetActive(false);
        if (goToStartLocationPanel != null) goToStartLocationPanel.SetActive(false);

        // Hide all info panels
        if (waterBucketInformationPanel != null) waterBucketInformationPanel.SetActive(false);
        if (iceInformationPanel         != null) iceInformationPanel.SetActive(false);
        if (growthInformationPanel      != null) growthInformationPanel.SetActive(false);
        if (bonus3Panel                 != null) bonus3Panel.SetActive(false);

        // Hide all tutorial panels
        if (welcomePanel             != null) welcomePanel.SetActive(false);
        if (qrGuidePanel             != null) qrGuidePanel.SetActive(false);
        if (walkToCirclePanel        != null) walkToCirclePanel.SetActive(false);
        if (slackerBarTutorialPanel  != null) slackerBarTutorialPanel.SetActive(false);
        if (iceShardTutorialPanel    != null) iceShardTutorialPanel.SetActive(false);
        if (tutorialOKButton         != null) tutorialOKButton.SetActive(false);

        timeLeft          = gameDuration;
        catCount          = 0;
        catMultiplier     = 1;
        catCountText.text = "0";

        // Initialize demo_SpecialEffectManager
        effectManager = GetComponent<demo_SpecialEffectManager>();
        if (effectManager != null)
        {
            effectManager.Initialize(
                getBuildingPos:           GetBuildingPosition,
                addCatsCallback:          AddBonusCats,
                setTimePausedCallback:    SetTimePaused,
                spawnCatCallback:         SpawnCatFromCube,
                setCatMultiplierCallback: SetCatMultiplier,
                showInfoPanelCallback:    ShowInfoPanel,
                showShardTutorialCallback: ShowIceShardTutorialPanel,  // NEW
                hideShardTutorialCallback: HideIceShardTutorialPanel,  // NEW
                gameActiveGetter:         () => isGameActive,
                timeLeftGetter:           () => timeLeft
            );
            Debug.Log($"{TAG} demo_SpecialEffectManager found and initialized.");
        }
        else
        {
            Debug.LogWarning($"{TAG} No demo_SpecialEffectManager found. Special effects disabled.");
        }

        // Start tutorial
        StartTutorial();
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
        if (isGameActive && !isTutorialPaused)
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
    // Tutorial Logic
    // ──────────────────────────────────────────────

    private void StartTutorial()
    {
        currentTutorialStep = TutorialStep.Welcome;
        ShowTutorialStep(TutorialStep.Welcome);
        Debug.Log($"{TAG} 📚 Tutorial started.");
    }

    private void ShowTutorialStep(TutorialStep step)
    {
        // Hide all tutorial panels first
        HideAllTutorialPanels();

        switch (step)
        {
            case TutorialStep.Welcome:
                if (welcomePanel    != null) welcomePanel.SetActive(true);
                if (tutorialOKButton != null) tutorialOKButton.SetActive(true);
                Debug.Log($"{TAG} 📚 Tutorial Step: Welcome.");
                break;

            case TutorialStep.QRGuide:
                if (qrGuidePanel    != null) qrGuidePanel.SetActive(true);
                Debug.Log($"{TAG} 📚 Tutorial Step: QR Guide.");
                break;

            case TutorialStep.Scanning:
                if (qrGuidePanel    != null) qrGuidePanel.SetActive(false);
                if (scanningPanel   != null) scanningPanel.SetActive(true);
                if (tutorialOKButton != null) tutorialOKButton.SetActive(false);
                Debug.Log($"{TAG} 📚 Tutorial Step: Scanning.");
                break;

            case TutorialStep.WalkToCircle:
                if (walkToCirclePanel != null) walkToCirclePanel.SetActive(true);
                if (tutorialOKButton  != null) tutorialOKButton.SetActive(true);
                Debug.Log($"{TAG} 📚 Tutorial Step: Walk To Circle.");
                break;

            case TutorialStep.Walking:
                // No tutorial panel, just let user walk
                if (tutorialOKButton != null) tutorialOKButton.SetActive(false);
                Debug.Log($"{TAG} 📚 Tutorial Step: Walking.");
                break;

            case TutorialStep.SlackerBarExplain:
                isTutorialPaused = true;
                slackerBar?.Pause();  // ✅ Pause slacker bar
                if (slackerBarTutorialPanel != null) slackerBarTutorialPanel.SetActive(true);
                if (tutorialOKButton        != null) tutorialOKButton.SetActive(true);
                break;

            case TutorialStep.Gameplay:
                isTutorialPaused = false;
                slackerBar?.Resume();  // ✅ Resume slacker bar
                if (tutorialOKButton != null) tutorialOKButton.SetActive(false);
                break;
        }

        currentTutorialStep = step;
    }

    private void HideAllTutorialPanels()
    {
        if (welcomePanel            != null) welcomePanel.SetActive(false);
        if (qrGuidePanel            != null) qrGuidePanel.SetActive(false);
        if (walkToCirclePanel       != null) walkToCirclePanel.SetActive(false);
        if (slackerBarTutorialPanel != null) slackerBarTutorialPanel.SetActive(false);
        Debug.Log($"{TAG} 📚 All tutorial panels hidden.");
    }

    // ──────────────────────────────────────────────
    // OK Button callback - called by UI button
    // ──────────────────────────────────────────────
    public void OnTutorialOKPressed()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        Debug.Log($"{TAG} 📚 OK pressed at step: {currentTutorialStep}");

        switch (currentTutorialStep)
        {
            case TutorialStep.Welcome:
                // Welcome → QR Guide
                if (tutorialOKButton != null)
                    tutorialOKButton.SetActive(false);
                ShowTutorialStep(TutorialStep.QRGuide);
                break;

            case TutorialStep.QRGuide:
                // QR Guide → Scanning
                ShowTutorialStep(TutorialStep.Scanning);
                break;

            case TutorialStep.WalkToCircle:
                // Walk to Circle panel dismissed → user starts walking
                ShowTutorialStep(TutorialStep.Walking);

                // Show the goToStartLocationPanel so user can see the AR circle
                if (goToStartLocationPanel != null)
                    goToStartLocationPanel.SetActive(true);
                break;

            case TutorialStep.SlackerBarExplain:
                // SlackerBar explained → resume gameplay
                ShowTutorialStep(TutorialStep.Gameplay);
                break;
        }
    }

    // ──────────────────────────────────────────────
    // Shard Tutorial Panel — called by demo_SpecialEffectManager
    // ──────────────────────────────────────────────
    public void ShowIceShardTutorialPanel()
    {
        if (iceShardTutorialPanel != null)
        {
            iceShardTutorialPanel.SetActive(true);
            Debug.Log($"{TAG} 📚 IceShard tutorial panel shown.");
        }
    }

    public void HideIceShardTutorialPanel()
    {
        if (iceShardTutorialPanel != null)
        {
            iceShardTutorialPanel.SetActive(false);
            Debug.Log($"{TAG} 📚 IceShard tutorial panel hidden.");
        }
    }

    // ──────────────────────────────────────────────
    // Start Location Circle Logic
    // ──────────────────────────────────────────────

    private void SpawnStartCircle()
    {
        Vector3 buildingPos = GetBuildingPosition();
        Vector3 circlePos   = buildingPos + new Vector3(0f, 0f, -circleSpawnDistance);
        circlePos.y         = buildingPos.y;

        spawnedStartCircle = Instantiate(startCirclePrefab, circlePos, Quaternion.identity);

        // Save position for special mode
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
        Vector3 phonePos  = Camera.main.transform.position;
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
        isWaitingAtStartLocation    = false;
        isReadyForExerciseSelection = true;

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

        if (walkToCirclePanel != null)
            walkToCirclePanel.SetActive(false);
        if (tutorialOKButton != null)
            tutorialOKButton.SetActive(false);

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
                // Ignore ALL MQTT commands if user hasn't reached the start circle yet
                if (isWaitingAtStartLocation)
                {
                    Debug.Log($"{TAG} ⚠️ MQTT ignored - user still walking to start circle!");
                    return;
                }

                // Also ignore if not ready for exercise selection
                if (!isReadyForExerciseSelection && !isGameActive)
                {
                    Debug.Log($"{TAG} ⚠️ MQTT ignored - not ready for exercise selection yet!");
                    return;
                }

                if (selectedExercise == ExerciseType.None)
                {
                    switch (message)
                    {
                        case "0": selectedExercise = ExerciseType.HighKnee;     break;
                        case "1": selectedExercise = ExerciseType.PushUp;       break;
                        case "2": selectedExercise = ExerciseType.SitUp;        break;
                        case "3": selectedExercise = ExerciseType.Lunge;        break;
                        case "4": selectedExercise = ExerciseType.Squat;        break;
                        case "5": selectedExercise = ExerciseType.OverheadHold; break;
                    }

                    if (selectedExercise != ExerciseType.None)
                    {
                        Debug.Log($"{TAG} Exercise {selectedExercise} selected. Queuing countdown.");

                        // Tell special mode manager which exercise was selected
                        if (PushUpSitUpGameMode.Instance != null)
                            PushUpSitUpGameMode.Instance.SetSpecialMode(selectedExercise);

                        mqttSpawnQueue.Enqueue(false);
                    }
                }
                else if (isGameActive && !isTutorialPaused)
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
        isGameActive = true;
        slackerBar?.StartGame(
            () => catCount,
            (newCount) => {
                catCount = newCount;
                if (catCountText != null) catCountText.text = $"{catCount}";
            }
        );

        isCountingDown = false;

        // Set duration based on exercise type
        bool isSpecialMode = PushUpSitUpGameMode.Instance != null &&
                             PushUpSitUpGameMode.Instance.IsSpecialMode;

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

        // ── TUTORIAL: Pause game and show SlackerBar explanation ──
        ShowTutorialStep(TutorialStep.SlackerBarExplain);

        Debug.Log($"{TAG} Game STARTED. Tutorial paused for SlackerBar explanation.");
    }

    private void EndGame()
    {
        isGameActive     = false;
        isTimePaused     = false;
        isTutorialPaused = false;
        catMultiplier    = 1;
        timeLeft         = 0;
        isReadyForExerciseSelection = false;
        slackerBar?.EndGame();

        if (fireAudioSource != null)
        {
            fireAudioSource.loop = false;
            fireAudioSource.Stop();
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
        if (bonus3Panel                 != null) bonus3Panel.SetActive(false);

        // Hide tutorial panels
        HideAllTutorialPanels();
        if (iceShardTutorialPanel != null) iceShardTutorialPanel.SetActive(false);
        if (tutorialOKButton      != null) tutorialOKButton.SetActive(false);

        if (roundSummaryPanel != null)
        {
            roundSummaryPanel.SetActive(true);
            if (roundScoreText != null)
                roundScoreText.text = $"You saved {catCount} cats!";
        }

        timerPanel.SetActive(false);
        catCountPanel.SetActive(false);
        Debug.Log($"{TAG} Round ended. Cats: {catCount}");

        if (PushUpSitUpGameMode.Instance  != null) PushUpSitUpGameMode.Instance.Reset();
        if (StartCircleAnchor.Instance    != null) StartCircleAnchor.Instance.Reset();

        FreezeTimerBar freezeBar = FindFirstObjectByType<FreezeTimerBar>();
        if (freezeBar != null) freezeBar.HideBar();
    }

    // ──────────────────────────────────────────────
    // Public UI callbacks
    // ──────────────────────────────────────────────

    public void LockAnchor()
    {
        if (qrGuidePanel != null)
            qrGuidePanel.SetActive(false);
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        isAnchored                  = true;
        isReadyForExerciseSelection = false;

        if (startButton != null) startButton.SetActive(false);
        if (startPanel  != null) startPanel.SetActive(false);

        foreach (var entry in spawnedCubes)
            Debug.Log($"{TAG} Anchor locked at {entry.Value.transform.position}");

        trackedImageManager.enabled = false;

        SpawnStartCircle();

        // ── TUTORIAL: Show Walk to Circle panel ──
        ShowTutorialStep(TutorialStep.WalkToCircle);

        isWaitingAtStartLocation = true;

        Debug.Log($"{TAG} 🎯 Waiting for user to walk to start location...");
    }

    // ──────────────────────────────────────────────
    // Cat spawning
    // ──────────────────────────────────────────────
    public void SpawnCatFromCube()
    {
        // Only PushUp spawns cats at start circle
        bool isPushUpMode = selectedExercise == ExerciseType.PushUp &&
                            StartCircleAnchor.Instance != null &&
                            StartCircleAnchor.Instance.HasPosition();

        if (isPushUpMode)
        {
            // PushUp: spawn cats at START CIRCLE
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
            Debug.Log($"{TAG} 🐱 Cat spawned at START CIRCLE (PushUp). Total: {catCount}");
        }
        else
        {
            // SitUp + all others: spawn cats at QR CODE
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
            ExerciseType.HighKnee     => message == "0",
            ExerciseType.PushUp       => message == "1",
            ExerciseType.SitUp        => message == "2",
            ExerciseType.Lunge        => message == "3",
            ExerciseType.Squat        => message == "4",
            ExerciseType.OverheadHold => message == "5",
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
            if (int.TryParse(message, out int move) && move >= 0 && move <= 6)
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
            Vector3 spawnPos;

            // PushUp only: spawn at START CIRCLE
            if (selectedExercise == ExerciseType.PushUp &&
                StartCircleAnchor.Instance != null &&
                StartCircleAnchor.Instance.HasPosition())
            {
                spawnPos = StartCircleAnchor.Instance.GetStartCirclePosition();
                Debug.Log($"{TAG} Exercise prefab spawned at START CIRCLE (PushUp): {spawnPos}");
            }
            else
            {
                // SitUp + all others: spawn at QR CODE
                spawnPos = entry.Value.transform.position;
                Debug.Log($"{TAG} Exercise prefab spawned at QR CODE: {spawnPos}");
            }

            Quaternion spawnRot = entry.Value.transform.rotation;
            currentActiveExerciseModel = Instantiate(prefab, spawnPos, spawnRot);

            if (fireAudioSource != null && fireSound != null)
            {
                fireAudioSource.clip   = fireSound;
                fireAudioSource.loop   = true;
                fireAudioSource.volume = fireVolume;
                fireAudioSource.Play();
            }

            break;
        }
    }

    private GameObject GetSelectedPrefab() => selectedExercise switch
    {
        ExerciseType.HighKnee     => highKneePrefab,
        ExerciseType.PushUp       => pushUpPrefab,
        ExerciseType.SitUp        => sitUpPrefab,
        ExerciseType.Lunge        => lungePrefab,
        ExerciseType.Squat        => squatPrefab,
        ExerciseType.OverheadHold => overheadHoldPrefab,
        _                         => null
    };

    // ──────────────────────────────────────────────
    // Helpers used by demo_SpecialEffectManager
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

        wrongMovePanel.transform.SetAsLastSibling();
        wrongMovePanel.SetActive(true);
        yield return new WaitForSeconds(1f);
        wrongMovePanel.SetActive(false);
        wrongMoveCoroutine = null;
    }

    public void OnMainMenuButtonClicked()
    {
        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        Debug.Log($"{TAG} 🏠 Main menu button clicked. Loading main menu.");
        StartCoroutine(LoadMainMenuRoutine());
    }

    private System.Collections.IEnumerator LoadMainMenuRoutine()
    {
        // ✅ Re-enable AR tracking before leaving scene
        if (trackedImageManager != null)
        {
            trackedImageManager.enabled = true;
            Debug.Log($"{TAG} ✅ ARTrackedImageManager re-enabled before scene change.");
        }

        // ✅ Reset AR Session before leaving scene
        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            arSession.Reset();
            Debug.Log($"{TAG} ✅ AR Session reset before scene change.");
        }

        float clipLength = (clickSound != null) ? clickSound.length : 0.1f;
        yield return new WaitForSeconds(clipLength);

        SceneManager.LoadScene("MainMenu");
    }
}

