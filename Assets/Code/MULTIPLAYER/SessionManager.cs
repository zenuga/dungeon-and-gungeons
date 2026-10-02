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
    private bool joinInProgress = false;
    private bool shuttingDown = false;
    private bool waitingToStartGame = false;
    private int sessionGeneration;

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
        if (GetComponent<GameAudioManager>() == null) gameObject.AddComponent<GameAudioManager>();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeToNetworkManager();

        await InitializeUnityServices();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        shuttingDown = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetcodeClientConnected;
        }

        if (currentSession != null)
        {
            currentSession.PlayerJoined -= OnPlayerJoined;
            currentSession.PlayerLeaving -= OnPlayerLeft;
        }

        Instance = null;
    }

    private void SubscribeToNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetcodeClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnNetcodeClientConnected;
        }
    }

    private void OnNetcodeClientConnected(ulong clientId)
    {
        if (currentSession != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost &&
            currentSession.PlayerCount >= minimumPlayersToStart)
        {
            WaitForPlayersAndStartGame();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (shuttingDown)
        {
            return;
        }

        if (scene.name == "StartScreen")
        {
            if (currentSession != null)
            {
                _ = LeaveCurrentSessionForMenuAsync();
            }
            else
            {
                gameStarting = false;
                waitingToStartGame = false;
                joinInProgress = false;
            }

            joinCodeText = GameObject.Find("JoinCodeText")?.GetComponent<TMP_Text>();
            joinCodeInput = GameObject.Find("JoinCodeInput")?.GetComponent<TMP_InputField>();
            SetStatus("Ready");
            return;
        }

        if (scene.name == gameSceneName)
        {
            SetStatus("In game");
        }
    }

    public async Task LeaveCurrentSessionForMenuAsync()
    {
        int generation = ++sessionGeneration;
        gameStarting = false;
        waitingToStartGame = false;
        joinInProgress = false;

        ISession session = currentSession;
        currentSession = null;
        if (session == null)
        {
            SetStatus("Ready");
            return;
        }

        session.PlayerJoined -= OnPlayerJoined;
        session.PlayerLeaving -= OnPlayerLeft;
        try
        {
            Task leaveTask = session.LeaveAsync();
            if (await Task.WhenAny(leaveTask, Task.Delay(2000)) == leaveTask)
            {
                await leaveTask;
            }
            else
            {
                Debug.LogWarning("The previous multiplayer session is still closing; returning to the menu anyway.");
                _ = ObserveSessionLeaveAsync(leaveTask);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not leave the previous multiplayer session cleanly: {exception.Message}");
        }

        if (generation == sessionGeneration)
        {
            gameStarting = false;
            waitingToStartGame = false;
            joinInProgress = false;
            SetStatus("Ready");
        }
    }

    private static async Task ObserveSessionLeaveAsync(Task leaveTask)
    {
        try
        {
            await leaveTask;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Previous multiplayer session cleanup failed: {exception.Message}");
        }
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

            if (shuttingDown || !Application.isPlaying)
            {
                return;
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
        if (shuttingDown || !Application.isPlaying)
        {
            return;
        }

        sessionGeneration++;

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
            SubscribeToNetworkManager();
            SetStatus("Creating game...");

            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers
            }.WithRelayNetwork();

            Debug.Log("Creating multiplayer session...");

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            if (shuttingDown || !Application.isPlaying)
            {
                return;
            }

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

            // The second player can arrive while CreateSessionAsync is still awaiting.
            // Check the current count after subscribing so that join event cannot be missed.
            if (currentSession.PlayerCount >= minimumPlayersToStart)
            {
                WaitForPlayersAndStartGame();
            }

            Debug.Log("Waiting for players...");

            // IMPORTANT:
            // We do NOT start the GameScene here anymore.
        }
        catch (Exception e)
        {
            if (shuttingDown || !Application.isPlaying)
            {
                return;
            }

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

            WaitForPlayersAndStartGame();
        }
    }

    private async void WaitForPlayersAndStartGame()
    {
        if (waitingToStartGame || gameStarting || NetworkManager.Singleton == null)
        {
            return;
        }

        waitingToStartGame = true;
        int generation = sessionGeneration;

        try
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (!shuttingDown &&
                   Application.isPlaying &&
                   !gameStarting &&
                   generation == sessionGeneration &&
                   DateTime.UtcNow < deadline)
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening &&
                    manager.ConnectedClientsIds.Count >= minimumPlayersToStart)
                {
                    break;
                }

                await Task.Delay(100);
            }

            if (shuttingDown || !Application.isPlaying || gameStarting || generation != sessionGeneration)
            {
                return;
            }

            NetworkManager connectedManager = NetworkManager.Singleton;
            if (connectedManager == null || !connectedManager.IsListening ||
                connectedManager.ConnectedClientsIds.Count < minimumPlayersToStart)
            {
                Debug.LogWarning("Timed out waiting for all players to connect to Netcode.");
                SetStatus("Could not connect both players. Try joining again.");
                return;
            }

            StartGame();
        }
        finally
        {
            if (generation == sessionGeneration)
            {
                waitingToStartGame = false;
            }
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
        if (shuttingDown || !Application.isPlaying || joinInProgress)
        {
            return;
        }

        joinInProgress = true;

        try
        {
            await JoinGameAsync();
        }
        finally
        {
            joinInProgress = false;
        }
    }

    private async Task JoinGameAsync()
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

            if (shuttingDown || !Application.isPlaying)
            {
                return;
            }

            Debug.Log("=================================");
            Debug.Log("JOINED SESSION");
            Debug.Log("Session ID: " + currentSession.Id);
            Debug.Log("Players: " + currentSession.PlayerCount);
            Debug.Log("=================================");

            SetStatus("Connected. Loading game...");

            // The client does NOT load the GameScene itself.
            // The host controls the network scene.
        }
        catch (Exception e)
        {
            if (shuttingDown || !Application.isPlaying)
            {
                return;
            }

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

        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("Cannot load the game scene because Netcode is not connected yet.");
            SetStatus("Network is still connecting...");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError(
                "Cannot load game scene '" + gameSceneName +
                "'. Add the scene to Build Settings and check its exact name."
            );
            SetStatus("Game scene is not in Build Settings");
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

        var sceneLoadStatus = NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );

        if (sceneLoadStatus != SceneEventProgressStatus.Started)
        {
            gameStarting = false;
            Debug.LogError("Network scene load failed: " + sceneLoadStatus);
            SetStatus("Could not start game scene");
        }
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
