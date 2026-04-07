using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;

public class CatMovement : MonoBehaviour
{
    public float speed;
    public float maxDistance;
    private Vector3 startPosition;

    [Header("Avoidance Settings")]
    public float detectionDistance = 0.5f; // How far ahead to look
    public float turnSpeed = 100f;        // How fast it pivots away from walls
    public LayerMask obstacleLayer;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // 1. Check for obstacles using a Raycast
        RaycastHit hit;
        // Shoots a line forward from the cat's position
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, transform.forward, out hit, detectionDistance))
        {
            Debug.Log($"[CatInstance] Obstacle detected: {hit.collider.name}. Turning...");
            // Rotate the cat to the right until the path is clear
            transform.Rotate(Vector3.up * turnSpeed * Time.deltaTime);
        }
        else
        {
            // 2. Move forward if the path is clear
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }

        // Self-destruct logic
        if (Vector3.Distance(startPosition, transform.position) > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    // This draws a red line in the Scene view so you can see the cat's "eyes"
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * detectionDistance);
    }
}