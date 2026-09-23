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

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (checkpoints.Count > 0)
        {
            PickTargetPoint();
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
        if (checkpoints.Count == 0 || finished) return;

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

        if (lapsCompleted >= totalLaps)
        {
            finished = true;
            return;
        }

        if (currentCheckpointIndex >= checkpoints.Count)
        {
            if (loopCheckpoints)
            {
                currentCheckpointIndex = 0;
                lapsCompleted++;
            }
            else
            {
                currentCheckpointIndex--;
                finished = true;
            }
        }

        SetTargetSpeed();
        PickTargetPoint();
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
        if (rb.linearVelocity.magnitude > currentTargetSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * currentTargetSpeed;
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

    private void OnDrawGizmosSelected()
    {
        if (checkpoints.Count == 0 || currentCheckpointIndex >= checkpoints.Count) return;

        Vector3 gizmoTarget = Application.isPlaying ? currentTargetPoint : checkpoints[currentCheckpointIndex].position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, gizmoTarget);
        Gizmos.DrawWireSphere(gizmoTarget, checkpointReachDistance);
    }
}