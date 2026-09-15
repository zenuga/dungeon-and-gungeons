using UnityEngine;

public class PlacedBomb : MonoBehaviour
{
    [Header("Explosion Setup")]
    [SerializeField] private float fuseTime = 3f;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Color Flashing Setup")]
    [SerializeField] private Color baseColor = Color.black;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float initialFlashSpeed = 4f;
    [SerializeField] private float finalFlashSpeed = 24f;

    private Renderer bombRenderer;
    private Material bombMaterial;
    private float timer;

    private void Awake()
    {
        bombRenderer = GetComponentInChildren<Renderer>();
        if (bombRenderer != null)
        {
            bombMaterial = bombRenderer.material;
            bombMaterial.color = baseColor;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / fuseTime);

        if (bombRenderer != null && bombMaterial != null)
        {
            float currentSpeed = Mathf.Lerp(initialFlashSpeed, finalFlashSpeed, progress);
            float pingPong = Mathf.PingPong(Time.time * currentSpeed, 1f);
            bombMaterial.color = Color.Lerp(baseColor, flashColor, pingPong);
        }

        if (timer >= fuseTime)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}