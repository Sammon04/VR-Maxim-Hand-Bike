using UnityEngine;

public class ActivateScript : MonoBehaviour
{

    public AudioClip clip;
    public GameSettings settings;
    private AudioSource source;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        source = GetComponent<AudioSource>();
        UpdateVolume();
    }

    public void PlaySound()
    {
        source.PlayOneShot(clip);
    }

    private void UpdateVolume()
    {
        source.volume = settings.masterVolume;
    }

    private void OnEnable()
    {
        settings.OnSettingsChanged += UpdateVolume;
    }

    private void OnDisable()
    {
        settings.OnSettingsChanged -= UpdateVolume;
    }
}
