using UnityEngine;

public class PlacedBomb : MonoBehaviour
{
    [Header("Explosion Setup")]
    [SerializeField] private float fuseTime = 3f;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Color Flashing Setup")]
    [ColorUsage(true, true)] [SerializeField] private Color baseColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float initialFlashSpeed = 2f;
    [SerializeField] private float finalFlashSpeed = 12f;

    [Header("URP Setup")]
    [Tooltip("Enable if you want the bomb emission/glow to flash")]
    [SerializeField] private bool flashEmission = true;

    private Material bombMaterial;
    private float timer;
    private float flashPhase;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        Renderer bombRenderer = GetComponent<Renderer>();
        if (bombRenderer != null)
        {
            bombMaterial = bombRenderer.material;

            if (flashEmission)
            {
                bombMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / fuseTime);

        if (bombMaterial != null)
        {
            // Calculate current frequency speed
            float currentSpeed = Mathf.Lerp(initialFlashSpeed, finalFlashSpeed, progress);

            // Accumulate phase delta smoothly to prevent phase jumps caused by Time.time
            flashPhase += Time.deltaTime * currentSpeed;

            float pingPong = Mathf.PingPong(flashPhase, 1f);
            Color currentColor = Color.Lerp(baseColor, flashColor, pingPong);

            bombMaterial.SetColor(BaseColorID, currentColor);

            if (flashEmission)
            {
                bombMaterial.SetColor(EmissionColorID, currentColor);
            }
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