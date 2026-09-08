using Unity.Netcode;
using UnityEngine;

public class PlayerCurrency : NetworkBehaviour
{
    [SerializeField] private int startingGold;
    private readonly NetworkVariable<int> gold = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Amount => gold.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            gold.Value = startingGold;
        }
    }

    public void AddGold(int amount)
    {
        if (!IsServer || amount <= 0)
        {
            return;
        }

        gold.Value += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || gold.Value < amount)
        {
            return false;
        }

        if (!IsServer)
        {
            SpendServerRpc(amount);
            return true;
        }

        gold.Value -= amount;
        return true;
    }

    [ServerRpc]
    private void SpendServerRpc(int amount)
    {
        if (amount > 0 && gold.Value >= amount)
        {
            gold.Value -= amount;
        }
    }
}