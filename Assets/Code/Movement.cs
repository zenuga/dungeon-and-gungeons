using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    public enum PlayerType
    {
        Player1, // Uses WASD
        Player2  // Uses IJKL
    }

    [Header("Player Setup")]
    [SerializeField] private PlayerType playerType = PlayerType.Player1;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private GameObject visualModel;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController _characterController;
    private Vector3 _velocity;
    private float speedMultiplier = 1f;

    public Vector3 FacingDirection => visualModel != null ? visualModel.transform.forward : transform.forward;
    public Transform VisualModelTransform => visualModel != null ? visualModel.transform : transform;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (GetComponent<NetworkObject>() != null && GetComponent<NetworkTransform>() == null)
        {
            gameObject.AddComponent<NetworkTransform>();
        }

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

    public override void OnNetworkSpawn()
    {
        SetLocalCamera(IsOwner);
    }

    private void SetLocalCamera(bool isLocalPlayer)
    {
        foreach (Camera playerCamera in GetComponentsInChildren<Camera>(true))
        {
            playerCamera.enabled = isLocalPlayer;

            if (isLocalPlayer)
            {
                playerCamera.tag = "MainCamera";
            }
            else if (playerCamera.CompareTag("MainCamera"))
            {
                playerCamera.tag = "Untagged";
            }
        }

        foreach (AudioListener audioListener in GetComponentsInChildren<AudioListener>(true))
        {
            audioListener.enabled = isLocalPlayer;
        }
    }

    private void Update()
    {
        if ((IsSpawned && !IsOwner) || !NetworkOwnership.CanControl(this))
        {
            return;
        }

        // Keep player grounded properly
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        // Both players use the same WASD movement on their own computer.
        Vector2 inputVector = GetInput();

        // If opposing keys are pressed, inputVector cancels out to zero
        if (inputVector.sqrMagnitude > 0.001f)
        {
            // Normalize input vector so moving diagonally isn't faster
            inputVector = inputVector.normalized;

            // Apply movement on X and Z axes (3D space)
            Vector3 moveDirection = new Vector3(inputVector.x, 0f, inputVector.y);
            _characterController.Move(moveDirection * moveSpeed * speedMultiplier * Time.deltaTime);

        }

        // Apply continuous gravity (No Jump functionality)
        _velocity.y += gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    private Vector2 GetInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;

        return input;
    }
}