// StartCircleAnchor.cs
// Attach to XR Origin.
// Stores the start circle position so it can be used by other scripts.

using UnityEngine;

public class StartCircleAnchor : MonoBehaviour
{
    public static StartCircleAnchor Instance { get; private set; }

    private Vector3 startCirclePosition;
    private bool hasPosition = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetStartCirclePosition(Vector3 pos)
    {
        startCirclePosition = pos;
        hasPosition = true;
        Debug.Log($"[START_CIRCLE_ANCHOR] 📍 Start circle position saved: {pos}");
    }

    public Vector3 GetStartCirclePosition()
    {
        if (!hasPosition)
            Debug.LogWarning("[START_CIRCLE_ANCHOR] ⚠️ Position not set yet!");
        return startCirclePosition;
    }

    public bool HasPosition() => hasPosition;

    public void Reset()
    {
        hasPosition = false;
        Debug.Log("[START_CIRCLE_ANCHOR] 🔄 Position reset.");
    }
}