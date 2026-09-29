using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    public static NetworkPlayerSpawner Instance { get; private set; }

    private const string GameplaySceneName = "SampleScene";

    [Header("Player Prefabs")]
    [SerializeField] private NetworkObject player1Prefab;
    [SerializeField] private NetworkObject player2Prefab;

    [Header("Spawn Positions")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;
    [SerializeField] private Vector3 player2Offset = new Vector3(1.5f, 0f, 0f);

    private readonly Dictionary<ulong, NetworkObject> playersByClient = new Dictionary<ulong, NetworkObject>();

    private void Start()
    {
        // The start-screen Play button loads SampleScene without starting NGO.
        // Spawn one local player for that offline path; network sessions spawn
        // their owned player objects from OnNetworkSpawn/OnClientConnected.
        if (NetworkSpawnUtility.IsNetworkSessionActive || player1Prefab == null ||
            FindFirstObjectByType<PlayerController>() != null)
        {
            return;
        }

        Instantiate(player1Prefab, GetPlayer1SpawnPosition(), Quaternion.identity);
    }

    public List<GameObject> GetSpawnedPlayerObjects()
    {
        List<GameObject> players = new List<GameObject>();

        foreach (NetworkObject player in playersByClient.Values)
        {
            if (player != null && player.gameObject != null && !players.Contains(player.gameObject))
            {
                players.Add(player.gameObject);
            }
        }

        if (players.Count == 0 && NetworkManager != null && NetworkManager.SpawnManager != null)
        {
            foreach (NetworkObject networkObject in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (networkObject != null && networkObject.GetComponent<PlayerController>() != null && !players.Contains(networkObject.gameObject))
                {
                    players.Add(networkObject.gameObject);
                }
            }
        }

        if (players.Count == 0 && !NetworkSpawnUtility.IsNetworkSessionActive)
        {
            foreach (PlayerController player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (player != null && !players.Contains(player.gameObject))
                {
                    players.Add(player.gameObject);
                }
            }
        }

        return players;
    }

    private void Update()
    {
        UpdateLocalPlayerCameras();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (!IsServer)
        {
            return;
        }

        NetworkManager.SceneManager.OnLoadComplete += HandleNetworkSceneLoadComplete;
        NetworkManager.SceneManager.OnLoadEventCompleted += HandleNetworkSceneLoadCompleted;
        RegisterPlayerPrefab(player1Prefab);
        RegisterPlayerPrefab(player2Prefab);

        NetworkManager.OnClientDisconnectCallback += RemovePlayerForClient;

        // The server's scene object spawns when its own scene is ready. Remote
        // player objects wait for that client's OnLoadComplete callback below.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameplaySceneName)
        {
            SpawnPlayerForClient(NetworkManager.ServerClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (NetworkManager == null)
        {
            return;
        }

        NetworkManager.OnClientDisconnectCallback -= RemovePlayerForClient;
        if (NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadComplete -= HandleNetworkSceneLoadComplete;
            NetworkManager.SceneManager.OnLoadEventCompleted -= HandleNetworkSceneLoadCompleted;
        }
    }

    private void HandleNetworkSceneLoadComplete(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if (!IsServer || sceneName != GameplaySceneName)
        {
            return;
        }

        Debug.Log($"[NetworkPlayerSpawner] Client {clientId} finished loading {sceneName}; assigning its player prefab.");
        SpawnPlayerForClient(clientId);
    }

    private void HandleNetworkSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != GameplaySceneName)
        {
            return;
        }

        // Scene-placed NetworkBehaviours can spawn before every client has
        // completed the network scene event. Reconcile after the event too.
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        if (!IsServer || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            return;
        }

        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            return;
        }

        if (playersByClient.TryGetValue(clientId, out NetworkObject trackedPlayer))
        {
            if (trackedPlayer != null && trackedPlayer.IsSpawned)
            {
                return;
            }

            playersByClient.Remove(clientId);
        }

        if (client.PlayerObject != null)
        {
            playersByClient[clientId] = client.PlayerObject;
            return;
        }

        if (playersByClient.Count >= 2)
        {
            Debug.LogWarning("A third client tried to spawn, but this game supports only two players.");
            return;
        }

        bool isHostPlayer = clientId == NetworkManager.ServerClientId;
        NetworkObject prefab = isHostPlayer ? player1Prefab : player2Prefab;
        if (prefab == null)
        {
            Debug.LogError("NetworkPlayerSpawner is missing a NetworkObject player prefab.", this);
            return;
        }

        Vector3 player1Position = GetPlayer1SpawnPosition();
        Vector3 spawnPosition = isHostPlayer
            ? player1Position
            : GetPlayer2SpawnPosition(player1Position);

        NetworkObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
        playersByClient.Add(clientId, player);
        Debug.Log($"[NetworkPlayerSpawner] Spawned {prefab.name} for client {clientId} at {spawnPosition}.");
        StartCoroutine(ApplySpawnPositionAfterSpawn(player, spawnPosition));
    }

    private IEnumerator ApplySpawnPositionAfterSpawn(NetworkObject player, Vector3 spawnPosition)
    {
        // Let NGO deliver the player object first, then set the owner's transform.
        yield return null;

        if (player == null || !player.IsSpawned)
        {
            yield break;
        }

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.RequestNetworkTeleport(spawnPosition);
        }
        else if (IsServer)
        {
            player.transform.position = spawnPosition;
        }
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

            PlayerController controller = player.GetComponent<PlayerController>();
            controller.SetLocalPresentation(player.IsOwner);
        }
    }

    private void RemovePlayerForClient(ulong clientId)
    {
        playersByClient.Remove(clientId);
    }

    public void RequestWorldDamage(Vector3 targetPosition, int damage)
    {
        if (!IsSpawned || damage <= 0)
        {
            return;
        }

        if (IsServer)
        {
            ApplyWorldDamage(targetPosition, damage);
        }
        else
        {
            RequestWorldDamageServerRpc(targetPosition, damage);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestWorldDamageServerRpc(Vector3 targetPosition, int damage, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
            client.PlayerObject == null ||
            Vector3.Distance(client.PlayerObject.transform.position, targetPosition) > 120f)
        {
            return;
        }

        ApplyWorldDamage(targetPosition, Mathf.Clamp(damage, 1, 500));
    }

    private void ApplyWorldDamage(Vector3 targetPosition, int damage)
    {
        Collider[] colliders = Physics.OverlapSphere(targetPosition, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        WallHealth wall = null;
        Crate crate = null;
        float closestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            WallHealth candidateWall = collider.GetComponentInParent<WallHealth>();
            Crate candidateCrate = collider.GetComponentInParent<Crate>();
            if (candidateWall == null && candidateCrate == null)
            {
                continue;
            }

            float distance = Vector3.Distance(targetPosition, collider.ClosestPoint(targetPosition));
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            wall = candidateWall;
            crate = candidateWall == null ? candidateCrate : null;
        }

        if (wall != null)
        {
            wall.ApplyWorldDamage(damage);
        }
        else if (crate != null)
        {
            crate.ApplyWorldDamage(damage);
        }
        else
        {
            return;
        }

        BroadcastWorldDamage(targetPosition, damage);
    }

    public void BroadcastWorldDamage(Vector3 targetPosition, int damage)
    {
        if (!IsSpawned || !IsServer || damage <= 0)
        {
            return;
        }

        List<ulong> recipients = new List<ulong>();
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                recipients.Add(clientId);
            }
        }

        if (recipients.Count > 0)
        {
            ApplyWorldDamageClientRpc(targetPosition, damage, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = recipients.ToArray() }
            });
        }
    }

    public void BroadcastDungeonCompletion(Vector3 dungeonPosition)
    {
        if (!IsSpawned || !IsServer)
        {
            return;
        }

        List<ulong> recipients = new List<ulong>();
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                recipients.Add(clientId);
            }
        }

        if (recipients.Count > 0)
        {
            ApplyDungeonCompletionClientRpc(dungeonPosition, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = recipients.ToArray() }
            });
        }
    }

    [ClientRpc]
    private void ApplyDungeonCompletionClientRpc(Vector3 dungeonPosition, ClientRpcParams rpcParams = default)
    {
        DungeonWaveManager[] dungeons = FindObjectsByType<DungeonWaveManager>(FindObjectsSortMode.None);
        DungeonWaveManager closestDungeon = null;
        float closestDistance = 2f;

        foreach (DungeonWaveManager dungeon in dungeons)
        {
            float distance = Vector3.Distance(dungeon.transform.position, dungeonPosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestDungeon = dungeon;
            }
        }

        closestDungeon?.ApplyNetworkCompletion();
    }

    [ClientRpc]
    private void ApplyWorldDamageClientRpc(Vector3 targetPosition, int damage, ClientRpcParams rpcParams = default)
    {
        Collider[] colliders = Physics.OverlapSphere(targetPosition, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        WallHealth wall = null;
        Crate crate = null;
        float closestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            WallHealth candidateWall = collider.GetComponentInParent<WallHealth>();
            Crate candidateCrate = collider.GetComponentInParent<Crate>();
            if (candidateWall == null && candidateCrate == null)
            {
                continue;
            }

            float distance = Vector3.Distance(targetPosition, collider.ClosestPoint(targetPosition));
            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            wall = candidateWall;
            crate = candidateWall == null ? candidateCrate : null;
        }

        if (wall != null)
        {
            wall.ApplyWorldDamage(damage);
        }
        else if (crate != null)
        {
            crate.ApplyWorldDamage(damage);
        }
    }

    private Vector3 GetPlayer1SpawnPosition()
    {
        if (player1SpawnPoint != null)
        {
            return player1SpawnPoint.position;
        }

        ChunkedMineGeneration mineGeneration = FindFirstObjectByType<ChunkedMineGeneration>();
        if (mineGeneration != null && mineGeneration.player1SpawnPoint != null)
        {
            return mineGeneration.player1SpawnPoint.position;
        }

        return transform.position;
    }

    private Vector3 GetPlayer2SpawnPosition(Vector3 player1Position)
    {
        if (player2SpawnPoint != null)
        {
            return player2SpawnPoint.position;
        }

        ChunkedMineGeneration mineGeneration = FindFirstObjectByType<ChunkedMineGeneration>();
        if (mineGeneration != null && mineGeneration.player2SpawnPoint != null)
        {
            return mineGeneration.player2SpawnPoint.position;
        }

        return player1Position + player2Offset;
    }
}
