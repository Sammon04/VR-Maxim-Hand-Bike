using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{

    [SerializeField] GameSettings settings;
    [SerializeField] Slider vignetteSlider;
    [SerializeField] Slider volumeSlider;

    void Start()
    {
        vignetteSlider.SetValueWithoutNotify(settings.vignetteStrength);
        volumeSlider.SetValueWithoutNotify(settings.masterVolume);
    }
}
