using UnityEngine;
using System.Collections.Generic;

public class ReviveTriggerDetector : MonoBehaviour
{
    public PlayerReviveController reviveController;
    private readonly Dictionary<Collider, PlayerHealth> nearbyPlayers = new Dictionary<Collider, PlayerHealth>();

    private void Awake()
    {
        if (reviveController == null) reviveController = GetComponentInParent<PlayerReviveController>();
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (reviveController == null || playerHealth == null || playerHealth == reviveController.GetComponent<PlayerHealth>())
        {
            return;
        }

        nearbyPlayers[other] = playerHealth;
        RefreshReviveTarget();
    }

    private void OnTriggerExit(Collider other)
    {
        nearbyPlayers.Remove(other);
        RefreshReviveTarget();
    }

    private void RefreshReviveTarget()
    {
        if (reviveController == null) return;

        PlayerHealth closestTarget = null;
        float closestDistance = float.MaxValue;
        List<Collider> staleColliders = null;
        foreach (KeyValuePair<Collider, PlayerHealth> entry in nearbyPlayers)
        {
            if (entry.Key == null || entry.Value == null || entry.Value.IsAlive)
            {
                if (staleColliders == null) staleColliders = new List<Collider>();
                staleColliders.Add(entry.Key);
                continue;
            }

            float distance = Vector3.Distance(transform.position, entry.Value.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = entry.Value;
            }
        }

        if (staleColliders != null)
            foreach (Collider collider in staleColliders) nearbyPlayers.Remove(collider);

        if (closestTarget != null) reviveController.SetReviveTarget(closestTarget);
        else reviveController.ClearReviveTarget(null);
    }
}
