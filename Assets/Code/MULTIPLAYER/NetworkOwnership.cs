using Unity.Netcode;
using UnityEngine;

public static class NetworkOwnership
{
    public static bool CanControl(Component component)
    {
        NetworkObject networkObject = component.GetComponentInParent<NetworkObject>();
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }
}
