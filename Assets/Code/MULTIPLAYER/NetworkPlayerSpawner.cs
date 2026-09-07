using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject player1Prefab;
    [SerializeField] private NetworkObject player2Prefab;

    [Header("Spawn Positions")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;
    [SerializeField] private Vector3 player2Offset = new Vector3(1.5f, 0f, 0f);

    private readonly Dictionary<ulong, NetworkObject> playersByClient = new Dictionary<ulong, NetworkObject>();

    private void Update()
    {
        UpdateLocalPlayerCameras();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        RegisterPlayerPrefab(player1Prefab);
        RegisterPlayerPrefab(player2Prefab);

        NetworkManager.OnClientConnectedCallback += SpawnPlayerForClient;
        NetworkManager.OnClientDisconnectCallback += RemovePlayerForClient;

        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager == null)
        {
            return;
        }

        NetworkManager.OnClientConnectedCallback -= SpawnPlayerForClient;
        NetworkManager.OnClientDisconnectCallback -= RemovePlayerForClient;
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer || playersByClient.ContainsKey(clientId))
        {
            return;
        }

        if (playersByClient.Count >= 2)
        {
            Debug.LogWarning("A third client tried to spawn, but this game supports only two players.");
            return;
        }

        NetworkObject prefab = playersByClient.Count == 0 ? player1Prefab : player2Prefab;
        if (prefab == null)
        {
            Debug.LogError("NetworkPlayerSpawner is missing a NetworkObject player prefab.", this);
            return;
        }

        Vector3 spawnPosition = GetPlayer1SpawnPosition();
        if (playersByClient.Count == 1)
        {
            spawnPosition = GetPlayer2SpawnPosition(spawnPosition);
        }

        NetworkObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
        playersByClient.Add(clientId, player);
    }

    private void RegisterPlayerPrefab(NetworkObject prefab)
    {
        if (prefab == null || NetworkManager.NetworkConfig.Prefabs.Contains(prefab.gameObject))
        {
            return;
        }

        NetworkManager.AddNetworkPrefab(prefab.gameObject);
    }

    private static void UpdateLocalPlayerCameras()
    {
        NetworkObject[] players = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (NetworkObject player in players)
        {
            if (!player.IsSpawned || player.GetComponent<PlayerController>() == null)
            {
                continue;
            }

            bool isLocalPlayer = player.IsOwner;
            foreach (Camera playerCamera in player.GetComponentsInChildren<Camera>(true))
            {
                playerCamera.enabled = isLocalPlayer;
            }

            foreach (AudioListener audioListener in player.GetComponentsInChildren<AudioListener>(true))
            {
                audioListener.enabled = isLocalPlayer;
            }
        }
    }

    private void RemovePlayerForClient(ulong clientId)
    {
        playersByClient.Remove(clientId);
    }

    private Vector3 GetPlayer1SpawnPosition()
    {
        return player1SpawnPoint != null ? player1SpawnPoint.position : transform.position;
    }

    private Vector3 GetPlayer2SpawnPosition(Vector3 player1Position)
    {
        return player2SpawnPoint != null ? player2SpawnPoint.position : player1Position + player2Offset;
    }
}
