using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.UnifiedRayTracing;

public class RaceModeLogic : MonoBehaviour
{

    [Header("Race Settings")]
    [Tooltip("Number of laps in the race.")]
    public int totalLaps = 3;

    [Tooltip("List of racers in the race. This can include both player and AI racers.")]
    public List<AIRacer> racers = new List<AIRacer>();

    [Tooltip("Text object for the standings display.")]
    public TextMeshProUGUI standingsText;

    private readonly List<AIRacer> finishedOrder = new List<AIRacer>();
    private readonly List<AIRacer> racing = new List<AIRacer>();
    private readonly List<AIRacer> newlyFinished = new List<AIRacer>();
    private readonly List<AIRacer> standings = new List<AIRacer>();
    public IReadOnlyList<AIRacer> Standings => standings;

    private void Start()
    {
        foreach (var racer in racers)
        {
            racer.totalLaps = totalLaps;
            racer.loopCheckpoints = totalLaps > 1 ? true : false;
        }
    }

    void Update()
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

        foreach (AIRacer r in racers)
        {
            if (!r.finished)
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

    private static int CompareRacers(AIRacer a, AIRacer b)
    {
        int lapCompare = b.lapsCompleted.CompareTo(a.lapsCompleted);
        if (lapCompare != 0) return lapCompare;

        int checkpointCompare = b.currentCheckpointIndex.CompareTo(a.currentCheckpointIndex);
        if (checkpointCompare != 0) return checkpointCompare;

        int distanceCompare = a.distanceToTarget.CompareTo(b.distanceToTarget);
        if (distanceCompare != 0) return distanceCompare;

        return string.CompareOrdinal(a.racerName, b.racerName);
    }

    public int GetPosition(AIRacer racer)
    {
        return standings.IndexOf(racer) + 1;
    }

    public string GetStandingsText()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < standings.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {standings[i].racerName}");
        }
        return sb.ToString();
    }
}
