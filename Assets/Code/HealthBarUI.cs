using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private bool faceCamera = true;
    public EnemyAi enemyAi;

    public void Initialize(Image backgroundImage, EnemyAi owner)
    {
        enemyAi = owner;

        if (fillImage == null && backgroundImage != null)
        {
            fillImage = FindFillImage(backgroundImage);
        }

        if (fillImage == null)
        {
            Debug.LogWarning(name + " has no child Image assigned as its health fill.", this);
            return;
        }

        fillImage.gameObject.SetActive(true);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillClockwise = true;
    }

    private void Update()
    {
        if (enemyAi != null)
        {
            SetHealth(enemyAi.CurrentHealth, enemyAi.MaxHealthValue);
        }
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    }

    private void LateUpdate()
    {
        if (!faceCamera || Camera.main == null)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(
            -Camera.main.transform.forward,
            Camera.main.transform.up);
    }

    private static Image FindFillImage(Image backgroundImage)
    {
        Image[] images = backgroundImage.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image.transform != backgroundImage.transform)
            {
                return image;
            }
        }

        return backgroundImage;
    }
}
