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
    [SerializeField] private Sprite healthWarning75Sprite;
    [SerializeField] private Sprite healthWarning50Sprite;
    [SerializeField] private Sprite healthWarning25Sprite;

    [SerializeField]
    private int currentHealth = 100;
    private Image healthWarning75Image;
    private Image healthWarning50Image;
    private Image healthWarning25Image;
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

        ResolveHealthWarningImages();
        UpdateHealthBar();
        DungeonProgressDisplay.SetProgress(DungeonWaveManager.CompletedDungeonCount);
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
        if (healthFill != null)
        {
            healthFill.type = Image.Type.Filled;
            healthFill.fillAmount = GetHealthPercent();
        }

        UpdateHealthWarningImages();
    }

    private void ResolveHealthWarningImages()
    {
        if (healthBarRoot == null) return;

        Image[] images = healthBarRoot.GetComponentsInChildren<Image>(true);
        healthWarning75Image = FindWarningImage(images, healthWarning75Sprite);
        healthWarning50Image = FindWarningImage(images, healthWarning50Sprite);
        healthWarning25Image = FindWarningImage(images, healthWarning25Sprite);

        if (healthWarning75Image == null) healthWarning75Image = CreateWarningImage("Health Warning 75", healthWarning75Sprite);
        if (healthWarning50Image == null) healthWarning50Image = CreateWarningImage("Health Warning 50", healthWarning50Sprite);
        if (healthWarning25Image == null) healthWarning25Image = CreateWarningImage("Health Warning 25", healthWarning25Sprite);

        if (healthWarning75Image != null) healthWarning75Image.raycastTarget = false;
        if (healthWarning50Image != null) healthWarning50Image.raycastTarget = false;
        if (healthWarning25Image != null) healthWarning25Image.raycastTarget = false;
    }

    private static Image FindWarningImage(Image[] images, Sprite sprite)
    {
        if (sprite == null || images == null) return null;
        foreach (Image image in images)
        {
            if (image != null && image.sprite == sprite) return image;
        }

        return null;
    }

    private Image CreateWarningImage(string objectName, Sprite sprite)
    {
        if (healthBarRoot == null || sprite == null) return null;

        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(healthBarRoot, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = healthFill != null && healthFill.rectTransform != null
            ? healthFill.rectTransform.sizeDelta
            : new Vector2(157f, 169f);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
        imageObject.transform.SetAsLastSibling();
        return image;
    }

    private void UpdateHealthWarningImages()
    {
        float healthPercent = GetHealthPercent();
        SetWarningAlpha(healthWarning75Image, healthPercent <= 0.75f);
        SetWarningAlpha(healthWarning50Image, healthPercent <= 0.50f);
        SetWarningAlpha(healthWarning25Image, healthPercent <= 0.25f);
    }

    private static void SetWarningAlpha(Image image, bool visible)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = visible ? 1f : 0f;
        image.color = color;
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
