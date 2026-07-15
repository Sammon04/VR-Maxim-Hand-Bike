using UnityEngine;

public class TriggerForwarder : MonoBehaviour
{
    public System.Action<Collider> onTriggerEntered;

    private void OnTriggerEnter(Collider other)
    {
        onTriggerEntered?.Invoke(other);
    }
}
