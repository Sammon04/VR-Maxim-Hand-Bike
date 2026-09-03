using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Settings/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Vignette Settings")]
    [Range(0f, 1f)] public float vignetteStrength = 0.0f;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    public event Action OnSettingsChanged;

    public void SetVignetteStrength(float value)
    {
        vignetteStrength = Mathf.Clamp01(value);
        OnSettingsChanged?.Invoke();
        Save();
    }

    public void SetGameVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        OnSettingsChanged?.Invoke();
        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetFloat("VignetteStrength", vignetteStrength);
        PlayerPrefs.SetFloat("GameVolume", masterVolume);
        OnSettingsChanged?.Invoke();
    }

    public void Load()
    {
        vignetteStrength = PlayerPrefs.GetFloat("VignetteStrength", 0.5f);
        masterVolume = PlayerPrefs.GetFloat("GameVolume", 0.5f);
    }
}
