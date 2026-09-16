using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class TimeTrialLogic : MonoBehaviour
{
    [Header("Objects")]
    [Tooltip("The trigger for the start of the time trial")]
    [SerializeField] private GameObject startTrigger;

    [Tooltip("The trigger for the end of the time trial")]
    [SerializeField] private GameObject endTrigger;

    [Tooltip("Gameobject for the bike. Used to enable/disable control")]
    [SerializeField] private GameObject bike;

    [Space(10)]

    [Header("Values")]
    [Tooltip("Amount of time to count down before giving the player control")]
    [SerializeField] private float countdownTime = 0.0f;

    [Tooltip("File name used to store this level's leaderboard (saved under Application.persistentDataPath")]
    [SerializeField] private string leaderboardFileName = "TimeTrial_Leaderboard_Level1.json";

    [Tooltip("Max number of entries kept on the leaderboard")]
    [SerializeField] private int maxLeaderboardEntries = 5;

    [Header("Name Entry")]
    [Tooltip("Input field the player types their name into via the spatial keyboard")]
    [SerializeField] private TMP_InputField nameInputField;

    [Tooltip("Canvas holding the name entry UI (input field + confirm button")]
    [SerializeField] private GameObject nameEntryCanvas;

    [Space(10)]

    [Header("UI")]
    [Tooltip("The UI canvases for the time trial")]
    [SerializeField] private GameObject timeTrialUI;

    [Tooltip("Displays the time elapsed while the time trial is active")]
    [SerializeField] private TextMeshProUGUI timerDisplay;

    [Tooltip("Displays the countdown before giving the player control")]
    [SerializeField] private TextMeshProUGUI countdownDisplay;

    [Tooltip("Displays text after the time trial is finished")]
    [SerializeField] private TextMeshProUGUI finishDisplay;

    [Tooltip("Displays the leaderboard placeholder text")]
    [SerializeField] private TextMeshProUGUI leaderboardPlaceholder;

    [Tooltip("Displays the text for returning to the menu after the time trial is finished")]
    [SerializeField] private TextMeshProUGUI returnToMenuText;

    [Tooltip("Canvas holding the restart/return to menu buttons")]
    [SerializeField] private GameObject buttonCanvas;

    private float timeElapsed = 0.0f;
    private bool raceActive = false;
    private bool countingDown = false;
    private HandbikeController bikeControls;

    [System.Serializable]
    private class LeaderboardEntry
    {
        public string playerName;
        public float time;
    }

    [System.Serializable]
    private class LeaderboardData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    private string LeaderboardFilePath
    {
        get
        {
            string fileName = $"Leaderboard_{SceneManager.GetActiveScene().name}.json";
#if UNITY_EDITOR
            string scriptsFolder = Path.Combine(Application.dataPath, "Scripts");
            if (!Directory.Exists(scriptsFolder))
            {
                Directory.CreateDirectory(scriptsFolder);
            }
            return Path.Combine(scriptsFolder, fileName);
#else       
            return Path.Combine(Application.persistentDataPath, fileName);
#endif
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        SetupTrigger(startTrigger, OnStartTriggerEntered);
        SetupTrigger(endTrigger, OnEndTriggerEntered);
    }

    void Start()
    {
        StartCountdown();
        DisplayCountdown(countdownTime);
        timeTrialUI.transform.SetParent(bike.transform, true);

        buttonCanvas.SetActive(false);
    }
    void Update()
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
                countdownDisplay.text = "";
                EndCountDown();
            }
        }
        if (raceActive)
        {
            timeElapsed += Time.deltaTime;
            timerDisplay.text = timeElapsed.ToString("F3");
        }
    }

    private void StartCountdown()
    {
        if (bike)
        {
            bikeControls = bike.GetComponent<HandbikeController>();
            bikeControls.enabled = false;
        }          
        countingDown = true;
    }

    private void EndCountDown()
    {
        if (bikeControls) bikeControls.enabled = true;
    }

    private void DisplayCountdown(float time)
    {
        if (time < 0) { time = 0; }

        float seconds = Mathf.CeilToInt(time % 60);

        countdownDisplay.text = seconds.ToString();
    }
    private void SetupTrigger(GameObject triggerObj, System.Action<Collider> callback)
    {
        var forwarder = triggerObj.GetComponent<TriggerForwarder>();
        if (forwarder == null) return;

        forwarder.onTriggerEntered += callback;
    }

    private void OnStartTriggerEntered(Collider other)
    {
        StartTimeTrial();
    }

    private void OnEndTriggerEntered(Collider other)
    {
        EndTimeTrial();
    }

    private void StartTimeTrial()
    {
        raceActive = true; 
    }

    private void EndTimeTrial()
    {
        raceActive = false;
        if (bikeControls) bikeControls.enabled = false;
        timerDisplay.text = "";
        finishDisplay.text = $"Time Trial Complete!\n\nFinal Time: {timeElapsed.ToString("F3")}";

        nameEntryCanvas.SetActive(true); // shows the name input ui first before saving
    }

    public void ConfirmName()
    {
        string playerName = string.IsNullOrWhiteSpace(nameInputField.text) ? "Player" : nameInputField.text;

        nameEntryCanvas.SetActive(false);

        SaveTimeToLeaderboard(timeElapsed, playerName);
        DisplayLeaderboard();
    }

    private LeaderboardData LoadLeaderboard()
    {
        if (File.Exists(LeaderboardFilePath))
        {
            string json = File.ReadAllText(LeaderboardFilePath);
            return JsonUtility.FromJson<LeaderboardData>(json);
        }
        return new LeaderboardData();
    }

    private void SaveLeaderboard(LeaderboardData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(LeaderboardFilePath, json);
    }

    private void SaveTimeToLeaderboard(float time, string playerName)
    {
        LeaderboardData data = LoadLeaderboard();
        
        data.entries.Add(new LeaderboardEntry
        {
            playerName = playerName,
            time = time
        });

        data.entries.Sort((a,b) => a.time.CompareTo(b.time)); // fastest time first
        
        if (data.entries.Count > maxLeaderboardEntries)
        {
            data.entries.RemoveRange(maxLeaderboardEntries, data.entries.Count - maxLeaderboardEntries);
        }

        SaveLeaderboard(data);
    }

    private void DisplayLeaderboard()
    {
        LeaderboardData data = LoadLeaderboard();

        string text = "Leaderboard\n\n";

        if (data.entries.Count == 0)
        {
            text += "No times recorded yet.";
        }
        else
        {
            for (int i = 0; i < data.entries.Count; i++)
            {
                text += $"{i + 1}. {data.entries[i].playerName} - {FormatTime(data.entries[i].time)}\n";
            }
        }
        leaderboardPlaceholder.text = text;
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        float seconds = time % 60f;
        return $"{minutes:00}:{seconds:00.000}";
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene("Main Menu");
        Debug.Log("loaded main menu");
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("restarted scene");
    }

}
