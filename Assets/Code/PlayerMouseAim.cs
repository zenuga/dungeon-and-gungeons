using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMouseAim : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private float modelBaseRotation = -90f;

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this) || Camera.main == null || Mouse.current == null)
        {
            return;
        }

        PlayerController playerController = GetComponent<PlayerController>();
        Transform modelTransform = playerController != null
            ? playerController.VisualModelTransform
            : transform;

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

        Quaternion aimRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
        Quaternion targetRotation = Quaternion.Euler(
            modelBaseRotation,
            aimRotation.eulerAngles.y,
            0f);
        modelTransform.rotation = Quaternion.Slerp(
            modelTransform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }
}
