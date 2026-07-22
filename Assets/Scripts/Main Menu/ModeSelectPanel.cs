using UnityEngine;

public class ModeSelectPanel : MonoBehaviour
{
    public GameObject EnvironmentsPanel;

    public void ToggleEnvironmentsPanel()
    {
        EnvironmentsPanel.SetActive(!EnvironmentsPanel.activeSelf);
    }
    void Start()
    {
        EnvironmentsPanel.SetActive(false);
    }
}
