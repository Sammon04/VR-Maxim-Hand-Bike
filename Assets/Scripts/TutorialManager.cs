using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    public TextMeshProUGUI instructionText;
    public string[] steps;
    private int currentStep = 0;

    void Start()
    {
        ShowStep(0);
    }

    void Update()
    {
        if (currentStep == 0 && Input.anyKeyDown)
        {
            NextStep();
        }
    }

    public void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Length) return;
        currentStep = index;
        instructionText.text = steps[index];
    }

    public void NextStep()
    {
        ShowStep(currentStep + 1);
    }
}