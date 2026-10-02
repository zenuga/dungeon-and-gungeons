using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(1000)]
public class PlayerMouseAim : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private GameObject rotationOnlyObject;
    private float lastReportedYaw = float.NaN;
    private float desiredYaw;
    private bool hasDesiredYaw;

    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    private void Update()
    {
        PlayerController playerController = GetComponentInParent<PlayerController>();
        Camera aimCamera = playerController != null ? playerController.PlayerCamera : Camera.main;
        if (!NetworkOwnership.CanControl(this) || aimCamera == null || Mouse.current == null)
        {
            return;
        }

        Ray mouseRay = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane movementPlane = new Plane(Vector3.up, transform.position);

        if (!movementPlane.Raycast(mouseRay, out float distance))
        {
            return;
        }

        Vector3 mouseWorldPosition = mouseRay.GetPoint(distance);
        Vector3 aimDirection = mouseWorldPosition - transform.position;
        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        AimDirection = aimDirection.normalized;

        float targetYaw = Quaternion.LookRotation(aimDirection.normalized, Vector3.up).eulerAngles.y;
        desiredYaw = targetYaw;
        hasDesiredYaw = true;
        if (playerController != null &&
            (float.IsNaN(lastReportedYaw) || Mathf.Abs(Mathf.DeltaAngle(lastReportedYaw, targetYaw)) >= 1f))
        {
            lastReportedYaw = targetYaw;
            playerController.ReportAimYaw(targetYaw);
        }

    }

    private void LateUpdate()
    {
        if (!hasDesiredYaw) return;

        Quaternion aimTarget = Quaternion.Euler(-90f, desiredYaw, 0f);
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            aimTarget,
            rotationSpeed * Time.deltaTime);

        if (rotationOnlyObject != null && rotationOnlyObject.transform != transform)
        {
            Quaternion rotationOnlyTarget = Quaternion.Euler(0f, desiredYaw, 0f);
            rotationOnlyObject.transform.localRotation = Quaternion.Slerp(
                rotationOnlyObject.transform.localRotation,
                rotationOnlyTarget,
                rotationSpeed * Time.deltaTime);
        }
    }

    public void ApplyReplicatedAimYaw(float yaw)
    {
        float normalizedYaw = Mathf.Repeat(yaw, 360f);
        float radians = normalizedYaw * Mathf.Deg2Rad;
        AimDirection = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        desiredYaw = normalizedYaw;
        hasDesiredYaw = true;
    }
}
