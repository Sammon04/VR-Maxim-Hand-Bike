using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AIRacer : MonoBehaviour, IRacer
{
    [Header("Race Mode Values")]
    [Tooltip("The name used to uniquely identify this racer in the race manager.")]
    [SerializeField] private string racerName = "AI Racer";

    [Header("Checkpoints")]
    [Tooltip("Ordered list of checkpoint transforms this racer will target in sequence.")]
    [SerializeField] private List<Transform> checkpoints = new List<Transform>();

    [Tooltip("Distance at which the AI considers a checkpoint 'reached' and advances to the next.")]
    [SerializeField] private float checkpointReachDistance = 5f;

    [Tooltip("Loop back to checkpoint 0 after the last one (for lap-based tracks). Set externally by RaceModeLogic based on totalLaps.")]
    public bool loopCheckpoints = true;

    [Header("Checkpoint Targeting")]
    [Tooltip("Inset from the checkpoint cube's edges (local units) so targets don't land right against the track boundary.")]
    [SerializeField] private float edgePadding = 1f;

    [Header("Movement")]
    [Tooltip("Forward thrust force applied toward the current checkpoint.")]
    [SerializeField] private float accelForce = 4000f;

    [Tooltip("Target speed in units/sec.")]
    [SerializeField] private float targetSpeed = 20f;

    [Tooltip("Max target speed variance applied at each checkpoint")]
    [SerializeField] private float speedVariance = 2f;

    [Tooltip("Maximum target speed range")]
    [SerializeField] private float maxSpeedVariance = 5f;

    [Tooltip("How fast the bike rotates to face its target direction (degrees/sec).")]
    [SerializeField] private float turnSpeed = 120f;

    [Header("Turning behavior")]
    [Tooltip("Slow down on sharp turns. 1 = full slowdown on a 180 turn, 0 = no slowdown.")]
    [Range(0f, 1f)]
    [SerializeField] private float turnSlowdownFactor = 0.6f;

    [Tooltip("Minimum upcoming turn angle before the AI begins slowing down.")]
    [SerializeField] private float lookAheadTurnThreshold = 30f;

    [Tooltip("Distance before a sharp turn at which the AI begins slowing down.")]
    [SerializeField] private float lookAheadSlowdownDistance = 20f;

    [Tooltip("Lowest percentage of target speed allowed for extremely sharp upcoming turns.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float sharpTurnSpeedFactor = 0.4f;

    [Header("Visuals")]
    [Tooltip("List of transforms for rotating the wheel components")]
    [SerializeField] private Transform[] wheels;

    /*
    IRacer values.
    These are updated internally by this script and read by RaceModeLogic
    through the interface properties below to determine standings.
    */
    private int lapsCompleted = 0;
    private float distanceToTarget = 0f;
    private int currentCheckpointIndex = 0;
    private bool finished;

    public string RacerName => racerName;
    public int CurrentCheckpointIndex => currentCheckpointIndex;
    public float DistanceToTarget => distanceToTarget;
    public int LapsCompleted => lapsCompleted;
    public bool Finished => finished;

    [HideInInspector] public int totalLaps = 1; // Set externally by RaceModeLogic.

    private Rigidbody rb;
    private float currentTargetSpeed;
    private Vector3 currentTargetPoint;
    private readonly float wheelRadius = 1.0f;
    public bool active = false;
    private float nextTurnSharpness = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (checkpoints.Count > 0)
        {
            PickTargetPoint();
            nextTurnSharpness = GetUpcomingTurnSharpness();
        }
    }

    private void Start()
    {
        RaceModeLogic.Instance.Register(this);
        currentTargetSpeed = targetSpeed;
        SetTargetSpeed();
    }

    private void OnDestroy()
    {
        if (RaceModeLogic.Instance != null)
        {
            RaceModeLogic.Instance.Unregister(this);
        }
    }

    private void FixedUpdate()
    {
        if (checkpoints.Count == 0 || finished || !active) return;

        Vector3 toTarget = currentTargetPoint - rb.position;
        distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= checkpointReachDistance)
        {
            AdvanceCheckpoint();
            return;
        }

        Vector3 direction = toTarget.normalized;
        RotateTowards(direction);
        ApplyAccelForce(direction);
        ClampSpeed();
        SpinWheels(-Vector3.Dot(rb.linearVelocity, transform.forward));
    }

    private void AdvanceCheckpoint()
    {
        currentCheckpointIndex++;

        if (currentCheckpointIndex >= checkpoints.Count)
        {
            lapsCompleted++;

            if (lapsCompleted >= totalLaps)
            {
                currentCheckpointIndex = checkpoints.Count - 1;
                finished = true;
                return;
            }

            currentCheckpointIndex = 0;
        }

        SetTargetSpeed();
        PickTargetPoint();
        nextTurnSharpness = GetUpcomingTurnSharpness();
    }

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
        float y = half.y;
        float z = Random.Range(-half.z + edgePadding, half.z - edgePadding);

        Vector3 localPoint = box.center + new Vector3(x, -y, z);
        currentTargetPoint = checkpoint.TransformPoint(localPoint);
    }

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
    }

    private void ApplyAccelForce(Vector3 direction)
    {
        // Scale force down when the bike isn't facing the target much,
        // so it slows into turns instead of muscling through them.
        float facingAlignment = Vector3.Dot(transform.forward, direction); // -1 to 1
        float turnFactor = Mathf.Lerp(1f - turnSlowdownFactor, 1f, Mathf.Clamp01((facingAlignment + 1f) / 2f));

        rb.AddForce(direction * accelForce * turnFactor, ForceMode.Force);
    }

    private void ClampSpeed()
    {
        float allowedSpeed = currentTargetSpeed;
        //float turnSharpness = GetUpcomingTurnSharpness();

        if (nextTurnSharpness > 0f)
        {
            // 0 when far away, 1 when at the target checkpoint.
            float approachFactor = Mathf.InverseLerp(
                lookAheadSlowdownDistance,
                0f,
                distanceToTarget
            );

            // A sharper turn results in a lower target speed.
            float turnSpeedFactor = Mathf.Lerp(
                1f,
                sharpTurnSpeedFactor,
                nextTurnSharpness
            );

            // The closer we get to the turn, the more strongly
            // the reduced speed limit is applied.
            allowedSpeed = Mathf.Lerp(
                currentTargetSpeed,
                currentTargetSpeed * turnSpeedFactor,
                approachFactor
            );
        }

        if (rb.linearVelocity.magnitude > allowedSpeed)
        {
            rb.linearVelocity =
                rb.linearVelocity.normalized * allowedSpeed;
        }
    }

    private void SetTargetSpeed()
    {
        currentTargetSpeed += Random.Range(-speedVariance, speedVariance);
        currentTargetSpeed = Mathf.Clamp(currentTargetSpeed, targetSpeed - maxSpeedVariance, targetSpeed + maxSpeedVariance);
    }

    private void SpinWheels(float currentSpeed)
    {
        float spinSpeed = currentSpeed / wheelRadius * Mathf.Rad2Deg;

        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(Vector3.forward * spinSpeed * Time.fixedDeltaTime, Space.Self);
            }
        }
    }

    private float GetUpcomingTurnSharpness()
    {
        int nextCheckpointIndex = currentCheckpointIndex + 1;

        // If this is the last checkpoint, only look toward checkpoint 0
        // if another lap still remains.
        if (nextCheckpointIndex >= checkpoints.Count)
        {
            if (lapsCompleted < totalLaps - 1)
            {
                nextCheckpointIndex = 0;
            }
            else
            {
                return 0f;
            }
        }

        Vector3 incomingDirection = currentTargetPoint - rb.position;

        Vector3 nextCheckpointPoint = GetCheckpointCenter(checkpoints[nextCheckpointIndex]);

        Vector3 outgoingDirection =
            nextCheckpointPoint - currentTargetPoint;

        incomingDirection.y = 0f;
        outgoingDirection.y = 0f;

        if (incomingDirection.sqrMagnitude < 0.001f ||
            outgoingDirection.sqrMagnitude < 0.001f)
        {
            return 0f;
        }

        float turnAngle = Vector3.Angle(
            incomingDirection.normalized,
            outgoingDirection.normalized
        );

        if (turnAngle <= lookAheadTurnThreshold)
        {
            return 0f;
        }

        return Mathf.InverseLerp(
            lookAheadTurnThreshold,
            180f,
            turnAngle
        );
    }

    private Vector3 GetCheckpointCenter(Transform checkpoint)
    {
        BoxCollider box = checkpoint.GetComponent<BoxCollider>();

        if (box == null)
        {
            return checkpoint.position;
        }

        return checkpoint.TransformPoint(box.center);
    }

    private void OnDrawGizmosSelected()
    {
        if (checkpoints.Count == 0 || currentCheckpointIndex >= checkpoints.Count) return;

        Vector3 gizmoTarget = Application.isPlaying ? currentTargetPoint : checkpoints[currentCheckpointIndex].position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, gizmoTarget);
        Gizmos.DrawWireSphere(gizmoTarget, checkpointReachDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(gizmoTarget, lookAheadSlowdownDistance);
    }
}