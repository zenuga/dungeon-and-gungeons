using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMouseAim : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private GameObject rotationOnlyObject;

    public Vector3 AimDirection { get; private set; } = Vector3.forward;

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this) || Camera.main == null || Mouse.current == null)
        {
            return;
        }

        Ray mouseRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
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

        Quaternion aimRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
        float targetYaw = aimRotation.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(-90f, targetYaw, 0f);
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);

        if (rotationOnlyObject != null && rotationOnlyObject.transform != transform)
        {
            Quaternion rotationOnlyTarget = Quaternion.Euler(0f, targetYaw, 0f);
            rotationOnlyObject.transform.localRotation = Quaternion.Slerp(
                rotationOnlyObject.transform.localRotation,
                rotationOnlyTarget,
                rotationSpeed * Time.deltaTime);
        }
    }
}
