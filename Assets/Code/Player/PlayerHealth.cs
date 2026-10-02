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
    [SerializeField] private GameObject hurtParticlePrefab;

    [SerializeField]
    private int currentHealth = 100;
    private NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> networkMaxHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private Transform visualModelTransform;
    private Quaternion standingVisualLocalRotation;
    private bool isKnockedDown;

    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => maxHealth;
    public bool IsAlive => currentHealth > 0;

    private void Awake()
    {
        PlayerController playerController = GetComponent<PlayerController>();
        visualModelTransform = playerController != null ? playerController.VisualModelTransform : null;
        if (visualModelTransform != null)
        {
            standingVisualLocalRotation = visualModelTransform.localRotation;
        }

        currentHealth = maxHealth;
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    public override void OnNetworkSpawn()
    {
        networkHealth.OnValueChanged += OnHealthChanged;
        networkMaxHealth.OnValueChanged += OnMaxHealthChanged;
        if (IsServer)
        {
            networkMaxHealth.Value = maxHealth;
            networkHealth.Value = maxHealth;
            currentHealth = maxHealth;
        }
        else
        {
            maxHealth = networkMaxHealth.Value;
            currentHealth = networkHealth.Value;
            UpdateHealthBar();
        }

        UpdatePlayerSystems();
    }

    public override void OnNetworkDespawn()
    {
        networkHealth.OnValueChanged -= OnHealthChanged;
        networkMaxHealth.OnValueChanged -= OnMaxHealthChanged;
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
        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (IsSpawned && IsServer)
        {
            networkHealth.Value = currentHealth;
        }
        UpdateHealthBar();
        if (!IsSpawned)
        {
            if (currentHealth < previousHealth) PlayHurtParticle();
            if (currentHealth <= 0) GameAudioManager.EnsureInstance().PlayPlayerDeath();
            else GameAudioManager.EnsureInstance().PlayPlayerHurt();
        }
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

    public void IncreaseMaxHealthAndHeal(int increase, float healPercent)
    {
        if (IsSpawned && !IsServer)
        {
            IncreaseMaxHealthAndHealServerRpc(increase, healPercent);
            return;
        }

        ApplyMaxHealthIncreaseAndHeal(increase, healPercent);
    }

    [ServerRpc(RequireOwnership = false)]
    private void IncreaseMaxHealthAndHealServerRpc(int increase, float healPercent)
    {
        ApplyMaxHealthIncreaseAndHeal(increase, healPercent);
    }

    private void ApplyMaxHealthIncreaseAndHeal(int increase, float healPercent)
    {
        maxHealth = Mathf.Max(1, maxHealth + Mathf.Max(0, increase));
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.RoundToInt(maxHealth * Mathf.Max(0f, healPercent)));
        if (IsSpawned && IsServer)
        {
            networkMaxHealth.Value = maxHealth;
            networkHealth.Value = currentHealth;
        }
        UpdateHealthBar();
        UpdatePlayerSystems();
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
        if (newHealth < previousHealth) PlayHurtParticle();
        GameAudioManager audio = GameAudioManager.EnsureInstance();
        if (newHealth <= 0 && previousHealth > 0) audio.PlayPlayerDeath();
        else if (newHealth < previousHealth) audio.PlayPlayerHurt();
        UpdateHealthBar();
        UpdatePlayerSystems();
    }

    private void PlayHurtParticle()
    {
        if (hurtParticlePrefab == null) return;

        CharacterController characterController = GetComponent<CharacterController>();
        Vector3 feetPosition = characterController != null
            ? new Vector3(transform.position.x, characterController.bounds.min.y, transform.position.z)
            : transform.position;
        GameObject effect = Instantiate(hurtParticlePrefab, feetPosition, Quaternion.identity);
        Destroy(effect, 5f);
    }

    private void OnMaxHealthChanged(int previousMaxHealth, int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        UpdateHealthBar();
    }

    private void UpdatePlayerSystems()
    {
        bool enableSystems = IsAlive;
        if (enableSystems && isKnockedDown)
        {
            if (visualModelTransform != null)
            {
                visualModelTransform.localRotation = standingVisualLocalRotation;
            }

            isKnockedDown = false;
        }
        else if (!enableSystems && !isKnockedDown)
        {
            if (visualModelTransform != null)
            {
                Vector3 knockedDownRotation = standingVisualLocalRotation.eulerAngles;
                knockedDownRotation.z += 90f;
                visualModelTransform.localRotation = Quaternion.Euler(knockedDownRotation);
            }

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
