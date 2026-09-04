using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;

    [Header("Settings")]
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private int minimumPlayersToStart = 2;
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;

    private ISession currentSession;
    private bool servicesInitialized = false;
    private bool gameStarting = false;

    private async void Awake()
    {
        maxPlayers = Mathf.Clamp(maxPlayers, 1, 2);
        minimumPlayersToStart = Mathf.Clamp(minimumPlayersToStart, 1, maxPlayers);

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;

            Debug.Log("Unity Services initialized.");
            Debug.Log("Player ID: " + AuthenticationService.Instance.PlayerId);

            SetStatus("Ready");
        }
        catch (Exception e)
        {
            servicesInitialized = false;

            Debug.LogError("Failed to initialize Unity Services:");
            Debug.LogException(e);

            SetStatus("Connection error");
        }
    }

    // =========================================================
    // CREATE GAME / HOST
    // =========================================================

    public async void CreateGame()
    {
        Debug.Log("=================================");
        Debug.Log("CREATE GAME");
        Debug.Log("=================================");

        if (!servicesInitialized)
        {
            Debug.LogError("Unity Services are not initialized yet!");
            SetStatus("Still connecting...");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "NETWORK MANAGER IS NULL!\n" +
                "Make sure a NetworkManager exists and is active in the current scene."
            );

            SetStatus("NetworkManager missing");
            return;
        }

        try
        {
            SetStatus("Creating game...");

            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers
            }.WithRelayNetwork();

            Debug.Log("Creating multiplayer session...");

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log("=================================");
            Debug.Log("SESSION CREATED");
            Debug.Log("Join Code: " + currentSession.Code);
            Debug.Log("Session ID: " + currentSession.Id);
            Debug.Log("Players: " + currentSession.PlayerCount);
            Debug.Log("=================================");

            if (joinCodeText != null)
            {
                joinCodeText.text = currentSession.Code;
            }

            // Listen for players joining.
            currentSession.PlayerJoined += OnPlayerJoined;
            currentSession.PlayerLeaving += OnPlayerLeft;

            UpdatePlayerStatus();

            Debug.Log("Waiting for players...");

            // IMPORTANT:
            // We do NOT start the GameScene here anymore.
        }
        catch (Exception e)
        {
            Debug.LogError("=================================");
            Debug.LogError("CREATE GAME FAILED");
            Debug.LogError("=================================");
            Debug.LogException(e);

            SetStatus("Could not create game");
        }
    }

    // =========================================================
    // PLAYER JOINED
    // =========================================================

    private void OnPlayerJoined(string playerId)
    {
        Debug.Log("Player joined: " + playerId);

        UpdatePlayerStatus();

        // Automatically start when enough players are present.
        if (currentSession != null &&
            currentSession.PlayerCount >= minimumPlayersToStart)
        {
            Debug.Log(
                "Minimum number of players reached: " +
                currentSession.PlayerCount
            );

            StartGame();
        }
    }

    // =========================================================
    // PLAYER LEFT
    // =========================================================

    private void OnPlayerLeft(string playerId)
    {
        Debug.Log("Player left: " + playerId);

        if (!gameStarting)
        {
            UpdatePlayerStatus();
        }
    }

    // =========================================================
    // JOIN GAME / CLIENT
    // =========================================================

    public async void JoinGame()
    {
        if (!servicesInitialized)
        {
            Debug.LogError("Unity Services are not initialized yet!");
            SetStatus("Still connecting...");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "NETWORK MANAGER IS NULL!\n" +
                "Make sure a NetworkManager exists and is active in the current scene."
            );

            SetStatus("NetworkManager missing");
            return;
        }

        if (joinCodeInput == null)
        {
            Debug.LogError("Join Code InputField is not assigned!");
            return;
        }

        string code = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a join code");
            return;
        }

        try
        {
            SetStatus("Joining game...");

            Debug.Log("Joining session with code: " + code);

            currentSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            Debug.Log("=================================");
            Debug.Log("JOINED SESSION");
            Debug.Log("Session ID: " + currentSession.Id);
            Debug.Log("Players: " + currentSession.PlayerCount);
            Debug.Log("=================================");

            SetStatus("Joined! Waiting for host...");

            // The client does NOT load the GameScene itself.
            // The host controls the network scene.
        }
        catch (Exception e)
        {
            Debug.LogError("=================================");
            Debug.LogError("JOIN GAME FAILED");
            Debug.LogError("=================================");
            Debug.LogException(e);

            SetStatus("Invalid or unavailable code");
        }
    }

    // =========================================================
    // START GAME BUTTON
    // =========================================================

    public void StartGame()
    {
        if (gameStarting)
        {
            return;
        }

        if (currentSession == null)
        {
            Debug.LogError("Cannot start game: no active session.");
            SetStatus("No game created");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Cannot start game: NetworkManager is missing.");
            SetStatus("NetworkManager missing");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        Debug.Log(
            "Starting game with " +
            currentSession.PlayerCount +
            " player(s)."
        );

        LoadGameSceneAsHost();
    }

    // =========================================================
    // HOST SCENE LOADING
    // =========================================================

    private void LoadGameSceneAsHost()
    {
        if (gameStarting)
        {
            return;
        }

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("Game Scene name is empty!");
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("Cannot load GameScene: NetworkManager is missing!");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError(
                "Cannot load GameScene because this player is not the host."
            );
            return;
        }

        gameStarting = true;

        SetStatus("Starting game...");

        Debug.Log("=================================");
        Debug.Log("STARTING GAME");
        Debug.Log("Players: " + currentSession.PlayerCount);
        Debug.Log("Scene: " + gameSceneName);
        Debug.Log("=================================");

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
    }

    // =========================================================
    // UI
    // =========================================================

    private void UpdatePlayerStatus()
    {
        if (currentSession == null)
        {
            return;
        }

        int players = currentSession.PlayerCount;

        Debug.Log(
            "Players in session: " +
            players +
            "/" +
            maxPlayers
        );

        if (players >= minimumPlayersToStart)
        {
            SetStatus(
                players +
                "/" +
                maxPlayers +
                " players - Starting..."
            );
        }
        else
        {
            SetStatus(
                players +
                "/" +
                maxPlayers +
                " players - Waiting for player..."
            );
        }
    }

    private void SetStatus(string message)
    {
        Debug.Log("[SessionManager] " + message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}