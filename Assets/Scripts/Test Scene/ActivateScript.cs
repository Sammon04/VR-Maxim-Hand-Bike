using UnityEngine;

public class ActivateScript : MonoBehaviour
{

    public AudioClip clip;
    private AudioSource source;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        source = GetComponent<AudioSource>();
    }

    public void PlaySound()
    {
        source.PlayOneShot(clip);
    }
}
