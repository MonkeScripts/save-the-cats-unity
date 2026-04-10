// PushUpSitUpGameMode.cs
// Attach to XR Origin.
// Handles special game mode for PushUp (1) and SitUp (2):
//   - Spawns exercise prefab at START CIRCLE position instead of building
//   - Disables all power-ups during these exercises

using UnityEngine;

public class PushUpSitUpGameMode : MonoBehaviour
{
    private const string TAG = "[SPECIAL_MODE]";

    public static PushUpSitUpGameMode Instance { get; private set; }

    private bool isSpecialMode = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Returns true if current exercise is PushUp or SitUp
    /// </summary>
    public bool IsSpecialMode => isSpecialMode;

    public void SetSpecialMode(ExerciseType exercise)
    {
        isSpecialMode = (exercise == ExerciseType.PushUp || exercise == ExerciseType.SitUp);
        Debug.Log($"{TAG} Special mode: {isSpecialMode} for exercise: {exercise}");
    }

    public void Reset()
    {
        isSpecialMode = false;
        Debug.Log($"{TAG} 🔄 Reset.");
    }
}