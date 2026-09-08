using UnityEngine;

public class ReviveTriggerDetector : MonoBehaviour
{
    public PlayerReviveController reviveController;

    private void OnTriggerStay(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            reviveController.SetReviveTarget(playerHealth);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            reviveController.SetReviveTarget(null);
        }
    }
}