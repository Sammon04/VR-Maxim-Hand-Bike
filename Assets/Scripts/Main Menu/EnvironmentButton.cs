using UnityEngine;
using UnityEngine.SceneManagement;

public class EnvironmentButton : MonoBehaviour
{
    [SerializeField] private GameMode mode;
    [SerializeField] private string SceneName;

    public void Load()
    {
        ModeSettings.Mode = mode;
        SceneManager.LoadScene(SceneName);
    }
}