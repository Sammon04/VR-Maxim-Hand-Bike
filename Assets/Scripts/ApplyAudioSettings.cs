using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;


public class ApplyAudioSettings : MonoBehaviour
{
    [SerializeField] private GameSettings settings;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";

    private static ApplyAudioSettings instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (settings != null)
        {
            settings.OnSettingsChanged += Apply;
        }
    }

    private void Start()
    {
        Apply();
    }

    private void OnDisable()
    {
        if (settings != null)
        {
            settings.OnSettingsChanged -= Apply;
        }
    }
    private void Apply()
    {
        if (settings == null || mixer == null) return;
        // convert mixer decibles to float (-80 dB is silent, 0 dB is full volume)
        float volume = settings.masterVolume;
        float db = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
        mixer.SetFloat(masterVolumeParameter, db);
    }
}

