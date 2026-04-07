using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement;
using System.Collections.Concurrent;

// 1. Add an Enum to keep track of the exercise type
public enum ExerciseType { None, Squat, Lunge, HighKnee, Climbers, AltArmLeg, ChairTricep }

public class scan_qrcode_exercise_final : MonoBehaviour
{
    private readonly ConcurrentQueue<bool> mqttSpawnQueue = new();
    private Coroutine wrongMoveCoroutine;
    private ARTrackedImageManager trackedImageManager;
    private const string TAG = "[AR_DEBUG]";
    
    [Header("Prefabs")]
    [SerializeField] private GameObject catPrefab;

    [Header("UI Elements")]
    [SerializeField] private GameObject scanningPanel;
    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject waitingExercisePanel;
    [SerializeField] private GameObject exerciseSelectedPanel;
    [SerializeField] private TextMeshProUGUI exerciseText;
    [SerializeField] private GameObject catCountPanel;
    [SerializeField] private TextMeshProUGUI catCountText;
    [SerializeField] private Button spawnCatButton;
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject wrongMovePanel;
    

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
    [SerializeField] private AudioClip tickSound; // The "Beep" for 3, 2, 1
    [SerializeField] private AudioClip goSound;   // The "BEEP" for GO!
    [SerializeField] private AudioClip meowSound;    // NEW: Sound for each cat
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip wrongMoveSound;
    [SerializeField] private AudioClip fireSound;

    [Header("Round Transition UI")]
    [SerializeField] private GameObject roundSummaryPanel; // The intermediate panel
    [SerializeField] private TextMeshProUGUI roundScoreText; // "Cats saved this round: X"
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button finalEndButton;

    [Header("Water Bucket Settings")]
    [SerializeField] private GameObject waterBucketPrefab;
    [SerializeField] private GameObject rainEffectPrefab;
    [SerializeField] private float bucketSpawnDistance = 0.15f;    // 15cm away from building
    [SerializeField] private float bucketTriggerDistance = 0.3f;   // How close phone must be
    [SerializeField] private float rainDuration = 5f;              // Rain lasts 5 seconds
    [SerializeField] private float bucketSpawnTime = 15f;          // Spawn at 15s remaining
    
    private ExerciseType selectedExercise = ExerciseType.None;
    private Dictionary<string, GameObject> spawnedCubes = new Dictionary<string, GameObject>();
    private float timeLeft;
    private int catCount = 0;
    private bool isGameActive = false;
    private bool isAnchored = false;
    private bool isCountingDown = false;
    private bool triggerWrongMoveUI = false;
    private GameObject currentActiveExerciseModel;
    private GameObject spawnedWaterBucket;
    private GameObject spawnedRainEffect;
    private bool bucketHasSpawned = false;
    private bool bucketCollected = false;
    private bool isRaining = false;

    void Awake() 
    {
        Debug.Log($"{TAG} System Awake: Initializing AR Exercise Game.");
        // Initial State: Just scanning for QR
        trackedImageManager = GetComponent<ARTrackedImageManager>();

        if(startButton != null) startButton.SetActive(false);
        if(spawnCatButton != null) spawnCatButton.gameObject.SetActive(false);
        if(timerText != null) timerText.gameObject.SetActive(false);
        if(countdownText != null) countdownText.gameObject.SetActive(false);
        if(gameOverPanel != null) gameOverPanel.SetActive(false);
        if(startPanel != null) startPanel.SetActive(false);
        if(scanningPanel != null) scanningPanel.SetActive(true);
        if(waitingExercisePanel != null) waitingExercisePanel.SetActive(false);
        if(exerciseSelectedPanel != null) exerciseSelectedPanel.SetActive(false);

        timeLeft = gameDuration;

        catCount = 0;
        catCountText.text = "0";
    }

    void OnEnable() 
    {
        if (trackedImageManager != null)
        {
            // Use .AddListener instead of +=
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived += HandleMqttMessage;
    }
    
    void OnDisable() 
    {
        if (trackedImageManager != null)
        {
            // Use .RemoveListener instead of -=
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        if (MqttService.Instance != null)
            MqttService.Instance.OnMessageReceived -= HandleMqttMessage;
    }

    private void HandleMqttMessage(string topic, string payload) 
    {
        try
        {
           // 1. Convert the JSON string into an object
            MqttPayload data = JsonUtility.FromJson<MqttPayload>(payload);
            string message = data.action;
            Debug.Log($"{TAG} MQTT Received on topic '{topic}': {payload}");

            Debug.Log($"{TAG} MQTT Extracted Action: {message}");

            if (topic == "ultra/action1") 
            {
                // CASE 1: Mode is NOT selected yet (Setup Phase)
                if (selectedExercise == ExerciseType.None)
                {
                    switch (message)
                    {
                        case "1": selectedExercise = ExerciseType.Squat;       break;
                        case "2": selectedExercise = ExerciseType.Lunge;       break;
                        case "3": selectedExercise = ExerciseType.HighKnee;    break;
                        case "4": selectedExercise = ExerciseType.Climbers;    break;
                        case "5": selectedExercise = ExerciseType.AltArmLeg;   break;
                        case "6": selectedExercise = ExerciseType.ChairTricep; break;
                    }

                    if (selectedExercise != ExerciseType.None)
                    {
                        Debug.Log($"{TAG} Exercise {selectedExercise} Received. Starting Countdown.");
                        mqttSpawnQueue.Enqueue(false);
                    }
                }
                // CASE 2: Mode is ALREADY locked (Gameplay Phase)
                else if (isGameActive)
                {
                    CheckExerciseCompletion(message);
                }
            }
        }
        catch (System.Exception e) {
            Debug.LogError($"{TAG} MQTT Error: {e.Message}");
        }
    }

    System.Collections.IEnumerator StartCountdownRoutine()
    {
        isCountingDown = true;
        Debug.Log($"{TAG} 3-2-1 COUNTDOWN STARTED for {selectedExercise}");
        if (waitingExercisePanel != null) waitingExercisePanel.SetActive(false);
        exerciseSelectedPanel.SetActive(true);
        exerciseText.text = $"{selectedExercise}";
        countdownText.gameObject.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            Debug.Log($"{TAG} Countdown: {i}...");
            countdownText.text = i.ToString() + "...";
            // --- PLAY TICK SOUND ---
            if (audioSource != null && tickSound != null)
            {
                audioSource.PlayOneShot(tickSound);
            }
            yield return new WaitForSeconds(1.0f);
        }

        countdownText.text = "GO!";
        // --- PLAY GO SOUND ---
        if (audioSource != null && goSound != null)
        {
            audioSource.PlayOneShot(goSound);
        }
        Debug.Log($"{TAG} GAME START! Spawned Exercise Prefab & Timer active.");
        yield return new WaitForSeconds(0.5f);
        countdownText.gameObject.SetActive(false);
        exerciseSelectedPanel.SetActive(false);

        // ACTUALLY START THE GAME
        SpawnExercisePrefab(); // Instantiate the real exercise model at the locked coords
        isGameActive = true;
        isCountingDown = false;
        timerPanel.SetActive(true);
        catCountPanel.SetActive(true);
        timerText.gameObject.SetActive(true);
    }

    void CheckExerciseCompletion(string message)
    {
        bool isCorrect = false;
        switch (selectedExercise)
        {
            case ExerciseType.Squat:       if (message == "1") isCorrect = true; break;
            case ExerciseType.Lunge:       if (message == "2") isCorrect = true; break;
            case ExerciseType.HighKnee:    if (message == "3") isCorrect = true; break;
            case ExerciseType.Climbers:    if (message == "4") isCorrect = true; break;
            case ExerciseType.AltArmLeg:   if (message == "5") isCorrect = true; break;
            case ExerciseType.ChairTricep: if (message == "6") isCorrect = true; break;
        }

        if (isCorrect)
        {
            Debug.Log($"{TAG} REPETITION DETECTED: Correct {selectedExercise} performed!");
            mqttSpawnQueue.Enqueue(true); // Trigger cat spawn
        }
        else
        {
            Debug.Log($"{TAG} WRONG MOVE: Expected {selectedExercise}, but got action {message}");
            if (int.TryParse(message, out int move) && move >= 1 && move <= 6)
            {
                Debug.Log($"{TAG} Wrong move detected: {message}");
                triggerWrongMoveUI = true;
            }
        }   
    }

    void SpawnExercisePrefab()
    {
        // --- NEW: DESTROY THE OLD MODEL IF IT EXISTS ---
        if (currentActiveExerciseModel != null)
        {
            Destroy(currentActiveExerciseModel);
            Debug.Log($"{TAG} Destroyed previous exercise model.");
        }

        GameObject prefabToSpawn = GetSelectedPrefab();
        
        foreach (var entry in spawnedCubes)
        {
            // --- NEW: SAVE THE NEW MODEL TO OUR VARIABLE ---
            currentActiveExerciseModel = Instantiate(prefabToSpawn, entry.Value.transform.position, entry.Value.transform.rotation);
            if (audioSource != null && fireSound != null)
            {
                audioSource.clip = fireSound;
                audioSource.loop = true;
                audioSource.Play();
            }
            break; 
        }
    }

    void Update()
    {
        // Handle MQTT Queue
        if (mqttSpawnQueue.TryDequeue(out bool isCatSpawn))
        {
            if (isCatSpawn) {
                SpawnCatFromCube();
            } else {
                // This means an exercise was selected and we need to start the countdown
                StartCoroutine(StartCountdownRoutine());
            }
        }

        if (isGameActive)
        {
            if (timeLeft > 0)
            {
                timeLeft -= Time.deltaTime;
                timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s";

                // ✅ NEW: Check if it's time to spawn the bucket
                if (!bucketHasSpawned && timeLeft <= bucketSpawnTime)
                {
                    SpawnWaterBucket();
                }

                // ✅ NEW: Check if phone is near the bucket
                if (bucketHasSpawned && !bucketCollected && spawnedWaterBucket != null)
                {
                    CheckPhoneNearBucket();
                }
            }
            else { EndGame(); }
        }

        if (triggerWrongMoveUI)
        {
            triggerWrongMoveUI = false; // Reset the flag immediately
            
            if (wrongMoveCoroutine == null) 
            {
                wrongMoveCoroutine = StartCoroutine(ShowWrongMoveRoutine());
            }
        }
    }

    private void EndGame()
    {
        isGameActive = false;
        timeLeft = 0;

        // 1. Stop looping sounds
        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.Stop();
        }

        // 2. Play victory
        if (audioSource != null && victorySound != null)
        {
            audioSource.PlayOneShot(victorySound);
        }

        // 3. Clean up exercise model
        if (currentActiveExerciseModel != null)
        {
            Destroy(currentActiveExerciseModel);
        }

        // ✅ 4. Clean up bucket and rain (BEFORE showing UI)
        if (spawnedWaterBucket != null)
        {
            Destroy(spawnedWaterBucket);
            Debug.Log($"{TAG} 🧹 Bucket cleaned up on game end.");
        }
        if (spawnedRainEffect != null)
        {
            Destroy(spawnedRainEffect);
            Debug.Log($"{TAG} 🧹 Rain cleaned up on game end.");
        }

        // 5. Show summary panel
        if(roundSummaryPanel != null) 
        {
            roundSummaryPanel.SetActive(true);
            if(roundScoreText != null) roundScoreText.text = $"You saved {catCount} cats!";
        }

        // 6. Hide gameplay UI
        timerPanel.SetActive(false);
        catCountPanel.SetActive(false);
        Debug.Log($"{TAG} Round Ended. Waiting for player choice (Again/End).");
    }
    
    public void PlayAgain()
    {
        // 1. Hide the summary panel
        if(roundSummaryPanel != null) roundSummaryPanel.SetActive(false);

        // 2. Reset the state for a new exercise selection
        selectedExercise = ExerciseType.None;
        timeLeft = gameDuration; // Reset timer
        isGameActive = false; // Stay false until MQTT + Countdown finishes
        waitingExercisePanel.SetActive(true);

        Debug.Log($"{TAG} Player chose AGAIN. Ready for new MQTT command.");

        bucketHasSpawned = false;
        bucketCollected = false;
        isRaining = false;
        Debug.Log($"{TAG} 🔄 Bucket and rain flags reset for new round.");
    }

    public void ShowFinalResults()
    {
        if(roundSummaryPanel != null) roundSummaryPanel.SetActive(false);
        
        // Show the actual final Game Over panel
        if(gameOverPanel != null) gameOverPanel.SetActive(true);
        if(finalScoreText != null) finalScoreText.text = $"{catCount}";
        
        // Play final victory sound
        if (audioSource != null && victorySound != null)
        {
            audioSource.PlayOneShot(victorySound);
        }

        Debug.Log($"{TAG} Game Ended Permanently. Final Score: {catCount}");
    }


    public void SpawnCatFromCube()
    {
        if (!isGameActive) return; // Prevent spawning if timer is finished

        foreach (var cubeEntry in spawnedCubes) //dictionary with qr code and object
        {
            GameObject cube = cubeEntry.Value;
            if (cube != null)
            {
                catCount++;

                if (catCountText != null) catCountText.text = $"{catCount}";

                Debug.Log($"{TAG} Cat Count: {catCount}");
                // --- PLAY MEOW SOUND ---
                if (audioSource != null && meowSound != null)
                {
                    audioSource.PlayOneShot(meowSound);
                }
                //StartCoroutine(ShowFeedbackRoutine("CAT SAVED!"));

                Vector3 spawnPos = cube.transform.position;
                Quaternion spawnRotation = cube.transform.rotation;
                int choice = Random.Range(0, 4);

                switch (choice)
                {
                    case 0: // RIGHT
                        spawnPos += cube.transform.right * 0.15f;
                        spawnRotation *= Quaternion.Euler(0, 90, 0);
                        break;
                    case 1: // LEFT
                        spawnPos += -cube.transform.right * 0.15f;
                        spawnRotation *= Quaternion.Euler(0, -90, 0);
                        break;
                    case 2: // FRONT
                        spawnPos += cube.transform.forward * 0.15f;
                        spawnRotation *= Quaternion.Euler(0, 0, 0);
                        break;
                    case 3: // BACK
                        spawnPos += -cube.transform.forward * 0.15f;
                        spawnRotation *= Quaternion.Euler(0, 180, 0);
 
                    break;
                }
        

                Instantiate(catPrefab, spawnPos, spawnRotation);
                Debug.Log($"{TAG} SUCCESS: Cat spawned.");
                return; 
            }
        }
    }

    private GameObject GetSelectedPrefab()
    {
        return selectedExercise switch
        {
            ExerciseType.Squat       => squatPrefab,
            ExerciseType.Lunge       => lungePrefab,
            ExerciseType.HighKnee    => highKneePrefab,
            ExerciseType.Climbers    => climbersPrefab,
            ExerciseType.AltArmLeg   => altArmLegPrefab,
            ExerciseType.ChairTricep => chairTricepPrefab,
            _ => null
        };
    }

    public void LockAnchor()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
        isAnchored = true;
        startButton.SetActive(false);
        startPanel.SetActive(false);
        waitingExercisePanel.SetActive(true);


        foreach (var entry in spawnedCubes)
        {
            Vector3 lockedPos = entry.Value.transform.position;
            Debug.Log($"{TAG} COORDINATES SAVED: Anchor locked at {lockedPos}");
        }        
        // This stops the AR camera from trying to "correct" the position anymore
        trackedImageManager.enabled = false; 
    }

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"{TAG} QR CODE DETECTED: {trackedImage.referenceImage.name} at {trackedImage.transform.position}");
            // If we haven't locked an anchor yet, create a temporary one
            if (!isAnchored && !spawnedCubes.ContainsKey(trackedImage.referenceImage.name))
            {
                // Spawn a simple marker (or your catPrefab) just to show where the QR is
                GameObject tempAnchor = new GameObject("TempAnchor");
                tempAnchor.transform.position = trackedImage.transform.position;
                spawnedCubes.Add(trackedImage.referenceImage.name, tempAnchor);
                
                if (audioSource != null && clickSound != null)
                {
                    audioSource.PlayOneShot(clickSound);
                }

                startButton.SetActive(true); // Show the Start button
                scanningPanel.SetActive(false); // Hide the scanning instructions
                startPanel.SetActive(true); // Show the start instructions
            }
        }
        
        // Keep moving the temp anchor until it is locked
        if (!isAnchored)
        {
            foreach (var trackedImage in eventArgs.updated)
            {
                if (spawnedCubes.TryGetValue(trackedImage.referenceImage.name, out GameObject cube))
                {
                    cube.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
                }
            }
        }
    }

    void SpawnWaterBucket()
    {
        bucketHasSpawned = true;

        // Find the building's position
        Vector3 buildingPosition = Vector3.zero;
        foreach (var entry in spawnedCubes)
        {
            buildingPosition = entry.Value.transform.position;
            break;
        }

        // Spawn bucket 15cm to the RIGHT of the building
        Vector3 bucketPosition = buildingPosition + new Vector3(bucketSpawnDistance, 0f, 0f);

        spawnedWaterBucket = Instantiate(waterBucketPrefab, bucketPosition, Quaternion.identity);

        // 🪲 DEBUG LOGS
        Debug.Log($"{TAG} ⏱️ 15 SECONDS LEFT! Water bucket spawned!");
        Debug.Log($"{TAG} 🏠 Building Position: {buildingPosition}");
        Debug.Log($"{TAG} 🪣 Bucket Spawned At: {bucketPosition}");
        Debug.Log($"{TAG} 📱 Phone needs to get within {bucketTriggerDistance}m of bucket to collect it.");
    }

    void CheckPhoneNearBucket()
    {
        Vector3 phonePosition = Camera.main.transform.position;
        Vector3 bucketPosition = spawnedWaterBucket.transform.position;
        float distance = Vector3.Distance(phonePosition, bucketPosition);

        // 🪲 DEBUG: Print phone position every second (use a timer to avoid spam)
        Debug.Log($"{TAG} 📱 Phone Position: {phonePosition}");
        Debug.Log($"{TAG} 🪣 Bucket Position: {bucketPosition}");
        Debug.Log($"{TAG} 📏 Distance from phone to bucket: {distance:F2}m (need < {bucketTriggerDistance}m)");

        if (distance < bucketTriggerDistance)
        {
            Debug.Log($"{TAG} ✅ PHONE IS NEAR BUCKET! Collecting bucket now...");
            CollectWaterBucket();
        }
    }

    void CollectWaterBucket()
    {
        bucketCollected = true;

        // Destroy the bucket
        Destroy(spawnedWaterBucket);
        Debug.Log($"{TAG} 🪣 Bucket DESTROYED (collected by player).");

        // Find building position to place rain above it
        Vector3 buildingPosition = Vector3.zero;
        foreach (var entry in spawnedCubes)
        {
            buildingPosition = entry.Value.transform.position;
            break;
        }

        // Place rain slightly above the building
        Vector3 rainPosition = buildingPosition + new Vector3(0f, -1.5f, 0f);

        // Spawn the rain effect
        spawnedRainEffect = Instantiate(rainEffectPrefab, rainPosition, Quaternion.identity);

        Debug.Log($"{TAG} 🌧️ Rain effect SPAWNED at: {rainPosition}");

        // Give bonus cats
        catCount += 5;
        if (catCountText != null) catCountText.text = $"{catCount}";
        Debug.Log($"{TAG} 🐱 BONUS! +5 cats saved! New total: {catCount}");

        // Start the rain timer coroutine
        StartCoroutine(RainRoutine());
    }


    private System.Collections.IEnumerator ShowWrongMoveRoutine()
    {
        // Safety check: if the panel isn't assigned, stop here to avoid a crash
        if (wrongMovePanel == null)
        {
            Debug.LogError($"{TAG} ERROR: Wrong Move Panel is not assigned in the Inspector!");
            yield break;
        }

        // 1. Play the sound (Check if audioSource exists first)
        if (audioSource != null && wrongMoveSound != null)
        {
            audioSource.PlayOneShot(wrongMoveSound);
        }

        // 2. Show the panel exactly where it is placed in the Unity Editor
        wrongMovePanel.SetActive(true);

        // 3. Wait for 1 second
        yield return new WaitForSeconds(1.0f);

        // 4. Hide the panel
        wrongMovePanel.SetActive(false);

        // 5. Reset the coroutine reference so we can trigger it again
        wrongMoveCoroutine = null;
    }

    private System.Collections.IEnumerator RainRoutine()
    {
        isRaining = true;
        Debug.Log($"{TAG} 🌧️ Rain APPEARS. Will last {rainDuration} seconds.");
        Debug.Log($"{TAG} 🌧️ Rain Position: {spawnedRainEffect.transform.position}");

        yield return new WaitForSeconds(rainDuration);

        if (spawnedRainEffect != null)
        {
            Destroy(spawnedRainEffect);
            Debug.Log($"{TAG} ☀️ Rain DISAPPEARS after {rainDuration} seconds.");
        }

        isRaining = false;
    }

}


[System.Serializable]
public class MqttPayload {
    public string action; // This must match the key "action" in your JSON
}