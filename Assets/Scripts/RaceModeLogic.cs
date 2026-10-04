using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.VisualScripting;
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

    [Tooltip("Parent for UI Canvases")]
    [SerializeField] private GameObject raceModeUI;

    [Tooltip("Text object for the countdown display.")]
    [SerializeField] private TextMeshProUGUI countdownText;

    [SerializeField] private GameObject startButtonanvas;

    [Header("Other")]
    [SerializeField] private GameObject bike;
    [SerializeField] private HandbikeController playerBikeControls;
    [SerializeField] private float countdownTime = 3f;




    // Populated via Register()/Unregister() rather than the Inspector,
    // since interfaces (IRacer) can't be serialized by Unity.
    private readonly List<IRacer> racers = new List<IRacer>();
    private readonly List<IRacer> finishedOrder = new List<IRacer>();
    private readonly List<IRacer> racing = new List<IRacer>();
    private readonly List<IRacer> newlyFinished = new List<IRacer>();
    private readonly List<IRacer> standings = new List<IRacer>();

    public IReadOnlyList<IRacer> Standings => standings;

    private bool active = false;
    private bool countingDown = false;

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
        playerBikeControls.enabled = false;
        raceModeUI.transform.SetParent(bike.transform, true);

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
        if (countingDown)
        {
            if (countdownTime >= 0)
            {
                countdownTime -= Time.deltaTime;
                DisplayCountdown(countdownTime);
            }
            else
            {
                countingDown = false;
                countdownText.text = "";
                StartRace();
            }
        }

        if (!active) return;

        UpdateStandings();

        if (standingsText != null)
        {
            string newStandingsText = GetStandingsText();

            if (standingsText.text != newStandingsText)
            {
                standingsText.text = newStandingsText;
            }
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
            string racerText = $"{standings[i].RacerName}";

            if (i == 0)
            {
                racerText = $"<color=#FFD700>{racerText}</color>"; // Gold
            }
            else if (i == 1)
            {
                racerText = $"<color=#C0C0C0>{racerText}</color>"; // Silver
            }
            else if (i == 2)
            {
                racerText = $"<color=#CD7F32>{racerText}</color>"; // Bronze
            }

            sb.AppendLine(racerText);
        }
        return sb.ToString();
    }

    public void StartRace()
    {
        active = true;

        foreach (var racer in racers)
        {
            if (racer is AIRacer aiRacer)
            {
                aiRacer.active = true;
            }
            playerBikeControls.enabled = true;
        }
    }

    public void startCountdown()
    {
        countingDown = true;
        startButtonanvas.SetActive(false);

    }

    private void DisplayCountdown(float time)
    {
        if (time < 0) { time = 0; }

        float seconds = Mathf.CeilToInt(time % 60);

        countdownText.text = seconds.ToString();
    }
}