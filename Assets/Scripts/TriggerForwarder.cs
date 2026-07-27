using UnityEngine;

public class TriggerForwarder : MonoBehaviour
{
    [Tooltip("The body entering the trigger must have this tag for the trigger to be invoked.\nLeave blank for any tag.")]
    [SerializeField] private string requiredTag = "";

    public System.Action<Collider> onTriggerEntered;

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }
        onTriggerEntered?.Invoke(other);
    }
}
