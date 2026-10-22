using UnityEngine;
using UnityEngine.UI;

public class OffScreenIndicator : MonoBehaviour
{
    [Header("Target & Camera Settings")]
    [SerializeField] private Transform targetPlayer;
    [SerializeField] private Camera mainCamera;

    [Header("UI Component Settings")]
    [SerializeField] private RectTransform indicatorUI;
    [SerializeField] private Image indicatorImage;

    [Header("Edge Padding")]
    [Tooltip("Distance in pixels from the edge of the screen")]
    [SerializeField] private float edgePadding = 40f;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        SetIndicatorVisible(false);
    }

    private void Update()
    {
        // Players can spawn/despawn during a run, so never retain a stale target.
        targetPlayer = FindOtherPlayer();
        if (targetPlayer == null || indicatorUI == null || indicatorImage == null || mainCamera == null)
        {
            SetIndicatorVisible(false);
            return;
        }

        Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetPlayer.position);
        bool isOffScreen = viewportPos.x < 0f || viewportPos.x > 1f ||
                           viewportPos.y < 0f || viewportPos.y > 1f || viewportPos.z < 0f;
        SetIndicatorVisible(isOffScreen);
        if (!isOffScreen) return;

        if (viewportPos.z < 0f)
        {
            viewportPos.x = 1f - viewportPos.x;
            viewportPos.y = 1f - viewportPos.y;
        }

        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Vector3 screenPos = mainCamera.ViewportToScreenPoint(viewportPos);
        Vector3 directionFromCenter = screenPos - screenCenter;
        float angleRad = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x);
        float boundsX = (Screen.width * 0.5f) - edgePadding;
        float boundsY = (Screen.height * 0.5f) - edgePadding;
        float slope = Mathf.Tan(angleRad);
        Vector3 clampedPosition;

        if (Mathf.Abs(boundsX * slope) <= boundsY)
        {
            float sign = Mathf.Sign(directionFromCenter.x);
            clampedPosition = new Vector3(sign * boundsX, sign * boundsX * slope, 0f);
        }
        else
        {
            float sign = Mathf.Sign(directionFromCenter.y);
            clampedPosition = new Vector3(sign * boundsY / slope, sign * boundsY, 0f);
        }

        indicatorUI.position = screenCenter + clampedPosition;
        indicatorUI.rotation = Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg);
    }

    private Transform FindOtherPlayer()
    {
        string otherPlayerTag;
        if (CompareTag("Player1")) otherPlayerTag = "Player2";
        else if (CompareTag("Player2")) otherPlayerTag = "Player1";
        else return null;

        GameObject otherPlayer = GameObject.FindWithTag(otherPlayerTag);
        return otherPlayer != null ? otherPlayer.transform : null;
    }

    private void SetIndicatorVisible(bool visible)
    {
        if (indicatorImage == null) return;
        if (indicatorImage.gameObject.activeSelf != visible)
            indicatorImage.gameObject.SetActive(visible);
        if (indicatorImage.enabled != visible)
            indicatorImage.enabled = visible;
    }
}
