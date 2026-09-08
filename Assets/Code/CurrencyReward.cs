using UnityEngine;

public static class CurrencyReward
{
    public static void GiveNearestPlayer(Vector3 position, int minimum, int maximum)
    {
        PlayerCurrency[] players = Object.FindObjectsByType<PlayerCurrency>(FindObjectsSortMode.None);
        PlayerCurrency closest = null;
        float closestDistance = Mathf.Infinity;

        foreach (PlayerCurrency player in players)
        {
            if (player == null || !player.IsServer || player.GetComponent<PlayerHealth>()?.IsAlive == false)
            {
                continue;
            }

            float distance = Vector3.Distance(position, player.transform.position);
            if (distance < closestDistance)
            {
                closest = player;
                closestDistance = distance;
            }
        }

        if (closest != null)
        {
            closest.AddGold(Random.Range(minimum, maximum + 1));
        }
    }
}