using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public InputActionProperty pauseAction;
    public TextMeshProUGUI menuButtonText;
    public TextMeshProUGUI quitButtonText;
    public TextMeshProUGUI restartButtonText;
    public GameObject pauseMenuUI;

    private bool paused = false;
    private bool menuButtonSelected = false;
    private bool quitButtonSelected = false;
    private bool restartButtonSelected = false;

    private void OnEnable()
    {
        pauseAction.action.Enable();
        pauseAction.action.performed += Pause;
        Debug.Log(pauseAction.action.enabled);
    }
    private void OnDisable()
    {
        pauseAction.action.performed -= Pause;
    }   

    public void Start()
    {
        pauseMenuUI.SetActive(false);
    }

    public void Pause(InputAction.CallbackContext obj)
    {
        paused = !paused;
        //Time.timeScale = paused ? 0f : 1f;
        pauseMenuUI.SetActive(paused);
    }

    public void ReturnToMenu()
    {
        if (!menuButtonSelected)
        {
            menuButtonSelected = true;
            menuButtonText.text = "Are you sure?";

            quitButtonSelected = false;
            restartButtonSelected = false;
            quitButtonText.text = "Quit Game";
            restartButtonText.text = "Restart Session";

            return;
        }
        //Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }

    public void QuitGame()
    {
        if (!quitButtonSelected)
        {
            quitButtonSelected = true;
            quitButtonText.text = "Are you sure?";

            menuButtonText.text = "Exit to Menu";
            restartButtonText.text = "Restart Session";
            menuButtonSelected = false;
            restartButtonSelected = false;

            return;
        }

        Application.Quit();

        if (Application.isEditor)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }

    public void RestartScene()
    {
        if (!restartButtonSelected)
        {
            restartButtonSelected = true;
            restartButtonText.text = "Are you sure?";

            menuButtonText.text = "Exit to Menu";
            quitButtonText.text = "Quit Game";
            menuButtonSelected = false;
            quitButtonSelected = false;

            return;
        }
        //Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
