using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerType
    {
        Player1, // Uses WASD
        Player2  // Uses IJKL
    }

    [Header("Player Setup")]
    [SerializeField] private PlayerType playerType = PlayerType.Player1;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private GameObject visualModel; // GameObject that rotates (defaults to this.gameObject if left empty)

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float rotationSpeed = 15.0f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController _characterController;
    private Vector3 _velocity;
    private float speedMultiplier = 1f;

    public Vector3 FacingDirection => visualModel != null ? visualModel.transform.forward : transform.forward;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        // Default visual object to this transform if unassigned
        if (visualModel == null)
        {
            visualModel = gameObject;
        }

        // Set layer so physics/collisions use the "Player" layer settings
        int layerIndex = LayerMask.NameToLayer(playerLayerName);
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;

            // FIX: Force Unity's physics system to ignore collisions between objects on the Player layer.
            // CharacterController requires this explicit call to obey layer ignores.
            Physics.IgnoreLayerCollision(layerIndex, layerIndex, true);
        }
        else
        {
            Debug.LogWarning($"Layer '{playerLayerName}' does not exist. Please create it in the Unity Inspector.");
        }

        // Automatically assign the correct tag based on the player type
        if (playerType == PlayerType.Player1)
        {
            gameObject.tag = "Player1";
        }
        else if (playerType == PlayerType.Player2)
        {
            gameObject.tag = "Player2";
        }
    }

    private void Start()
    {
        // FIX (Alternative Backup): Find all other PlayerControllers and ignore their specific CharacterControllers
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (PlayerController player in allPlayers)
        {
            if (player != this && player._characterController != null)
            {
                Physics.IgnoreCollision(_characterController, player._characterController, true);
            }
        }
    }

    private void Update()
    {
        if (!NetworkOwnership.CanControl(this))
        {
            return;
        }

        // Keep player grounded properly
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        // Get inputs based on assigned player type
        Vector2 inputVector = GetInput();

        // If opposing keys are pressed, inputVector cancels out to zero
        if (inputVector.sqrMagnitude > 0.001f)
        {
            // Normalize input vector so moving diagonally isn't faster
            inputVector = inputVector.normalized;

            // Apply movement on X and Z axes (3D space)
            Vector3 moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);
            _characterController.Move(moveDirection * moveSpeed * speedMultiplier * Time.deltaTime);

            // Handle direction rotation (W/I = North, S/K = South, A/J = West, D/L = East)
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            visualModel.transform.rotation = Quaternion.Slerp(
                visualModel.transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }

        // Apply continuous gravity (No Jump functionality)
        _velocity.y += gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    private Vector2 GetInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (playerType == PlayerType.Player1)
        {
            // WASD Controls
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
        }
        else if (playerType == PlayerType.Player2)
        {
            // IJKL Controls
            if (Keyboard.current.iKey.isPressed) input.y += 1f;
            if (Keyboard.current.kKey.isPressed) input.y -= 1f;
            if (Keyboard.current.jKey.isPressed) input.x -= 1f;
            if (Keyboard.current.lKey.isPressed) input.x += 1f;
        }

        return input;
    }
}