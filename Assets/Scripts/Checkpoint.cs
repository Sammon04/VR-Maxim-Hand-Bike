using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Tooltip("The next checkpoint in the chain\nEnabled on entering this one.\nCan be left empty.")]
    [SerializeField] private GameObject next;

    void Awake()
    {
        var forwarder = GetComponent<TriggerForwarder>();
        if (forwarder == null) return;
        forwarder.onTriggerEntered += OnEntered;
    }

    void OnEntered(Collider other)
    {
        if (next) next.SetActive(true);
        gameObject.SetActive(false);
    }
}
