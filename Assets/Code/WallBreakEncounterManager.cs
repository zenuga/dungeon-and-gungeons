using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;

public class WallBreakEncounterManager : MonoBehaviour
{
    private static WallBreakEncounterManager instance;

    [Header("Encounter")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField, Range(0f, 100f)] private float spawnChancePercent = 3f;
    [SerializeField, Min(0f)] private float spawnRadius = 3f;
    [SerializeField] private string warningText = "A hunter is after you!";

    private GameObject activeEnemy;
    private bool warningVisible;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        SetWarningVisible(false);
    }

    private void Update()
    {
        if (warningVisible && activeEnemy == null)
        {
            SetWarningVisible(false);
        }
    }

    public static void TrySpawnFromBrokenWall(Vector3 wallPosition)
    {
        if (instance == null || !NetworkSpawnUtility.IsServerOrOffline || instance.activeEnemy != null ||
            instance.enemyPrefab == null || Random.value * 100f >= instance.spawnChancePercent)
        {
            return;
        }

        PlayerHealth target = FindClosestLivingPlayer(wallPosition);
        if (target == null) return;

        Vector2 offset = Random.insideUnitCircle;
        if (offset.sqrMagnitude < 0.01f) offset = Vector2.right;
        offset = offset.normalized * Random.Range(instance.spawnRadius * 0.5f, instance.spawnRadius);
        Vector3 desiredPosition = target.transform.position + new Vector3(offset.x, 0f, offset.y);
        NavMeshHit navMeshHit;
        if (NavMesh.SamplePosition(desiredPosition, out navMeshHit, 5f, NavMesh.AllAreas))
        {
            desiredPosition = navMeshHit.position;
        }

        Vector3 facing = target.transform.position - desiredPosition;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        GameObject enemy = Instantiate(instance.enemyPrefab, desiredPosition, rotation);
        PrepareEnemy(enemy);
        WallBreakAmbusher ambusher = enemy.GetComponent<WallBreakAmbusher>();
        if (ambusher == null) ambusher = enemy.AddComponent<WallBreakAmbusher>();
        instance.activeEnemy = enemy;
        ambusher.Initialize(target);

        if (!NetworkSpawnUtility.SpawnIfNetworked(enemy))
        {
            Debug.LogWarning("The wall-break enemy must have a registered NetworkObject prefab in multiplayer.", instance);
            Destroy(enemy);
            instance.activeEnemy = null;
            return;
        }

        instance.SetWarningVisible(true);
    }

    public static void DismissForTransition()
    {
        if (instance == null || !NetworkSpawnUtility.IsServerOrOffline) return;
        instance.DismissActiveEnemy();
    }

    private void DismissActiveEnemy()
    {
        GameObject enemy = activeEnemy;
        activeEnemy = null;
        SetWarningVisible(false);
        if (enemy != null) NetworkSpawnUtility.DespawnOrDestroy(enemy);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningVisible == visible) return;
        warningVisible = visible;

        NetworkPlayerSpawner spawner = NetworkPlayerSpawner.Instance;
        if (NetworkSpawnUtility.IsNetworkSessionActive && spawner != null && spawner.IsSpawned && spawner.IsServer)
        {
            spawner.BroadcastWallBreakWarning(visible, warningText);
        }
        else
        {
            WallBreakEncounterUI.SetVisible(visible, warningText);
        }
    }

    private static PlayerHealth FindClosestLivingPlayer(Vector3 position)
    {
        PlayerHealth closest = null;
        float closestDistance = float.MaxValue;
        foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            if (player == null || !player.IsAlive) continue;
            float distance = Vector3.Distance(position, player.transform.position);
            if (distance < closestDistance)
            {
                closest = player;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private static void PrepareEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        foreach (EnemyAi enemyAi in enemy.GetComponentsInChildren<EnemyAi>(true))
        {
            enemyAi.SetExternallyControlled(true);
            if (enemyAi.CompareTag("Untagged")) enemyAi.tag = "Enemy";
        }

        foreach (Collider existingCollider in enemy.GetComponentsInChildren<Collider>(true))
        {
            existingCollider.enabled = false;
        }

        SphereCollider trigger = enemy.GetComponent<SphereCollider>();
        if (trigger == null) trigger = enemy.AddComponent<SphereCollider>();
        trigger.enabled = true;
        trigger.isTrigger = true;
        trigger.radius = 0.7f;

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        if (body == null) body = enemy.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null) agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 4f;
        agent.stoppingDistance = 0f;
    }
}

public static class WallBreakEncounterUI
{
    private static GameObject canvasObject;
    private static TextMeshProUGUI message;

    public static void SetVisible(bool visible, string text)
    {
        if (visible) EnsureCreated();
        if (canvasObject == null) return;
        message.text = text;
        canvasObject.SetActive(visible);
    }

    private static void EnsureCreated()
    {
        if (canvasObject != null) return;

        canvasObject = new GameObject("WallBreakEncounterWarning");
        Object.DontDestroyOnLoad(canvasObject);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject textObject = new GameObject("WarningText");
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.82f);
        rect.anchorMax = new Vector2(0.9f, 0.96f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        message = textObject.AddComponent<TextMeshProUGUI>();
        message.fontSize = 36f;
        message.alignment = TextAlignmentOptions.Center;
        message.color = Color.white;
        message.enableWordWrapping = true;
        message.outlineWidth = 0.2f;
        message.outlineColor = Color.black;
    }
}
