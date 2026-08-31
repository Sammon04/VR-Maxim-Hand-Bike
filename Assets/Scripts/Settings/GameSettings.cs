using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Settings/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Vignette Settings")]
    public bool enableVignette = true;
    [Range(0f, 1f)] public float vignetteStrength = 0.5f;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    public event Action OnSettingsChanged;

    public void ToggleVignette()
    {
        enableVignette = !enableVignette;
        Save();
    }

    public void SetVignetteStrength(float value)
    {
        vignetteStrength = Mathf.Clamp01(value);
        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetInt("EnableVignette", enableVignette ? 1 : 0);
        PlayerPrefs.SetFloat("VignetteStrength", vignetteStrength);
        OnSettingsChanged?.Invoke();
    }

    public void Load()
    {
        enableVignette = PlayerPrefs.GetInt("EnableVignette", 1) == 1;
        vignetteStrength = PlayerPrefs.GetFloat("VignetteStrength", 0.5f);
    }
}
