// ClimberAltArmLegGameMode.cs
// Attach to XR Origin.
// Handles special game mode for Climbers (3) and AltArmLeg (4):
//   - Spawns exercise prefab at START CIRCLE position instead of building
//   - Disables all power-ups during these exercises

using UnityEngine;

public class ClimberAltArmLegGameMode : MonoBehaviour
{
    private const string TAG = "[SPECIAL_MODE]";

    public static ClimberAltArmLegGameMode Instance { get; private set; }

    private bool isSpecialMode = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Returns true if current exercise is Climbers or AltArmLeg
    /// </summary>
    public bool IsSpecialMode => isSpecialMode;

    public void SetSpecialMode(ExerciseType exercise)
    {
        isSpecialMode = (exercise == ExerciseType.Climbers || exercise == ExerciseType.AltArmLeg);
        Debug.Log($"{TAG} Special mode: {isSpecialMode} for exercise: {exercise}");
    }

    public void Reset()
    {
        isSpecialMode = false;
        Debug.Log($"{TAG} 🔄 Reset.");
    }
}