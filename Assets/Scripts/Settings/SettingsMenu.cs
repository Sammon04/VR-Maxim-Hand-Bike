using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{

    [SerializeField] GameSettings settings;
    [SerializeField] Slider vignetteSlider;
    [SerializeField] Slider volumeSlider;

    void Start()
    {
        vignetteSlider.value = settings.vignetteStrength;
        volumeSlider.value = settings.masterVolume;
    }
}
