using UnityEngine;
using Unity.Netcode;

public class WallHealth : NetworkBehaviour
{
    private Depth depth;
    public int Health = 10;

    private void Awake()
    {
        if (Health <= 0)
        {
            Health = 10;
        }
    }

    public void UpdateWallHealth()
    {
        if (depth != null)
        {
            Health = 10 * depth.depth;
        }
        else if (Health <= 0)
        {
            Health = 10;
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(amount);
            return;
        }

        ApplyDamage(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
        {
            if (IsSpawned && IsServer)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}