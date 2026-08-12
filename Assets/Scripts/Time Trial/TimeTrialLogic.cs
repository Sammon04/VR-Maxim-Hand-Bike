using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    [SerializeField] private GameObject buttons;

    private float timeElapsed = 0.0f;
    private bool raceActive = false;
    private bool countingDown = false;
    private HandbikeController bikeControls;
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
        buttons.SetActive(false);
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
        leaderboardPlaceholder.text = "Leaderboard Placeholder\n\n1. Player 1 - 00:00.000\n2. Player 2 - 00:00.000\n3. Player 3 - 00:00.000";
        buttons.SetActive(true);
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
