using System.Collections.Generic;
using UnityEngine;

public class PlayerRacer : MonoBehaviour, IRacer
{
    [Header("Race Mode Values")]
    [Tooltip("The name used to uniquely identify this racer in the race manager.")]
    [SerializeField] private string racerName = "Player";

    [Header("Checkpoints")]
    [Tooltip("Ordered list of checkpoint transforms this racer will target in sequence. Should match the AI racers' checkpoint list so laps/positions are directly comparable.")]
    [SerializeField] private List<Transform> checkpoints = new List<Transform>();

    [Tooltip("Distance at which the player is considered to have reached a checkpoint and advances to the next.")]
    [SerializeField] private float checkpointReachDistance = 6f;

    [Tooltip("Loop back to checkpoint 0 after the last one (for lap-based tracks). Set externally by RaceModeLogic based on totalLaps.")]
    public bool loopCheckpoints = true;

    /*
    IRacer values.
    Updated internally by this script and read by RaceModeLogic
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

    private Vector3 currentTargetPoint;

    private void Awake()
    {
        // The bike can load into non-race gamemodes, where there's no active
        // RaceModeLogic to register with. Disabling the component here means
        // Start/FixedUpdate never run in that case, so nothing below has to
        // guard against a missing race manager.
        if (ModeSettings.Mode != GameMode.NPCRace)
        {
            enabled = false;
            return;
        }

        if (checkpoints.Count > 0)
        {
            PickTargetPoint();
        }
    }

    private void Start()
    {
        RaceModeLogic.Instance.Register(this);
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

        distanceToTarget = Vector3.Distance(transform.position, currentTargetPoint);

        if (distanceToTarget <= checkpointReachDistance)
        {
            AdvanceCheckpoint();
        }
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

        Vector3 localPoint = box.center;
        currentTargetPoint = checkpoint.TransformPoint(localPoint);
    }

    private void OnDrawGizmosSelected()
    {
        if (checkpoints.Count == 0 || currentCheckpointIndex >= checkpoints.Count) return;

        Vector3 gizmoTarget = Application.isPlaying ? currentTargetPoint : checkpoints[currentCheckpointIndex].position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, gizmoTarget);
        Gizmos.DrawWireSphere(gizmoTarget, checkpointReachDistance);
    }
}