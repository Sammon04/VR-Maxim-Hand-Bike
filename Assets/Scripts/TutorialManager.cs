using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{

    [SerializeField] private HandbikeController playerBikeControls;
    [SerializeField] private InputActionProperty rightGripHeld;
    [SerializeField] private InputActionProperty leftGripHeld;

    public TextMeshProUGUI instructionText;
    public string[] steps;
    private int currentStep = 0;

    private void OnEnable()
    {
        rightGripHeld.action.Enable();
        leftGripHeld.action.Enable();
    }

    private void OnDisable()
    {
        rightGripHeld.action.Disable();
        leftGripHeld.action.Disable();
    }

    void Start()
    {
        ShowStep(0);
    }

    void Update()
    {
        if (currentStep == 0)
        {
            if (rightGripHeld.action.ReadValue<float>() > 0.5f && leftGripHeld.action.ReadValue<float>() > 0.5f)
            {
                NextStep();
            }
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

        if (currentStep == steps.Length - 1)
        {
            playerBikeControls.setControl(false);
        }
    }
}