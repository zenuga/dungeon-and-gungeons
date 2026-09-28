using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Helpers for deciding whether this instance may process input for an object.
/// Locally spawned objects remain controllable when Netcode is not running.
/// </summary>
public static class NetworkOwnership
{
    public static bool CanControl(Component component)
    {
        if (component == null)
        {
            return false;
        }

        NetworkObject networkObject = component.GetComponentInParent<NetworkObject>();

        // Preserve single-player behavior for objects that are not networked.
        if (networkObject == null || !networkObject.IsSpawned)
        {
            return true;
        }

        return networkObject.IsOwner;
    }
}
