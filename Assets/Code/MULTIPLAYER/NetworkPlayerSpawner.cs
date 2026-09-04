using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject player1Prefab;
    [SerializeField] private NetworkObject player2Prefab;

    [Header("Spawn Positions")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Vector3 player2Offset = new Vector3(1.5f, 0f, 0f);

    private readonly Dictionary<ulong, NetworkObject> playersByClient = new Dictionary<ulong, NetworkObject>();
    private bool hasRepositionedPlayers;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

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
            spawnPosition += player2Offset;
        }

        NetworkObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
        playersByClient.Add(clientId, player);

        if (playersByClient.Count == 2 && !hasRepositionedPlayers)
        {
            hasRepositionedPlayers = true;
            RepositionSecondPlayerPair();
        }
    }

    private void RepositionSecondPlayerPair()
    {
        NetworkObject firstPlayer = null;
        NetworkObject secondPlayer = null;
        int index = 0;

        foreach (NetworkObject player in playersByClient.Values)
        {
            if (index++ == 0)
            {
                firstPlayer = player;
            }
            else
            {
                secondPlayer = player;
            }
        }

        if (firstPlayer == null || secondPlayer == null)
        {
            return;
        }

        firstPlayer.transform.position = secondPlayer.transform.position - player2Offset;
        secondPlayer.transform.position = firstPlayer.transform.position + player2Offset;
        NetworkTransform firstTransform = firstPlayer.GetComponent<NetworkTransform>();
        NetworkTransform secondTransform = secondPlayer.GetComponent<NetworkTransform>();
        firstTransform?.Teleport(firstPlayer.transform.position, firstPlayer.transform.rotation, firstPlayer.transform.localScale);
        secondTransform?.Teleport(secondPlayer.transform.position, secondPlayer.transform.rotation, secondPlayer.transform.localScale);
    }

    private void RemovePlayerForClient(ulong clientId)
    {
        playersByClient.Remove(clientId);
        hasRepositionedPlayers = false;
    }

    private Vector3 GetPlayer1SpawnPosition()
    {
        return player1SpawnPoint != null ? player1SpawnPoint.position : transform.position;
    }
}
