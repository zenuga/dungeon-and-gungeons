using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class StartScreen : MonoBehaviour
{
    public GameObject startScreenUI; 
    public GameObject settingsUI; 
    private bool sceneTransitionStarted;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (startScreenUI != null) startScreenUI.transform.localScale = Vector3.one;
        if (settingsUI == null) return;

        settingsUI.transform.localScale = Vector3.one;
        Settings settings = settingsUI.GetComponent<Settings>();
        if (settings == null) settings = settingsUI.AddComponent<Settings>();
        settings.Configure(settingsUI);
    }

    public async void StartGame()
    {
        if (sceneTransitionStarted) return;
        sceneTransitionStarted = true;
        Time.timeScale = 1f;
        DungeonWaveManager.ResetRunProgress();

        if (SessionManager.Instance != null)
        {
            await SessionManager.Instance.LeaveCurrentSessionForMenuAsync();
        }

        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            await Task.Yield();
        }

        // Make sure "SampleScene" matches your scene's exact file name in Build Settings
        SceneManager.LoadScene("SampleScene");
    }

    public async void GoBackToStartScreen()
    {
        if (sceneTransitionStarted) return;
        sceneTransitionStarted = true;
        Time.timeScale = 1f;

        if (SessionManager.Instance != null)
        {
            await SessionManager.Instance.LeaveCurrentSessionForMenuAsync();
        }

        Unity.Netcode.NetworkManager networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            await Task.Yield();
        }

        DungeonWaveManager.ResetRunProgress();
        SceneManager.LoadScene("StartScreen");
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
