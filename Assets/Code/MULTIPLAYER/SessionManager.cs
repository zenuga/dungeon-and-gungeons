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
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;

    private ISession currentSession;
    private bool servicesInitialized = false;

    private async void Awake()
    {
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
    // CREATE GAME
    // =========================================================

    public async void CreateGame()
    {
        Debug.Log("=================================");
        Debug.Log("CREATE GAME");
        Debug.Log("=================================");

        // Check Unity Services
        if (!servicesInitialized)
        {
            Debug.LogError("Unity Services are not initialized yet!");
            SetStatus("Still connecting...");
            return;
        }

        // Check NetworkManager
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError(
                "NETWORK MANAGER IS NULL!\n" +
                "Make sure a NetworkManager exists and is active in the current scene."
            );

            SetStatus("NetworkManager missing");
            return;
        }

        Debug.Log("NetworkManager found: " + NetworkManager.Singleton.gameObject.name);

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
            Debug.Log("=================================");

            if (joinCodeText != null)
            {
                joinCodeText.text = currentSession.Code;
            }

            SetStatus("Game created!");

            // Host blijft voorlopig in de menu scene.
            // We gaan pas naar GameScene wanneer we zeker weten
            // dat de multiplayer setup werkt.
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
    // JOIN GAME
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
            Debug.Log("=================================");

            SetStatus("Joined game!");

            LoadGameScene();
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
    // LOAD GAME SCENE
    // =========================================================

    private void LoadGameScene()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("Game Scene name is empty!");
            return;
        }

        Debug.Log("Loading game scene: " + gameSceneName);

        SceneManager.LoadScene(gameSceneName);
    }

    // =========================================================
    // STATUS
    // =========================================================

    private void SetStatus(string message)
    {
        Debug.Log("[SessionManager] " + message);

        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}