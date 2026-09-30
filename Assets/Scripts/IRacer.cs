using UnityEngine;

public interface IRacer
{
    string RacerName { get; }
    int CurrentCheckpointIndex { get; }
    float DistanceToTarget { get; }
    int LapsCompleted { get; }
    bool Finished { get; }
}
