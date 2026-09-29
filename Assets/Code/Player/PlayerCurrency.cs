using Unity.Netcode;
using UnityEngine;

public class PlayerCurrency : NetworkBehaviour
{
    [SerializeField] private int startingGold;
    private int offlineGold;
    private readonly NetworkVariable<int> gold = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Amount => IsSpawned ? gold.Value : offlineGold;

    private void Awake()
    {
        offlineGold = startingGold;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            gold.Value = startingGold;
        }
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!IsSpawned)
        {
            offlineGold += amount;
            return;
        }

        if (!IsServer)
        {
            return;
        }

        gold.Value += amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!IsSpawned)
        {
            if (offlineGold < amount)
            {
                return false;
            }

            offlineGold -= amount;
            return true;
        }

        if (gold.Value < amount)
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
