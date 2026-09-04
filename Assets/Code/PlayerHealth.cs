using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Player Setup")]
    [SerializeField] private string playerTag = "Player1";
    [SerializeField] private Image healthFill;
    [SerializeField] private Transform healthBarRoot;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool destroyOnZero = false;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealthValue => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        UpdateHealthBar();
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

        currentHealth = Mathf.Max(0, currentHealth - amount);
        UpdateHealthBar();
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthBar();
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
}
