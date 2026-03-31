// using UnityEngine;
// using UnityEngine.XR.ARFoundation;
// using UnityEngine.XR.ARSubsystems;
// using System.Collections.Generic;
// using UnityEngine.UI;
// using TMPro; 
// using UnityEngine.SceneManagement;
// using System.Collections.Concurrent;

// // 1. Add an Enum to keep track of the exercise type
// public enum ExerciseType { None, HighKnee, PushUp, SitUp, Lunge, Squat, OverheadHold }

// public class qrcode_many_exercise : MonoBehaviour
// {
//     private readonly ConcurrentQueue<bool> mqttSpawnQueue = new();
//     private ARTrackedImageManager trackedImageManager;
//     private const string TAG = "[AR_DEBUG]";
    
//     [Header("Prefabs")]
//     [SerializeField] private GameObject catPrefab;

//     [Header("UI Elements")]
//     [SerializeField] private TextMeshProUGUI statusText; // The text that tells the user what's happening
//     [SerializeField] private Button startButton;
//     [SerializeField] private Button spawnCatButton;
//     [SerializeField] private TextMeshProUGUI timerText;
//     [SerializeField] private GameObject gameOverPanel;
//     [SerializeField] private TextMeshProUGUI scoreText;
    

//     [Header("Game Settings")]
//     [SerializeField] private float gameDuration = 60f;
//     [SerializeField] private string mainMenuSceneName = "MainMenu";
//     private float timeLeft;
//     private int catCount = 0;
//     private bool isGameActive = false;
//     private bool isLocked = false; 
//     private bool qrCodeDetected = false;

//     [Header("Exercise Prefabs")]
//     [SerializeField] private GameObject highKneePrefab;
//     [SerializeField] private GameObject pushUpPrefab;
//     [SerializeField] private GameObject sitUpPrefab;
//     [SerializeField] private GameObject lungePrefab;
//     [SerializeField] private GameObject squatPrefab;
//     [SerializeField] private GameObject overheadHoldPrefab;
    
//     private ExerciseType selectedExercise = ExerciseType.None;

//     private Dictionary<string, GameObject> spawnedCubes = new Dictionary<string, GameObject>();

//     public void GoToMainMenu()
//     {
//         Debug.Log($"{TAG} Back Button Pressed. Returning to Scene: {mainMenuSceneName}");
        
//         // This line actually changes the scene
//         SceneManager.LoadScene(mainMenuSceneName);
//     }

//     void Awake() 
//     {
//         if(statusText != null) statusText.text = "Waiting for Exercise Command...";

//         trackedImageManager = GetComponent<ARTrackedImageManager>();

//         Debug.Log($"{TAG} CAMERA ACTIVATED: System is live and scanning for QR code...");
        
//         // Launch: Screen is empty (no buttons)
//         if(startButton != null) startButton.gameObject.SetActive(false); 
//         if(spawnCatButton != null) spawnCatButton.gameObject.SetActive(false);
//         if(timerText != null) timerText.gameObject.SetActive(false);
//         if(gameOverPanel != null) gameOverPanel.SetActive(false);
//         if(scoreText != null) scoreText.gameObject.SetActive(false);

//         timeLeft = gameDuration;
//     }

//     void OnEnable() 
//     {
//         if (trackedImageManager != null)
//         {
//             // Use .AddListener instead of +=
//             trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
//         }

//         if (MqttService.Instance != null)
//             MqttService.Instance.OnMessageReceived += HandleMqttMessage;
//     }
    
//     void OnDisable() 
//     {
//         if (trackedImageManager != null)
//         {
//             // Use .RemoveListener instead of -=
//             trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
//         }

//         if (MqttService.Instance != null)
//             MqttService.Instance.OnMessageReceived -= HandleMqttMessage;
//     }

//     // 2. Update your MQTT handler to catch "lunge" or "squat"
//     private void HandleMqttMessage(string topic, string payload) 
//     {
//         try
//         {
//            // 1. Convert the JSON string into an object
//             MqttPayload data = JsonUtility.FromJson<MqttPayload>(payload);
//             string message = data.action; // Now 'message' is just "6" or "4"
//             Debug.Log($"{TAG} MQTT Received on topic '{topic}': {payload}");

//             Debug.Log($"{TAG} MQTT Extracted Action: {message}");

//             if (topic == "ultra/action1") 
//             {
//                 // CASE 1: Mode is NOT selected yet (Setup Phase)
//                 if (selectedExercise == ExerciseType.None)
//                 {
//                     switch (message)
//                     {
//                         case "0": selectedExercise = ExerciseType.HighKnee; break;
//                         case "1": selectedExercise = ExerciseType.PushUp; break;
//                         case "2": selectedExercise = ExerciseType.SitUp; break;
//                         case "3": selectedExercise = ExerciseType.Lunge; break;
//                         case "4": selectedExercise = ExerciseType.Squat; break;
//                         case "5": selectedExercise = ExerciseType.OverheadHold; break;
//                         case "hehe": mqttSpawnQueue.Enqueue(true); break;
//                     }

//                     if (selectedExercise != ExerciseType.None)
//                     {
//                         Debug.Log($"{TAG} Mode LOCKED to {selectedExercise}. Scan QR code.");

//                         // --- UPDATE THE UI TEXT HERE ---
//                         if (statusText != null) {
//                             statusText.text = $"{selectedExercise.ToString().ToUpper()} selected! Scan QR Code.";
//                             statusText.color = Color.green; // Optional: make it green to show success
//                         }
//                     }
//                 }
//                 // CASE 2: Mode is ALREADY locked (Gameplay Phase)
//                 else 
//                 {
//                     // Check if the incoming number matches the CURRENT locked exercise
//                     bool isCorrectExercise = false;
//                     switch (selectedExercise)
//                     {
//                         case ExerciseType.HighKnee: if (message == "0") isCorrectExercise = true; break;
//                         case ExerciseType.PushUp:   if (message == "1") isCorrectExercise = true; break;
//                         case ExerciseType.SitUp:    if (message == "2") isCorrectExercise = true; break;
//                         case ExerciseType.Lunge:    if (message == "3") isCorrectExercise = true; break;
//                         case ExerciseType.Squat:    if (message == "4") isCorrectExercise = true; break;
//                         case ExerciseType.OverheadHold: if (message == "5") isCorrectExercise = true; break;
//                     }

//                     if (isCorrectExercise || message == "hehe")
//                     {
//                         mqttSpawnQueue.Enqueue(true);
//                         Debug.Log($"{TAG} {selectedExercise} detected -> Spawning Cat");
//                     }
//                 }
//             }
//         }
//         catch (System.Exception e) {
//             Debug.LogError($"{TAG} Failed to parse JSON: {e.Message}");
//         }
//     }

//     void Update()
//     {
//         // Only countdown if the game has started
//         if (isGameActive)
//         {
//             if (timeLeft > 0)
//             {
//                 timeLeft -= Time.deltaTime;
//                 timerText.text = $"Time: {Mathf.Ceil(timeLeft)}s";
//             }
//             else
//             {
//                 EndGame();
//             }
//         }

//         // 2. ADD THIS: Check the MQTT Queue and spawn a cat if a message arrived
//         if (mqttSpawnQueue.TryDequeue(out _))
//         {
//             Debug.Log($"{TAG} MQTT triggered a cat spawn!");
//             SpawnCatFromCube(); 
//         }
//     }

//     // --- START BUTTON FUNCTION ---
//     public void LockAnchorAndStart()
//     {
//         if (qrCodeDetected)
//         {
//             isLocked = true;
//             isGameActive = true;
//             catCount = 0;
//             Debug.Log($"{TAG} Start Pressed: Anchor Locked. Timer Started.");

//             // 1. Get the cube's position
//             // We look for the first value in your dictionary
//             foreach (var cubeEntry in spawnedCubes)
//             {
//                 Vector3 prefabPos = cubeEntry.Value.transform.position;
//                 Vector3 camPos = Camera.main.transform.position;

//                 // 2. Debug: Prefab coordinates
//                 Debug.Log($"{TAG} PREFAB LOCKED in place at: {prefabPos}");

//                 // 3. Debug: Camera coordinates and distance
//                 float distance = Vector3.Distance(camPos, prefabPos);
//                 Debug.Log($"{TAG} CAMERA is at: {camPos}. Distance to Prefab: {distance} meters.");
                
//                 // 4. Debug: Direction (Is it behind the camera?)
//                 Vector3 toPrefab = (prefabPos - camPos).normalized;
//                 float dotProduct = Vector3.Dot(Camera.main.transform.forward, toPrefab);
//                 if (dotProduct < 0) {
//                     Debug.LogWarning($"{TAG} WARNING: The prefab is BEHIND the camera's current view!");
//                 }
//             }

//             // Start button vanishes, Spawn Cat button and Timer appear
//             startButton.gameObject.SetActive(false);
//             spawnCatButton.gameObject.SetActive(true);
//             timerText.gameObject.SetActive(true);
//             Debug.Log($"{TAG} Spawn Cat button and Timer enabled.");
//         }
//     }

//     private void EndGame()
//     {
//         isGameActive = false;
//         timeLeft = 0;
        
//         // --- NEW OVERLAY LOGIC ---
//         if(gameOverPanel != null) gameOverPanel.SetActive(true);
//         scoreText.gameObject.SetActive(true); 
//         if(scoreText != null) scoreText.text = $"Cats Saved: {catCount} Cats!";
        
//         spawnCatButton.gameObject.SetActive(false);
//         timerText.gameObject.SetActive(false); // Hide timer when done
        
//         Debug.Log($"{TAG} Game Over: Overlay displayed.");
//         Debug.Log($"{TAG} Game Over: Final Score {catCount}");
//     }

//     public void SpawnCatFromCube()
//     {
//         if (!isGameActive) return; // Prevent spawning if timer is finished

//         foreach (var cubeEntry in spawnedCubes) //dictionary with qr code and object
//         {
//             GameObject cube = cubeEntry.Value;
//             if (cube != null)
//             {
//                 catCount++;
//                 Debug.Log($"{TAG} Cat Count: {catCount}");

//                 Vector3 spawnPos = cube.transform.position;
//                 Quaternion spawnRotation = cube.transform.rotation;
//                 int choice = Random.Range(0, 4);

//                 switch (choice)
//                 {
//                     case 0: // RIGHT
//                         spawnPos += cube.transform.right * 0.15f;
//                         spawnRotation *= Quaternion.Euler(0, 90, 0);
//                         break;
//                     case 1: // LEFT
//                         spawnPos += -cube.transform.right * 0.15f;
//                         spawnRotation *= Quaternion.Euler(0, -90, 0);
//                         break;
//                     case 2: // FRONT
//                         spawnPos += cube.transform.forward * 0.15f;
//                         spawnRotation *= Quaternion.Euler(0, 0, 0);
//                         break;
//                     case 3: // BACK
//                         spawnPos += -cube.transform.forward * 0.15f;
//                         spawnRotation *= Quaternion.Euler(0, 180, 0);
 
//                     break;
//                 }
        

//                 Instantiate(catPrefab, spawnPos, spawnRotation);
//                 Debug.Log($"{TAG} SUCCESS: Cat spawned.");
//                 return; 
//             }
//         }
//     }

//     private GameObject GetSelectedPrefab()
//     {
//         return selectedExercise switch
//         {
//             ExerciseType.HighKnee    => highKneePrefab,
//             ExerciseType.PushUp      => pushUpPrefab,
//             ExerciseType.SitUp       => sitUpPrefab,
//             ExerciseType.Lunge       => lungePrefab,
//             ExerciseType.Squat       => squatPrefab,
//             ExerciseType.OverheadHold => overheadHoldPrefab,
//             _ => null
//         };
//     }

// // 2. Fix the Function Name and Parameter Type
// // This must be named EXACTLY 'OnTrackablesChanged' to match the OnEnable line
// // 3. Update the Detection logic to use the selected prefab
//     void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
//     {
//         // Handle New Images
//         foreach (var trackedImage in eventArgs.added)
//         {
//             if (spawnedCubes.Count == 0 && selectedExercise != ExerciseType.None) 
//             {
//                 GameObject prefabToSpawn = GetSelectedPrefab();

//                 if (prefabToSpawn != null) {
//                     GameObject newCube = Instantiate(prefabToSpawn, trackedImage.transform.position, trackedImage.transform.rotation);
//                     newCube.name = "ExerciseAnchor";
//                     spawnedCubes.Add(trackedImage.referenceImage.name, newCube);

//                     qrCodeDetected = true;
//                     if(!isLocked && startButton != null) startButton.gameObject.SetActive(true);
//                     if(statusText != null) statusText.gameObject.SetActive(false);
//                 }
//             }
//             else if (selectedExercise == ExerciseType.None)
//             {
//                 Debug.LogWarning($"{TAG} QR code detected but no exercise selected. Please select an exercise via MQTT first.");
//             }
//         }

//         // Handle Movement/Updates
//         foreach (var trackedImage in eventArgs.updated)
//         {
//             if (!isLocked && spawnedCubes.TryGetValue(trackedImage.referenceImage.name, out GameObject cube))
//             {
//                 cube.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
//             }
//         }
//     }

// }

// [System.Serializable]
// public class MqttPayload {
//     public string action; // This must match the key "action" in your JSON
// }