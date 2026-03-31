using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement;
using System.Collections.Concurrent;

// 1. Add an Enum to keep track of the exercise type
public enum ExerciseType { None, HighKnee, PushUp, SitUp, Lunge, Squat, OverheadHold }

public class scan_qrcode_exercise_final : MonoBehaviour
{
    private readonly ConcurrentQueue<bool> mqttSpawnQueue = new();
    private ARTrackedImageManager trackedImageManager;
    private const string TAG = "[AR_DEBUG]";
    
    [Header("Prefabs")]
    [SerializeField] private GameObject catPrefab;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI statusText; // The text that tells the user what's happening
    [SerializeField] private Button startButton;
    [SerializeField] private Button spawnCatButton;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private GameObject lockButtonObject;
    [SerializeField] private TextMeshProUGUI feedbackText; // New text for "CAT SAVED!"
    

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    private float timeLeft;
    private int catCount = 0;
    private bool isGameActive = false;
    private bool isAnchored = false;
    private bool isCountingDown = false;

    [Header("Exercise Prefabs")]
    [SerializeField] private GameObject highKneePrefab;
    [SerializeField] private GameObject pushUpPrefab;
    [SerializeField] private GameObject sitUpPrefab;
    [SerializeField] private GameObject lungePrefab;
    [SerializeField] private GameObject squatPrefab;
    [SerializeField] private GameObject overheadHoldPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickSound; // The "Beep" for 3, 2, 1
    [SerializeField] private AudioClip goSound;   // The "BEEP" for GO!
    [SerializeField] private AudioClip meowSound;    // NEW: Sound for each cat
    [SerializeField] private AudioClip victorySound;

    [Header("Round Transition UI")]
    [SerializeField] private GameObject roundSummaryPanel; // The intermediate panel
    [SerializeField] private TextMeshProUGUI roundScoreText; // "Cats saved this round: X"
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button finalEndButton;
    
    private ExerciseType selectedExercise = ExerciseType.None;

    private Dictionary<string, GameObject> spawnedCubes = new Dictionary<string, GameObject>();

    public void GoToMainMenu()
    {
        Debug.Log($"{TAG} Back Button Pressed. Returning to Scene: {mainMenuSceneName}");
        
        // This line actually changes the scene
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void Awake() 
    {
        Debug.Log($"{TAG} System Awake: Initializing AR Exercise Game.");
        // Initial State: Just scanning for QR
        if(statusText != null) statusText.text = "Scan QR Code to Start...";
        trackedImageManager = GetComponent<ARTrackedImageManager>();

        if(lockButtonObject != null) lockButtonObject.SetActive(false);
        if(spawnCatButton != null) spawnCatButton.gameObject.SetActive(false);
        if(timerText != null) timerText.gameObject.SetActive(false);
        if(countdownText != null) countdownText.gameObject.SetActive(false);
        if(gameOverPanel != null) gameOverPanel.SetActive(false);

        timeLeft = gameDuration;
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
                        case "0": selectedExercise = ExerciseType.HighKnee; break;
                        case "1": selectedExercise = ExerciseType.PushUp; break;
                        case "2": selectedExercise = ExerciseType.SitUp; break;
                        case "3": selectedExercise = ExerciseType.Lunge; break;
                        case "4": selectedExercise = ExerciseType.Squat; break;
                        case "5": selectedExercise = ExerciseType.OverheadHold; break;
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
        statusText.text = $"{selectedExercise.ToString().ToUpper()} SELECTED!";
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
        if(statusText != null) statusText.gameObject.SetActive(false);

        // ACTUALLY START THE GAME
        SpawnExercisePrefab(); // Instantiate the real exercise model at the locked coords
        isGameActive = true;
        timerText.gameObject.SetActive(true);
        isCountingDown = false;
    }

    void CheckExerciseCompletion(string message)
    {
        bool isCorrect = false;
        switch (selectedExercise)
        {
            case ExerciseType.HighKnee: if (message == "0") isCorrect = true; break;
            case ExerciseType.PushUp:   if (message == "1") isCorrect = true; break;
            case ExerciseType.SitUp:    if (message == "2") isCorrect = true; break;
            case ExerciseType.Lunge:    if (message == "3") isCorrect = true; break;
            case ExerciseType.Squat:    if (message == "4") isCorrect = true; break;
            case ExerciseType.OverheadHold: if (message == "5") isCorrect = true; break;
        }

        if (isCorrect)
        {
            Debug.Log($"{TAG} REPETITION DETECTED: Correct {selectedExercise} performed!");
            mqttSpawnQueue.Enqueue(true); // Trigger cat spawn
        }
    }

    void SpawnExercisePrefab()
    {
        GameObject prefabToSpawn = GetSelectedPrefab();
        // Get the position from our saved dictionary
        foreach (var entry in spawnedCubes)
        {
            Instantiate(prefabToSpawn, entry.Value.transform.position, entry.Value.transform.rotation);
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
            }
            else { EndGame(); }
        }
    }

    // private void EndGame()
    // {
    //     isGameActive = false;
    //     timeLeft = 0;

    //     // --- PLAY VICTORY FANFARE ---
    //     if (audioSource != null && victorySound != null)
    //     {
    //         audioSource.PlayOneShot(victorySound);
    //     }
        
    //     // --- NEW OVERLAY LOGIC ---
    //     if(gameOverPanel != null) gameOverPanel.SetActive(true);
    //     scoreText.gameObject.SetActive(true); 
    //     if(scoreText != null) scoreText.text = $"Cats Saved: {catCount} Cats!";
        
    //     spawnCatButton.gameObject.SetActive(false);
    //     timerText.gameObject.SetActive(false); // Hide timer when done
        
    //     Debug.Log($"{TAG} Game Over: Overlay displayed.");
    //     Debug.Log($"{TAG} Game Over: Final Score {catCount}");
    // }

    private void EndGame()
    {
        isGameActive = false;
        timeLeft = 0;

        // --- PLAY VICTORY FANFARE ---
         if (audioSource != null && victorySound != null)
         {
             audioSource.PlayOneShot(victorySound);
         }

        // Show the intermediate panel instead of the final game over
        if(roundSummaryPanel != null) 
        {
            roundSummaryPanel.SetActive(true);
            if(roundScoreText != null) roundScoreText.text = $"Round Complete! You saved {catCount} cats so far!";
        }
        
        // Hide gameplay UI
        timerText.gameObject.SetActive(false);
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
        
        // 3. Update UI to wait for next MQTT command
        if(statusText != null) 
        {
            statusText.gameObject.SetActive(true);
            statusText.text = "Waiting for Exercise Command...";
            statusText.color = Color.yellow;
        }

        Debug.Log($"{TAG} Player chose AGAIN. Ready for new MQTT command.");
    }

    public void ShowFinalResults()
    {
        if(roundSummaryPanel != null) roundSummaryPanel.SetActive(false);
        
        // Show the actual final Game Over panel
        if(gameOverPanel != null) gameOverPanel.SetActive(true);
        if(scoreText != null) scoreText.text = $"FINAL SCORE: {catCount} Cats Saved!";
        
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
                Debug.Log($"{TAG} Cat Count: {catCount}");
                // --- PLAY MEOW SOUND ---
                if (audioSource != null && meowSound != null)
                {
                    audioSource.PlayOneShot(meowSound);
                }
                StartCoroutine(ShowFeedbackRoutine("CAT SAVED!"));

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
            ExerciseType.HighKnee    => highKneePrefab,
            ExerciseType.PushUp      => pushUpPrefab,
            ExerciseType.SitUp       => sitUpPrefab,
            ExerciseType.Lunge       => lungePrefab,
            ExerciseType.Squat       => squatPrefab,
            ExerciseType.OverheadHold => overheadHoldPrefab,
            _ => null
        };
    }

    public void LockAnchor()
    {
        isAnchored = true;
        lockButtonObject.SetActive(false);
        statusText.text = "Waiting for Exercise Command...";
        statusText.color = Color.yellow;

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
                
                lockButtonObject.SetActive(true); // Show the Lock button
                statusText.text = "QR Detected. Press Lock.";
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

    // Add this Coroutine to handle the "Cat Saved!" pop-up
    private System.Collections.IEnumerator ShowFeedbackRoutine(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.gameObject.SetActive(true);
            
            // Wait for 1 second so the user can see it
            yield return new WaitForSeconds(1.0f);
            
            feedbackText.gameObject.SetActive(false);
        }
    }

}

[System.Serializable]
public class MqttPayload {
    public string action; // This must match the key "action" in your JSON
}