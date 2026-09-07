using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Player Setup")]
    [SerializeField] private string playerTag = "Player1";
    [SerializeField] private Image healthFill;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool destroyOnZero = false;

    private int currentHealth;
    private NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public override void OnNetworkSpawn()
    {
        networkHealth.OnValueChanged += OnHealthChanged;
        if (IsServer)
        {
            networkHealth.Value = maxHealth;
        }
        else
        {
            currentHealth = networkHealth.Value;
            UpdateHealthBar();
        }
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

    private void LateUpdate()
    {
        if (healthBarRoot == null || Camera.main == null)
        {
            return;
        }

        Vector3 directionToCamera = Camera.main.transform.position - healthBarRoot.position;
        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            healthBarRoot.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
        }
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

        healthFill.fillAmount = GetHealthPercent();
    }

    private void OnHealthChanged(int previousHealth, int newHealth)
    {
        currentHealth = newHealth;
        UpdateHealthBar();
    }
}
