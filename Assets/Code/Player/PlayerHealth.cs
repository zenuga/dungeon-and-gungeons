using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(PlayerCurrency))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Player Setup")]
    [SerializeField] private string playerTag = "Player1";
    [SerializeField] private Image healthFill;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool destroyOnZero = false;

    [SerializeField]
    private int currentHealth;
    private NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private Quaternion standingLocalRotation;
    private bool isKnockedDown;

    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => maxHealth;
    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        standingLocalRotation = transform.localRotation;
        currentHealth = maxHealth;
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    public override void OnNetworkSpawn()
    {
        networkHealth.OnValueChanged += OnHealthChanged;
        if (IsServer)
        {
            networkHealth.Value = maxHealth;
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = networkHealth.Value;
            UpdateHealthBar();
        }

        UpdatePlayerSystems();
    }

    public override void OnNetworkDespawn()
    {
        networkHealth.OnValueChanged -= OnHealthChanged;
    }

    private void Start()
    {
        if (healthFill == null)
        {
            healthFill = GetComponentInChildren<Image>();
        }

        if (healthBarRoot == null && healthFill != null)
        {
            healthBarRoot = healthFill.transform.parent != null ? healthFill.transform.parent : healthFill.transform;
        }

        if (string.IsNullOrEmpty(playerTag))
        {
            playerTag = gameObject.tag;
        }

        UpdateHealthBar();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (IsSpawned && !IsServer)
        {
            TakeDamageServerRpc(amount);
            return;
        }

        ApplyDamage(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (IsSpawned && IsServer)
        {
            networkHealth.Value = currentHealth;
        }
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (IsSpawned && !IsServer)
        {
            HealServerRpc(amount);
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (IsSpawned && IsServer)
        {
            networkHealth.Value = currentHealth;
        }
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    [ServerRpc(RequireOwnership = false)]
    private void HealServerRpc(int amount)
    {
        Heal(amount);
    }

    public void HealPercentOfMax(float percent)
    {
        Heal(Mathf.RoundToInt(maxHealth * Mathf.Max(0f, percent)));
    }

    public float GetHealthPercent()
    {
        if (maxHealth <= 0)
        {
            return 0f;
        }

        return (float)currentHealth / maxHealth;
    }

    public void UpdateHealthBar()
    {
        if (healthFill == null)
        {
            return;
        }

        healthFill.type = Image.Type.Filled;
        healthFill.fillAmount = GetHealthPercent();
    }

    private void OnHealthChanged(int previousHealth, int newHealth)
    {
        currentHealth = newHealth;
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    private void UpdatePlayerSystems()
    {
        bool enableSystems = IsAlive;
        if (enableSystems && isKnockedDown)
        {
            transform.localRotation = standingLocalRotation;
            isKnockedDown = false;
        }
        else if (!enableSystems && !isKnockedDown)
        {
            Vector3 knockedDownRotation = standingLocalRotation.eulerAngles;
            knockedDownRotation.z += 90f;
            transform.localRotation = Quaternion.Euler(knockedDownRotation);
            isKnockedDown = true;
        }

        PlayerController playerController = GetComponentInChildren<PlayerController>(true);
        if (playerController != null)
        {
            playerController.enabled = enableSystems;
        }

        foreach (PlayerPickupManager pickupManager in GetComponentsInChildren<PlayerPickupManager>(true))
        {
            pickupManager.enabled = enableSystems;
        }

        foreach (WeaponAttack weaponAttack in GetComponentsInChildren<WeaponAttack>(true))
        {
            weaponAttack.enabled = enableSystems;
        }

        foreach (RangedWeapon rangedWeapon in GetComponentsInChildren<RangedWeapon>(true))
        {
            rangedWeapon.enabled = enableSystems;
        }

        foreach (PlayerMouseAim mouseAim in GetComponentsInChildren<PlayerMouseAim>(true))
        {
            mouseAim.enabled = enableSystems;
        }
    }
}
