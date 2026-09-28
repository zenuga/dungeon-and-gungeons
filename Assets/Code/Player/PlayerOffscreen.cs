using System.Collections;
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

    // [NEW] Interval between target searches in seconds
    [Header("Target Search Settings")]
    [SerializeField] private float searchInterval = 5f;

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    // [NEW] Start coroutine to run periodic target checks
    private void Start()
    {
        StartCoroutine(SearchTargetRoutine());
    }

    // [NEW] Coroutine that searches for the opponent player every 5 seconds
    private IEnumerator SearchTargetRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(searchInterval);

        while (true)
        {
            UpdateTargetByTag();
            yield return wait;
        }
    }

    // [NEW] Method to swap targets based on this object's tag
    private void UpdateTargetByTag()
    {
        if (gameObject.CompareTag("Player1"))
        {
            GameObject p2 = GameObject.FindWithTag("Player2");
            if (p2 != null)
            {
                targetPlayer = p2.transform;
            }
        }
        else if (gameObject.CompareTag("Player2"))
        {
            GameObject p1 = GameObject.FindWithTag("Player1");
            if (p1 != null)
            {
                targetPlayer = p1.transform;
            }
        }
    }

    private void Update()
    {
        if (targetPlayer == null || indicatorUI == null || indicatorImage == null)
        {
            return;
        }

        // Convert world position of target player to viewport position (0 to 1 range)
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetPlayer.position);

        // Check if the target player is outside the camera's view frustum
        bool isOffScreen = viewportPos.x < 0f || viewportPos.x > 1f ||
                           viewportPos.y < 0f || viewportPos.y > 1f ||
                           viewportPos.z < 0f;

        if (isOffScreen)
        {
            if (!indicatorImage.enabled)
            {
                indicatorImage.enabled = true;
            }

            // Invert vector if the target is behind the camera plane
            if (viewportPos.z < 0f)
            {
                viewportPos.x = 1f - viewportPos.x;
                viewportPos.y = 1f - viewportPos.y;
            }

            // Convert viewport to pixel coordinates relative to screen center
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Vector3 screenPos = mainCamera.ViewportToScreenPoint(viewportPos);
            Vector3 directionFromCenter = screenPos - screenCenter;

            // Calculate angle toward the target
            float angleRad = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x);

            // Compute maximum screen extents minus padding
            float boundsX = (Screen.width * 0.5f) - edgePadding;
            float boundsY = (Screen.height * 0.5f) - edgePadding;

            // Calculate intersection point along screen perimeter box
            float slope = Mathf.Tan(angleRad);
            Vector3 clampedPosition;

            if (Mathf.Abs(boundsX * slope) <= boundsY)
            {
                // Hits left or right screen border
                float sign = Mathf.Sign(directionFromCenter.x);
                clampedPosition = new Vector3(sign * boundsX, sign * boundsX * slope, 0f);
            }
            else
            {
                // Hits top or bottom screen border
                float sign = Mathf.Sign(directionFromCenter.y);
                clampedPosition = new Vector3(sign * boundsY / slope, sign * boundsY, 0f);
            }

            // Apply calculated screen position and rotation
            indicatorUI.position = screenCenter + clampedPosition;
            indicatorUI.rotation = Quaternion.Euler(0f, 0f, angleRad * Mathf.Rad2Deg);
        }
        else
        {
            if (indicatorImage.enabled)
            {
                indicatorImage.enabled = false;
            }
        }
    }
}