using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerReviveController : MonoBehaviour
{
    [Header("Revive Trigger")]
    [SerializeField] private float reviveTriggerRadius = 1.5f;

    [Header("Revive UI")]
    [SerializeField] private GameObject reviveImage;
    [SerializeField] private RectTransform movingImage;
    [SerializeField] private RectTransform targetImage;
    [SerializeField] private float slideDistance = 180f;
    [SerializeField] private float slideSpeed = 240f;
    [SerializeField] private int successfulPressesRequired = 10;

    private PlayerHealth playerHealth;
    private PlayerHealth reviveTarget;
    private PlayerController playerController;
    private PlayerPickupManager pickupManager;
    private Vector3 reviveStartPosition;
    private Coroutine reviveRoutine;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerController = GetComponent<PlayerController>();
        pickupManager = GetComponentInChildren<PlayerPickupManager>(true);
        CreateReviveTrigger();
        SetReviveUi(false);
    }

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this) || playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (reviveRoutine == null && reviveTarget != null && ReviveKeyPressed())
        {
            reviveRoutine = StartCoroutine(ReviveChallenge());
        }
    }

    public void SetReviveTarget(PlayerHealth target)
    {
        if (target == null || target == playerHealth) return;
        if (target.IsAlive)
        {
            ClearReviveTarget(target);
            return;
        }

        reviveTarget = target;
    }

    public void ClearReviveTarget(PlayerHealth target)
    {
        if (reviveTarget == target) reviveTarget = null;
    }

    private IEnumerator ReviveChallenge()
    {
        reviveStartPosition = transform.position;
        SetReviveUi(true);
        if (pickupManager != null)
        {
            pickupManager.enabled = false;
        }
        int successfulPresses = 0;
        float slidePosition = -slideDistance;
        float direction = 1f;
        float targetPosition = targetImage != null ? targetImage.anchoredPosition.x : -slideDistance;

        while (successfulPresses < successfulPressesRequired)
        {
            if (playerHealth == null || !playerHealth.IsAlive || reviveTarget == null || reviveTarget.IsAlive ||
                Vector3.Distance(transform.position, reviveStartPosition) > 0.05f)
            {
                CancelRevive();
                yield break;
            }

            slidePosition += direction * slideSpeed * Time.deltaTime;
            if (slidePosition >= slideDistance)
            {
                slidePosition = slideDistance;
                direction = -1f;
            }
            else if (slidePosition <= -slideDistance)
            {
                slidePosition = -slideDistance;
                direction = 1f;
            }

            if (movingImage != null)
            {
                Vector2 anchoredPosition = movingImage.anchoredPosition;
                anchoredPosition.x = slidePosition;
                movingImage.anchoredPosition = anchoredPosition;
            }

            if (ReviveKeyPressed() &&
                Mathf.Abs(slidePosition - targetPosition) <= 30f)
            {
                successfulPresses++;
            }

            yield return null;
        }

        reviveTarget.HealPercentOfMax(0.4f);
        CancelRevive();
    }

    private void CancelRevive()
    {
        if (reviveRoutine != null)
        {
            StopCoroutine(reviveRoutine);
            reviveRoutine = null;
        }

        SetReviveUi(false);
        if (pickupManager != null && playerHealth != null && playerHealth.IsAlive)
        {
            pickupManager.enabled = true;
        }
    }

    private bool ReviveKeyPressed()
    {
        if (Keyboard.current == null) return false;
        bool playerTwoLocalControls = playerController != null &&
            playerController.Type == PlayerController.PlayerType.Player2 &&
            !NetworkSpawnUtility.IsNetworkSessionActive;
        return playerTwoLocalControls
            ? Keyboard.current.semicolonKey.wasPressedThisFrame
            : Keyboard.current.fKey.wasPressedThisFrame;
    }

    private void SetReviveUi(bool visible)
    {
        if (reviveImage != null)
        {
            reviveImage.SetActive(visible);
        }
    }

    private void CreateReviveTrigger()
    {
        GameObject triggerObject = new GameObject("ReviveTrigger");
        triggerObject.transform.SetParent(transform, false);

        SphereCollider sphereCollider = triggerObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = reviveTriggerRadius;

        Rigidbody rigidbody = triggerObject.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        ReviveTriggerDetector detector = triggerObject.AddComponent<ReviveTriggerDetector>();
        detector.reviveController = this;
    }
}
