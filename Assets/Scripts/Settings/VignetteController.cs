using UnityEngine;

public class vignetteController : MonoBehaviour
{
    [SerializeField] RectTransform vignetteRect;
    [SerializeField] float maxScale;
    [SerializeField] float minScale;
    [SerializeField] GameSettings settings;

    float scaleFactor;
    CanvasGroup canvas;

    void Awake()
    {
        canvas = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        settings.OnSettingsChanged += SetValues;
    }

    void OnDisable()
    {
        settings.OnSettingsChanged -= SetValues;
    }

    void Start()
    {
        SetValues();
    }

    void SetValues()
    {
        if (settings.vignetteStrength == 0.0)
        {
            canvas.alpha = 0;
        } else
        {
            canvas.alpha = 1;
        }
        scaleFactor = minScale + (maxScale - minScale) * (1 - settings.vignetteStrength);
        vignetteRect.localScale = Vector3.one * scaleFactor;
    }
}
