using Unity.Netcode;
using UnityEngine;

public static class NetworkSpawnUtility
{
    public static bool IsNetworkSessionActive
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening;
        }
    }

    public static bool IsServerOrOffline
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager == null || !manager.IsListening || manager.IsServer;
        }
    }

    /// <summary>Synchronizes an already configured instance when Netcode is active.</summary>
    public static bool SpawnIfNetworked(GameObject instance)
    {
        if (instance == null)
        {
            return false;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
        {
            return true;
        }

        if (!manager.IsServer)
        {
            Object.Destroy(instance);
            return false;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError($"Cannot synchronize '{instance.name}': it has no NetworkObject.", instance);
            return false;
        }

        networkObject.Spawn();
        return true;
    }

    public static void DespawnOrDestroy(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        NetworkManager manager = NetworkManager.Singleton;
        if (networkObject != null && networkObject.IsSpawned && manager != null && manager.IsListening)
        {
            if (manager.IsServer)
            {
                networkObject.Despawn(true);
            }

            return;
        }

        Object.Destroy(instance);
    }
}
