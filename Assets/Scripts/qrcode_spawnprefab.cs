// using UnityEngine;
// using UnityEngine.XR.ARFoundation;
// using UnityEngine.XR.ARSubsystems;
// using System.Collections.Generic;

// [RequireComponent(typeof(ARTrackedImageManager))]
// public class qrcode_spawnprefab : MonoBehaviour
// {
//     private ARTrackedImageManager trackedImageManager;
//     private const string TAG = "[AR_DEBUG]";
    
//     [SerializeField]
//     private GameObject cubePrefab;

//     // Dictionary to keep track of spawned cubes so we can update them later
//     private Dictionary<string, GameObject> spawnedCubes = new Dictionary<string, GameObject>();

//     void Awake() 
//     {
//         trackedImageManager = GetComponent<ARTrackedImageManager>();
        
//         if (trackedImageManager == null) {
//             Debug.LogError($"{TAG} ERROR: This script is NOT on the same object as the ARTrackedImageManager!");
//         } else if (trackedImageManager.referenceLibrary == null) {
//             Debug.LogError($"{TAG} ERROR: You forgot to assign the Library to the Manager in the Inspector!");
//         } else {
//             Debug.Log($"{TAG} Logic Connected! Library has {trackedImageManager.referenceLibrary.count} images.");
//         }
//     }

//     void OnEnable() => trackedImageManager.trackedImagesChanged += OnChanged;
//     void OnDisable() => trackedImageManager.trackedImagesChanged -= OnChanged;

//     void OnChanged(ARTrackedImagesChangedEventArgs eventArgs)
//     {
//         // If you see this in the console, the camera is at least detecting something!
//         if(eventArgs.added.Count > 0 || eventArgs.updated.Count > 0) {
//             Debug.Log($"{TAG} I see SOMETHING. Added: {eventArgs.added.Count} | Updated: {eventArgs.updated.Count}");
//         }
//         // 1. HANDLE NEW IMAGES (SPAWNING)
//         foreach (var trackedImage in eventArgs.added)
//         {
//             string imageName = trackedImage.referenceImage.name;
//             Debug.Log($"{TAG} NEW Image Detected: {imageName} at {trackedImage.transform.position}");
            
//             if (cubePrefab != null && !spawnedCubes.ContainsKey(imageName))
//             {
//                 // Instantiate the cube
//                 GameObject newCube = Instantiate(cubePrefab, trackedImage.transform.position, trackedImage.transform.rotation);
//                 newCube.name = $"Cube_{imageName}";
                
//                 // Add to our dictionary to track it
//                 spawnedCubes.Add(imageName, newCube);
//                 Debug.Log($"{TAG} SUCCESS: Spawned {newCube.name}");
//             }
//         }

//         // 2. HANDLE UPDATED IMAGES (POSITION SYNC)
//         // This runs every frame the image is being moved or tilted
//         foreach (var trackedImage in eventArgs.updated)
//         {
//             string imageName = trackedImage.referenceImage.name;

//             if (spawnedCubes.ContainsKey(imageName))
//             {
//                 GameObject cube = spawnedCubes[imageName];

//                 // Update position and rotation to match the paper exactly
//                 cube.transform.position = trackedImage.transform.position;
//                 cube.transform.rotation = trackedImage.transform.rotation;

//                 // Optional: Hide the cube if tracking is "Limited" (e.g., camera is obscured)
//                 bool isTracking = trackedImage.trackingState == TrackingState.Tracking;
//                 cube.SetActive(isTracking);
//             }
//         }

//         // 3. HANDLE REMOVED IMAGES
//         foreach (var trackedImage in eventArgs.removed)
//         {
//             string imageName = trackedImage.referenceImage.name;
//             if (spawnedCubes.ContainsKey(imageName))
//             {
//                 Destroy(spawnedCubes[imageName]);
//                 spawnedCubes.Remove(imageName);
//                 Debug.Log($"{TAG} REMOVED: {imageName} cube destroyed.");
//             }
//         }
//     }

//     void Update() {
//         if (trackedImageManager.subsystem != null && !trackedImageManager.subsystem.running) {
//             Debug.LogWarning($"{TAG} Subsystem exists but is NOT RUNNING. Try restarting the AR Session.");
//         }
//         if (trackedImageManager.subsystem == null) {
//             Debug.LogWarning($"{TAG} The AR Subsystem is NULL. Check your XR Plug-in Management settings!");
//         } else if (trackedImageManager.referenceLibrary == null) {
//             Debug.LogError($"{TAG} The Reference Library is MISSING from the Manager!");
//         }
//     }
// }