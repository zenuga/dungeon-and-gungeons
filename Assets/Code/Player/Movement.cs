using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    private const string Player1CameraTag = "MainCamera";
    private const string Player2CameraTag = "Player2Camera";

    public enum PlayerType
    {
        Player1, 
        Player2  
    }

    [Header("Player Setup")]
    [SerializeField] private PlayerType playerType = PlayerType.Player1;
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private GameObject visualModel;
    [SerializeField] private Camera playerCamera;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private GameObject settings;

    private CharacterController _characterController;
    private Vector3 _velocity;
    private float speedMultiplier = 1f;
    private GameObject playerUI;
    private Canvas[] playerCanvases;
    private int localPresentationState = -1;
    private bool loggedMissingLocalCamera;

    public Vector3 FacingDirection => visualModel != null ? visualModel.transform.forward : transform.forward;
    public Transform VisualModelTransform => visualModel != null ? visualModel.transform : transform;
    public GameObject PlayerHud => playerUI;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }

    /// <summary>
    /// Teleports this player on the owning client so owner-authoritative
    /// NetworkTransform can replicate the new position to the other players.
    /// </summary>
    public void RequestNetworkTeleport(Vector3 targetPosition)
    {
        if (!IsSpawned)
        {
            TeleportLocally(targetPosition);
            return;
        }

        if (IsServer)
        {
            if (IsOwner)
            {
                TeleportLocally(targetPosition);
            }
            else
            {
                SendTeleportToOwner(targetPosition);
            }
        }
        else if (IsOwner)
        {
            TeleportServerRpc(targetPosition);
        }
    }

    [ServerRpc]
    private void TeleportServerRpc(Vector3 targetPosition)
    {
        SendTeleportToOwner(targetPosition);
    }

    private void SendTeleportToOwner(Vector3 targetPosition)
    {
        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        TeleportOwnerClientRpc(targetPosition, rpcParams);
    }

    [ClientRpc]
    private void TeleportOwnerClientRpc(Vector3 targetPosition, ClientRpcParams rpcParams = default)
    {
        if (IsOwner)
        {
            TeleportLocally(targetPosition);
        }
    }

    private void TeleportLocally(Vector3 targetPosition)
    {
        _velocity = Vector3.zero;
        if (_characterController == null)
        {
            transform.position = targetPosition;
            return;
        }

        _characterController.enabled = false;
        transform.position = targetPosition;
        Physics.SyncTransforms();

        // Owner-authoritative NetworkTransform needs an explicit teleport so the
        // spawn position is sent immediately instead of being corrected later.
        if (IsSpawned && IsOwner)
        {
            NetworkTransform networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.Teleport(targetPosition, transform.rotation, transform.localScale);
            }
        }

        _characterController.enabled = true;
    }

    public void RequestPlayerProjectile(GameObject projectilePrefab, Vector3 position, Vector3 direction, int damage)
    {
        if (projectilePrefab == null || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (!NetworkSpawnUtility.IsNetworkSessionActive)
        {
            SpawnPlayerProjectile(projectilePrefab, position, direction, damage);
            return;
        }

        if (!IsOwner)
        {
            return;
        }

        NetworkObject prefabNetworkObject = projectilePrefab.GetComponent<NetworkObject>();
        if (prefabNetworkObject == null || prefabNetworkObject.PrefabIdHash == 0)
        {
            Debug.LogError($"Projectile prefab '{projectilePrefab.name}' needs a registered NetworkObject.", projectilePrefab);
            return;
        }

        if (IsServer)
        {
            SpawnPlayerProjectile(prefabNetworkObject.PrefabIdHash, position, direction, damage);
        }
        else
        {
            SpawnPlayerProjectileServerRpc(prefabNetworkObject.PrefabIdHash, position, direction, damage);
        }
    }

    [ServerRpc]
    private void SpawnPlayerProjectileServerRpc(uint prefabHash, Vector3 position, Vector3 direction, int damage)
    {
        SpawnPlayerProjectile(prefabHash, position, direction, damage);
    }

    private void SpawnPlayerProjectile(uint prefabHash, Vector3 position, Vector3 direction, int damage)
    {
        foreach (NetworkPrefab registeredPrefab in NetworkManager.NetworkConfig.Prefabs.Prefabs)
        {
            GameObject prefab = registeredPrefab.Prefab;
            NetworkObject prefabNetworkObject = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
            if (prefabNetworkObject == null || prefabNetworkObject.PrefabIdHash != prefabHash)
            {
                continue;
            }

            // Only accept registered projectile prefabs through this request.
            if (prefab.GetComponent<Projectile>() == null)
            {
                Debug.LogWarning($"Rejected projectile request for non-projectile prefab '{prefab.name}'.", this);
                return;
            }

            GameObject projectileObject = Instantiate(
                prefab,
                position,
                Quaternion.LookRotation(direction.normalized, Vector3.up));
            Projectile projectile = projectileObject.GetComponent<Projectile>();
            projectile.SetDirection(direction);
            projectile.SetOwnerTag(gameObject.tag);
            projectile.SetDamage(Mathf.Clamp(damage, 1, 500));
            NetworkSpawnUtility.SpawnIfNetworked(projectileObject);
            return;
        }

        Debug.LogWarning($"Projectile prefab hash {prefabHash} is not registered on the server.", this);
    }

    private void SpawnPlayerProjectile(GameObject prefab, Vector3 position, Vector3 direction, int damage)
    {
        GameObject projectileObject = Instantiate(
            prefab,
            position,
            Quaternion.LookRotation(direction.normalized, Vector3.up));
        Projectile projectile = projectileObject.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<Projectile>();
        }

        projectile.SetDirection(direction);
        projectile.SetOwnerTag(gameObject.tag);
        projectile.SetDamage(damage);
    }

    public void RequestCollectible(NetworkObject collectibleObject)
    {
        if (collectibleObject == null || !IsSpawned || !IsOwner || !collectibleObject.IsSpawned)
        {
            return;
        }

        NetworkObjectReference collectibleReference = collectibleObject;
        if (IsServer)
        {
            CollectCollectibleOnServer(collectibleReference);
        }
        else
        {
            CollectCollectibleServerRpc(collectibleReference);
        }
    }

    [ServerRpc]
    private void CollectCollectibleServerRpc(NetworkObjectReference collectibleReference)
    {
        CollectCollectibleOnServer(collectibleReference);
    }

    private void CollectCollectibleOnServer(NetworkObjectReference collectibleReference)
    {
        if (!collectibleReference.TryGet(out NetworkObject collectibleObject) ||
            collectibleObject == null ||
            !collectibleObject.IsSpawned ||
            Vector3.Distance(transform.position, collectibleObject.transform.position) > 4f ||
            (collectibleObject.GetComponent<CollectibleItem>() == null &&
             collectibleObject.GetComponentInChildren<CollectibleItem>(true) == null))
        {
            return;
        }

        collectibleObject.Despawn(true);
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

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        playerCanvases = GetComponentsInChildren<Canvas>(true);

        // Prefer the tagged HUD, then fall back to this player's screen-space canvas.
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("playerUI"))
            {
                playerUI = child.gameObject;
                break;
            }
        }

        if (playerUI == null)
        {
            foreach (Canvas playerCanvas in playerCanvases)
            {
                if (playerCanvas != null && playerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    playerUI = playerCanvas.gameObject;
                    break;
                }
            }
        }

        // CHANGED: Removed invalid .GetComponent<GameObject>() since FindGameObjectWithTag already returns a GameObject
        settings = GameObject.FindGameObjectWithTag("settings");

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

        SetLocalPresentation(!IsSpawned || IsOwner);
    }

    public override void OnNetworkSpawn()
    {
        SetLocalPresentation(IsOwner);
        if (IsOwner)
        {
            Debug.Log($"[PlayerController] Received owned player prefab '{name}' for local client {NetworkManager.LocalClientId} (type: {playerType}).", this);
        }
    }

    public override void OnGainedOwnership()
    {
        SetLocalPresentation(true);
    }

    public override void OnLostOwnership()
    {
        SetLocalPresentation(false);
    }

    private void LateUpdate()
    {
        if (IsSpawned)
        {
            // Reapply after scene activation/ownership changes so the local player
            // always has one enabled camera, including the joining client's Player 2.
            SetLocalPresentation(IsOwner);
        }
    }

    public void SetLocalPresentation(bool isLocalPlayer)
    {
        // The scene can activate this player after the prefab was first created.
        // Resolve the child camera again here so a stale/missing serialized field
        // cannot leave the owning client without a view after a scene load.
        if (playerCamera == null ||
            (playerCamera.transform != transform && !playerCamera.transform.IsChildOf(transform)))
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        string localCameraTag = playerType == PlayerType.Player2 ? Player2CameraTag : Player1CameraTag;
        foreach (Camera cameraInRig in cameras)
        {
            bool shouldRender = isLocalPlayer && cameraInRig == playerCamera;
            if (shouldRender && !cameraInRig.gameObject.activeSelf)
            {
                cameraInRig.gameObject.SetActive(true);
            }

            cameraInRig.enabled = shouldRender;
            if (shouldRender)
            {
                cameraInRig.targetDisplay = 0;
                cameraInRig.cullingMask = ~0;
                cameraInRig.tag = localCameraTag;
            }
            else if (cameraInRig.CompareTag(Player1CameraTag) || cameraInRig.CompareTag(Player2CameraTag))
            {
                cameraInRig.tag = "Untagged";
            }
        }

        if (isLocalPlayer && playerCamera == null && !loggedMissingLocalCamera)
        {
            loggedMissingLocalCamera = true;
            Debug.LogError($"Local player '{name}' has no child Camera after scene activation. Add a Camera to this player prefab.", this);
        }
        else if (playerCamera != null)
        {
            loggedMissingLocalCamera = false;
        }

        if (playerCamera != null && isLocalPlayer)
        {
            playerCamera.targetDisplay = 0;
            playerCamera.tag = localCameraTag;
        }

        int requestedState = isLocalPlayer ? 1 : 0;
        if (localPresentationState == requestedState)
        {
            return;
        }

        localPresentationState = requestedState;
        foreach (AudioListener audioListener in GetComponentsInChildren<AudioListener>(true))
        {
            audioListener.enabled = isLocalPlayer;
        }

        foreach (Canvas playerCanvas in playerCanvases)
        {
            if (playerCanvas != null && playerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                playerCanvas.enabled = isLocalPlayer;
                if (isLocalPlayer)
                {
                    playerCanvas.targetDisplay = 0;
                }

                GraphicRaycaster raycaster = playerCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    raycaster.enabled = isLocalPlayer;
                }
            }
        }

        if (playerUI != null)
        {
            playerUI.SetActive(isLocalPlayer);
        }

    }

    private void Update()
    {
        if ((IsSpawned && !IsOwner) || !NetworkOwnership.CanControl(this))
        {
            return;
        }

        // Escape key toggles settings and player UI
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) 
        {
            if (settings != null && settings.activeSelf)
            {
                settings.SetActive(false);
                if (playerUI != null) playerUI.SetActive(true);
            }
            else if (settings != null && !settings.activeSelf)
            {
                settings.SetActive(true);
                if (playerUI != null) playerUI.SetActive(false);
            }
        }

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
