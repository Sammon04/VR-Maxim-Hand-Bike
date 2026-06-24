using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShowEnvironments : MonoBehaviour
{
    public GameObject EnvironmentsPanel;
    public RawImage photoDisplay;
    public string forestPic = "forest.jpg";
    public string raceTrackPic = "RaceTrack.jpg";

    public void ToggleEnvironmentsPanel()
    {
        if (EnvironmentsPanel.activeSelf == true)
        {
            EnvironmentsPanel.SetActive(false);
        }
        else
        {
            EnvironmentsPanel.SetActive(true);
        }
    }
    public void LoadForestPhoto()
    {
        LoadPhoto(forestPic);
    }

    public void LoadRacePhoto()
    {
        LoadPhoto(raceTrackPic);
    }

    public void LoadPhoto(string fileName)
    {
        string folderPath = Path.Combine(Application.dataPath, "Scenes/Main Menu Assets");
        string path = Path.Combine(folderPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError("File not found: " + path);
            return;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        photoDisplay.texture = tex;
    }

    public void LoadForestScene()
    {
        SceneManager.LoadScene("Forest Track");
    }

    public void LoadRaceTrackScene()
    {
        SceneManager.LoadScene("Race Track");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EnvironmentsPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
