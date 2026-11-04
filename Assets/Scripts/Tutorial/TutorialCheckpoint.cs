using UnityEngine;

public class TutorialCheckpoint : MonoBehaviour
{
    private bool triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            triggered = true;
            FindObjectOfType<TutorialManager>().NextStep();
        }
    }
}