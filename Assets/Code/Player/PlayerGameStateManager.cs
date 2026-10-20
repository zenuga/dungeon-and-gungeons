using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;
using System.Threading.Tasks;

public class PlayerGameStateManager : MonoBehaviour
{
    [SerializeField] private Image allPlayersDeadImage;
    [SerializeField] private string startSceneName = "StartScreen";
    [SerializeField] private float resetDelay = 3f;

    private bool resetStarted;
    private bool resetTransitionStarted;
    private float resetTime;

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void Update()
    {
        if (resetStarted)
        {
            if (!resetTransitionStarted && Time.unscaledTime >= resetTime)
            {
                ResetGame();
            }
            return;
        }

        bool networkSessionActive = NetworkSpawnUtility.IsNetworkSessionActive;
        if (networkSessionActive && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer))
        {
            return;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        int alivePlayers = 0;
        foreach (PlayerHealth player in players)
        {
            if (player != null && player.IsAlive)
            {
                alivePlayers++;
            }
        }

        bool gameOver = players.Length > 0 && alivePlayers == 0;
        if (gameOver)
        {
            if (allPlayersDeadImage != null)
            {
                allPlayersDeadImage.gameObject.SetActive(true);
            }

            if (!networkSessionActive || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
            {
                resetStarted = true;
                resetTime = Time.unscaledTime + resetDelay;
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // The server receives this callback when a remote player leaves too.
        // Keep the host in the current game; only a disconnected client should
        // return to the start screen here.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (!resetStarted && !string.IsNullOrEmpty(startSceneName) && SceneManager.GetActiveScene().name != startSceneName)
        {
            SceneManager.LoadScene(startSceneName);
        }
    }

    private async void ResetGame()
    {
        if (resetTransitionStarted) return;
        resetTransitionStarted = true;
        Time.timeScale = 1f;

        SessionManager sessionManager = SessionManager.Instance;
        if (sessionManager != null)
        {
            await sessionManager.LeaveCurrentSessionForMenuAsync();
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            await Task.Yield();
        }

        DungeonWaveManager.ResetRunProgress();
        resetStarted = false;
        SceneManager.LoadScene(startSceneName);
    }
}
