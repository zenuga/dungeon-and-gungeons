using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;

public class PlayerGameStateManager : MonoBehaviour
{
    [SerializeField] private Image allPlayersDeadImage;
    [SerializeField] private string startSceneName = "SampleScene";
    [SerializeField] private float resetDelay = 2f;

    private bool resetStarted;
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
            if (Time.unscaledTime >= resetTime)
            {
                ResetGame();
            }
            return;
        }

        if (NetworkManager.Singleton == null)
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

        if (players.Length >= 2 && alivePlayers == 0)
        {
            if (allPlayersDeadImage != null)
            {
                allPlayersDeadImage.gameObject.SetActive(true);
            }

            if (NetworkManager.Singleton.IsServer)
            {
                resetStarted = true;
                resetTime = Time.unscaledTime + resetDelay;
            }
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!resetStarted && !string.IsNullOrEmpty(startSceneName) && SceneManager.GetActiveScene().name != startSceneName)
        {
            SceneManager.LoadScene(startSceneName);
        }
    }

    private void ResetGame()
    {
        resetStarted = false;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(startSceneName);
    }
}