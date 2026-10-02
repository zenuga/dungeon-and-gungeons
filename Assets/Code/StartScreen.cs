using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreen : MonoBehaviour
{
    public GameObject startScreenUI; 
    public GameObject settingsUI; 

    private const string FullscreenPreference = "FullscreenEnabled";

    private void Awake()
    {
        bool fullscreen = PlayerPrefs.GetInt(FullscreenPreference, Screen.fullScreen ? 1 : 0) == 1;
        Screen.fullScreen = fullscreen;

        if (settingsUI == null) return;

        Toggle[] toggles = settingsUI.GetComponentsInChildren<Toggle>(true);
        foreach (Toggle toggle in toggles)
        {
            toggle.SetIsOnWithoutNotify(fullscreen);
            toggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenPreference, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void StartGame()
    {
        // Make sure "SampleScene" matches your scene's exact file name in Build Settings
        SceneManager.LoadScene("SampleScene");
    }

    public void Settings()
    {
        if (startScreenUI != null) startScreenUI.SetActive(false);
        if (settingsUI != null) settingsUI.SetActive(true);
    }

    public void SettingsClose()
    {
        if (settingsUI != null) settingsUI.SetActive(false);
        if (startScreenUI != null) startScreenUI.SetActive(true);
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
