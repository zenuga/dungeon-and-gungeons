using UnityEngine;

public class PickupTriggerDetector : MonoBehaviour
{
    public PlayerPickupManager pickupManager;

    private void OnTriggerEnter(Collider other)
    {
        if (pickupManager != null)
        {
            pickupManager.RegisterItem(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (pickupManager != null)
        {
            pickupManager.UnregisterItem(other);
        }
    }
}