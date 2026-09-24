using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class RaceModeLogic : MonoBehaviour
{
    public static RaceModeLogic Instance { get; private set; }

    [Header("Race Settings")]
    [Tooltip("Number of laps in the race.")]
    [SerializeField] private int totalLaps = 3;

    [Header("UI")]
    [Tooltip("Text object for the standings display.")]
    [SerializeField] private TextMeshProUGUI standingsText;

    // Populated via Register()/Unregister() rather than the Inspector,
    // since interfaces (IRacer) can't be serialized by Unity.
    private readonly List<IRacer> racers = new List<IRacer>();
    private readonly List<IRacer> finishedOrder = new List<IRacer>();
    private readonly List<IRacer> racing = new List<IRacer>();
    private readonly List<IRacer> newlyFinished = new List<IRacer>();
    private readonly List<IRacer> standings = new List<IRacer>();

    public IReadOnlyList<IRacer> Standings => standings;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(IRacer racer)
    {
        if (!racers.Contains(racer)) racers.Add(racer);
    }

    public void Unregister(IRacer racer)
    {
        racers.Remove(racer);
        finishedOrder.Remove(racer);
    }

    private void Start()
    {
        foreach (var racer in racers)
        {
            if (racer is AIRacer aiRacer)
            {
                aiRacer.totalLaps = totalLaps;
                aiRacer.loopCheckpoints = totalLaps > 1;
            }
            if (racer is PlayerRacer plrRacer)
            {
                plrRacer.totalLaps = totalLaps;
                plrRacer.loopCheckpoints = totalLaps > 1;
            }
        }
    }

    private void Update()
    {
        UpdateStandings();

        if (standingsText != null)
        {
            standingsText.text = GetStandingsText();
        }
    }

    private void UpdateStandings()
    {
        racing.Clear();
        newlyFinished.Clear();

        foreach (IRacer r in racers)
        {
            if (!r.Finished)
            {
                racing.Add(r);
            }
            else if (!finishedOrder.Contains(r))
            {
                newlyFinished.Add(r);
            }
        }

        if (newlyFinished.Count > 0)
        {
            newlyFinished.Sort(CompareRacers);
            finishedOrder.AddRange(newlyFinished);
        }

        racing.Sort(CompareRacers);

        standings.Clear();
        standings.AddRange(finishedOrder);
        standings.AddRange(racing);
    }

    private static int CompareRacers(IRacer a, IRacer b)
    {
        int lapCompare = b.LapsCompleted.CompareTo(a.LapsCompleted);
        if (lapCompare != 0) return lapCompare;

        int checkpointCompare = b.CurrentCheckpointIndex.CompareTo(a.CurrentCheckpointIndex);
        if (checkpointCompare != 0) return checkpointCompare;

        int distanceCompare = a.DistanceToTarget.CompareTo(b.DistanceToTarget);
        if (distanceCompare != 0) return distanceCompare;

        return string.CompareOrdinal(a.RacerName, b.RacerName);
    }

    public int GetPosition(IRacer racer)
    {
        return standings.IndexOf(racer) + 1;
    }

    public string GetStandingsText()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < standings.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {standings[i].RacerName}");
        }
        return sb.ToString();
    }
}