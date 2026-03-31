using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ImageTrackingManager : MonoBehaviour
{
    private ARTrackedImageManager trackedImageManager;
    private const string TAG = "[AR_DEBUG]";
    
    [SerializeField]
    private GameObject cubePrefab;

    void Awake() 
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
        
        // Check if you forgot to drag the prefab in!
        if (cubePrefab == null) {
            Debug.LogError($"{TAG} FATAL: Cube Prefab is MISSING in the Inspector!");
        } else {
            Debug.Log($"{TAG} ImageTrackingManager Initialized with Prefab: {cubePrefab.name}");
        }
    }

    void OnEnable() => trackedImageManager.trackedImagesChanged += OnChanged;
    void OnDisable() => trackedImageManager.trackedImagesChanged -= OnChanged;

    void OnChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"{TAG} DETECTED: {trackedImage.referenceImage.name} at {trackedImage.transform.position}");
            
            if (cubePrefab != null)
            {
                // Instantiate as a child of the tracked image
                GameObject spawnedCube = Instantiate(cubePrefab, trackedImage.transform);
                spawnedCube.name = "AR_Cube_Instance";
                
                // VERIFICATION LOG
                if (spawnedCube != null) {
                    Debug.Log($"{TAG} SUCCESS: Spawned {spawnedCube.name} at {spawnedCube.transform.position}");
                } else {
                    Debug.LogError($"{TAG} FAILED: Instantiate returned null!");
                }
            }
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            // Log if the position moves away from (0,0,0)
            if (trackedImage.transform.position != Vector3.zero)
            {
                // Un-comment this if you want to see the coordinates moving in real-time
                // Debug.Log($"{TAG} UPDATED: {trackedImage.referenceImage.name} is now at {trackedImage.transform.position}");
            }
        }
    }
}