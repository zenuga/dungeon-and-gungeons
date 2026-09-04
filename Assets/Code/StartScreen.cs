using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScreen : MonoBehaviour
{
    public GameObject startScreenUI; 
    public GameObject settingsUI; 

    public void StartGame()
    {
        // Make sure "SampleScene" matches your scene's exact file name in Build Settings
        SceneManager.LoadScene("SampleScene");
    }

    public void Settings()
    {
        startScreenUI.SetActive(false);
        settingsUI.SetActive(true);
    }

    public void SettingsClose()
    {
        settingsUI.SetActive(false);
        startScreenUI.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
        
        #if UNITY_EDITOR
        // Allows Application.Quit() to work while testing inside the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}