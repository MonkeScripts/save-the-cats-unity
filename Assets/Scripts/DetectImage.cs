using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTrackingLogger : MonoBehaviour
{
    private const string TAG = "[AR_DEBUG]";
    
    [SerializeField]
    private ARTrackedImageManager trackedImageManager;

    private void OnEnable()
    {
        // Subscribe to the event when images are added, updated, or removed
        trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
    }

    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Loop through all newly detected images
        foreach (var trackedImage in eventArgs.added)
        {
            Debug.Log($"{TAG} Seen: {trackedImage.referenceImage.name}");
        }

        // Optional: Log when an existing image is updated (e.g., moved)
        foreach (var trackedImage in eventArgs.updated)
        {
            // Only log if it's currently being tracked actively
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                 // You can add logic here if you need continuous tracking logs
            }
        }
    }
}