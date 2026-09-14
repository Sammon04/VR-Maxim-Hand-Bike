using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Basic AI bike racer: slides toward the current checkpoint using a constant
/// forward force, rotates to face its direction of travel, and clamps to a max speed.
/// Attach to the same rigidbody-driven bike object used for the player, minus
/// player input.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AIBikeController : MonoBehaviour
{
    [Header("Checkpoints")]
    [Tooltip("Ordered list of checkpoint transforms this racer will target in sequence.")]
    public List<Transform> checkpoints = new List<Transform>();

    [Tooltip("Distance at which the AI considers a checkpoint 'reached' and advances to the next.")]
    public float checkpointReachDistance = 5f;

    [Tooltip("Loop back to checkpoint 0 after the last one (for lap-based tracks).")]
    public bool loopCheckpoints = true;

    [Header("Checkpoint Targeting")]
    // Each checkpoint should have a BoxCollider stretched to the track width.
    // The AI picks a random point inside it (local X/Z) as its actual target.
    [Tooltip("Inset from the checkpoint cube's edges (local units) so targets don't land right against the track boundary.")]
    public float edgePadding = 1f;

    [Header("Movement")]
    [Tooltip("Forward thrust force applied toward the current checkpoint.")]
    public float driveForce = 4000f;

    [Tooltip("Max speed in units/sec.")]
    public float maxSpeed = 20f;

    [Tooltip("How fast the bike rotates to face its target direction (degrees/sec).")]
    public float turnSpeed = 120f;

    [Header("Turning behavior")]
    [Tooltip("Slow down on sharp turns. 1 = full slowdown on a 180 turn, 0 = no slowdown.")]
    [Range(0f, 1f)]
    public float turnSlowdownFactor = 0.6f;

    private Rigidbody rb;
    private int currentCheckpointIndex = 0;
    private Vector3 currentTargetPoint;
    private bool stopped = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (checkpoints.Count > 0)
        {
            PickTargetPoint();
        }
    }

    private void FixedUpdate()
    {
        if (checkpoints.Count == 0) return;
        if (stopped) return;

        Vector3 toTarget = currentTargetPoint - rb.position;
        toTarget.y = 0f; // ignore vertical difference, this is ground-plane movement

        float distance = toTarget.magnitude;

        // Advance to next checkpoint if close enough
        if (distance <= checkpointReachDistance)
        {
            AdvanceCheckpoint();
            return; // recalc next physics step with the new target
        }

        Vector3 direction = toTarget.normalized;

        RotateTowards(direction);
        ApplyDriveForce(direction);
        ClampSpeed();
    }

    private void AdvanceCheckpoint()
    {
        currentCheckpointIndex++;

        if (currentCheckpointIndex >= checkpoints.Count)
        {
            if (loopCheckpoints)
            {
                currentCheckpointIndex = 0;
            }
            else
            {
                currentCheckpointIndex--;
                stopped = true;
            }
        }

        PickTargetPoint();
    }

    /// <summary>
    /// Picks a random point within the current checkpoint's BoxCollider bounds
    /// (local X/Z, inset by edgePadding) and converts it to world space.
    /// Falls back to the checkpoint's transform position if it has no BoxCollider.
    /// </summary>
    private void PickTargetPoint()
    {
        Transform checkpoint = checkpoints[currentCheckpointIndex];
        BoxCollider box = checkpoint.GetComponent<BoxCollider>();

        if (box == null)
        {
            currentTargetPoint = checkpoint.position;
            return;
        }

        Vector3 half = box.size * 0.5f;

        float x = Random.Range(-half.x + edgePadding, half.x - edgePadding);
        float z = Random.Range(-half.z + edgePadding, half.z - edgePadding);

        Vector3 localPoint = box.center + new Vector3(x, 0f, z);
        currentTargetPoint = checkpoint.TransformPoint(localPoint);
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
    }

    private void ApplyDriveForce(Vector3 direction)
    {
        // Scale force down when the bike isn't facing the target much,
        // so it slows into turns instead of muscling through them.
        float facingAlignment = Vector3.Dot(transform.forward, direction); // -1 to 1
        float turnFactor = Mathf.Lerp(1f - turnSlowdownFactor, 1f, Mathf.Clamp01((facingAlignment + 1f) / 2f));

        rb.AddForce(direction * driveForce * turnFactor, ForceMode.Force);
    }

    private void ClampSpeed()
    {
        Vector3 flatVelocity = rb.linearVelocity;
        flatVelocity.y = 0f;

        if (flatVelocity.magnitude > maxSpeed)
        {
            Vector3 clamped = flatVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (checkpoints.Count == 0 || currentCheckpointIndex >= checkpoints.Count) return;

        Vector3 gizmoTarget = Application.isPlaying ? currentTargetPoint : checkpoints[currentCheckpointIndex].position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, gizmoTarget);
        Gizmos.DrawWireSphere(gizmoTarget, 0.5f);
        Gizmos.DrawWireSphere(checkpoints[currentCheckpointIndex].position, checkpointReachDistance);
    }
}