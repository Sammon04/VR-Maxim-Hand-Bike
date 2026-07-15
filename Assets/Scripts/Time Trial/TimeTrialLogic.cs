using UnityEngine;
using TMPro;

public class TimeTrialLogic : MonoBehaviour
{
    public GameObject starttrigger;
    public GameObject endtrigger;

    public TextMeshProUGUI timerDisplay;

    private float timeElapsed = 0.0f;
    private bool raceActive = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        SetupTrigger(starttrigger, OnStartTriggerEntered);
        SetupTrigger(endtrigger, OnEndTriggerEntered);
    }

    private void SetupTrigger(GameObject triggerObj, System.Action<Collider> callback)
    {
        var forwarder = triggerObj.GetComponent<TriggerForwarder>();
        if (forwarder == null)
        {
            forwarder = triggerObj.AddComponent<TriggerForwarder>();
        }

        forwarder.onTriggerEntered += callback;
    }

    private void OnStartTriggerEntered(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log("Race Started");
        raceActive = true;
    }

    private void OnEndTriggerEntered(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log("Race Finished");
        raceActive = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (raceActive)
        {
            timeElapsed += Time.deltaTime;
            Debug.Log(timeElapsed);
            timerDisplay.text = timeElapsed.ToString();
        }
    }
}
