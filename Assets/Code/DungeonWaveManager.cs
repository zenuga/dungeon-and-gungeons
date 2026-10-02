using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Unity.Netcode;

public class DungeonWaveManager : MonoBehaviour
{
    [Header("Enemy Setup")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public List<int> enemyWeights = new List<int>(); // Element 0 should be your basic enemy
    [Tooltip("Choose the base enemy prefab(s) that can be promoted to boss size and stats.")]
    public GameObject[] bossBaseEnemyPrefabs = new GameObject[0];
    [Min(1f)] public float bossScaleMultiplier = 2f;
    [Min(1)] public int bossHealthMultiplier = 5;
    [Min(1)] public int bossDamageMultiplier = 2;
    public GameObject ladderPrefab;
    public GameObject objectToDisableOnBossSpawn;

    [Header("Wave Settings")]
    public int totalWaves = 3;
    public int baseEnemiesPerWave = 4;
    public int enemiesAddedPerWave = 3;
    public Transform[] spawnPoints;

    [Header("Rewards & Exit")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint; // Drag empty GameObject located in middle of room
    public Vector3 chestSpawnOffset = Vector3.zero;
    public Vector3 ladderSpawnOffset = Vector3.zero;
    [FormerlySerializedAs("rewardWeaponTemplates")]
    public List<WeaponData> rewardItems = new List<WeaponData>();

    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool dungeonCompleted = false;
    private bool dungeonStarted = false;
    private bool playerIsInsideTrigger = false;
    private GameObject currentTriggerObject;
    private Dungeonenter dungeonEnter;
    private GameObject activeBoss;
    private GameObject spawnedChest;
    private GameObject spawnedLadder;
    private static int completedDungeonCount;

    public bool IsDungeonCompleted => dungeonCompleted;

    private void Awake()
    {
        dungeonEnter = GetComponent<Dungeonenter>();
        if (dungeonEnter == null)
        {
            dungeonEnter = GetComponentInChildren<Dungeonenter>(true);
        }
    }

    public void DungeonEntered()
    {
        if (!NetworkSpawnUtility.IsServerOrOffline)
        {
            return;
        }

        if (dungeonCompleted || dungeonStarted)
        {
            return;
        }

        dungeonStarted = true;
        completedDungeonCount++;
        GameObject bossPrefab = GetBossPrefab();
        if (completedDungeonCount >= 5 && bossPrefab != null)
        {
            currentWave = totalWaves;
            SpawnBoss(bossPrefab);
            if (objectToDisableOnBossSpawn != null)
            {
                objectToDisableOnBossSpawn.SetActive(false);
            }
            return;
        }
        StartNextWave();
    }

    private void SpawnBoss(GameObject bossPrefab)
    {
        Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[0] : transform;
        GameObject boss = Instantiate(bossPrefab, GetSpawnPositionOnNavMesh(spawnPoint.position), spawnPoint.rotation);
        boss.transform.localScale *= bossScaleMultiplier;
        boss.tag = "boss";
        foreach (EnemyAi bossAi in boss.GetComponentsInChildren<EnemyAi>(true))
        {
            bossAi.gameObject.tag = "boss";
            bossAi.SetBoss(bossHealthMultiplier, bossDamageMultiplier);
        }
        activeBoss = boss;
        RegisterEnemy(boss);
        NetworkSpawnUtility.SpawnIfNetworked(boss);
    }

    private GameObject GetBossPrefab()
    {
        if (bossBaseEnemyPrefabs == null || bossBaseEnemyPrefabs.Length == 0) return null;
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (GameObject prefab in bossBaseEnemyPrefabs)
            if (prefab != null) validPrefabs.Add(prefab);
        return validPrefabs.Count == 0 ? null : validPrefabs[Random.Range(0, validPrefabs.Count)];
    }

    private void Update()
    {
        if (!NetworkSpawnUtility.IsServerOrOffline)
        {
            return;
        }

        if (!dungeonStarted || dungeonCompleted)
        {
            return;
        }

        // Clear destroyed enemies from tracking list
        activeEnemies.RemoveAll(enemy => enemy == null);

        // Check if current wave is cleared
        if (activeEnemies.Count == 0)
        {
            if (currentWave < totalWaves)
            {
                StartNextWave();
            }
            else
            {
                CompleteDungeon();
            }
        }
    }

    private void StartNextWave()
    {
        if (!dungeonStarted)
        {
            return;
        }

        currentWave++;
        int enemiesToSpawn = baseEnemiesPerWave + ((currentWave - 1) * enemiesAddedPerWave);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            GameObject enemyToSpawn = GetWeightedRandomEnemy();
            if (enemyToSpawn == null)
            {
                continue;
            }

            Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[Random.Range(0, spawnPoints.Length)]
                : transform;
            Vector3 spawnPosition = GetSpawnPositionOnNavMesh(spawnPoint.position);

            GameObject spawnedEnemy = Instantiate(enemyToSpawn, spawnPosition, spawnPoint.rotation);
            RegisterEnemy(spawnedEnemy);
            NetworkSpawnUtility.SpawnIfNetworked(spawnedEnemy);
        }
    }

    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyAi[] enemyAis = enemy.GetComponentsInChildren<EnemyAi>(true);
        if (enemyAis.Length == 0)
        {
            AddActiveEnemy(enemy);
            return;
        }

        foreach (EnemyAi enemyAi in enemyAis)
        {
            if (enemyAi != null)
            {
                enemyAi.SetWaveManager(this);
                AddActiveEnemy(enemyAi.gameObject);
            }
        }
    }

    private void AddActiveEnemy(GameObject enemy)
    {
        if (!activeEnemies.Contains(enemy)) activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyAi enemyAi = enemy.GetComponent<EnemyAi>();
        if (enemyAi == null)
        {
            // A grouped prefab may report death from a child while tracking is per enemy.
            EnemyAi[] children = enemy.GetComponentsInChildren<EnemyAi>(true);
            foreach (EnemyAi child in children) activeEnemies.Remove(child.gameObject);
            activeEnemies.Remove(enemy);
        }
        else
        {
            activeEnemies.Remove(enemyAi.gameObject);
        }

        if (activeBoss != null && (enemy == activeBoss || enemy.transform.IsChildOf(activeBoss.transform)))
        {
            bool bossGroupStillAlive = activeEnemies.Exists(active => active != null &&
                (active == activeBoss || active.transform.IsChildOf(activeBoss.transform)));
            if (!bossGroupStillAlive)
            {
                EnemyAi defeatedBoss = enemy.GetComponent<EnemyAi>();
                if (defeatedBoss != null)
                    DropBossWeapon(defeatedBoss.SpecialBossWeaponDrop, enemy.transform.position);
                InstantiateLadder(activeBoss.transform.position);
                activeBoss = null;
            }
        }
    }

    private void DropBossWeapon(WeaponData weaponData, Vector3 position)
    {
        if (weaponData == null || weaponData.weaponPrefab == null) return;

        GameObject droppedWeapon = Instantiate(weaponData.weaponPrefab, position + Vector3.up * 0.5f, Quaternion.identity);
        CollectibleItem collectible = droppedWeapon.GetComponent<CollectibleItem>();
        if (collectible == null) collectible = droppedWeapon.AddComponent<CollectibleItem>();
        collectible.itemType = weaponData.weaponPrefab.GetComponentInChildren<WeaponAttack>() != null ? "melee" : "ranged";
        collectible.weaponData = weaponData;

        Collider weaponCollider = droppedWeapon.GetComponentInChildren<Collider>();
        if (weaponCollider == null) weaponCollider = droppedWeapon.AddComponent<BoxCollider>();
        weaponCollider.isTrigger = true;
        NetworkSpawnUtility.SpawnIfNetworked(droppedWeapon);
    }

    private void InstantiateLadder(Vector3 position)
    {
        if (ladderPrefab != null)
        {
            spawnedLadder = Instantiate(ladderPrefab, position + ladderSpawnOffset, Quaternion.identity);
            NetworkSpawnUtility.SpawnIfNetworked(spawnedLadder);
        }
    }

    public void ClearDungeonRewardsAndLadder()
    {
        if (spawnedChest != null)
        {
            RewardChest rewardChest = spawnedChest.GetComponentInChildren<RewardChest>(true);
            if (rewardChest != null)
            {
                rewardChest.ClearSpawnedRewards();
            }

            Destroy(spawnedChest);
            spawnedChest = null;
        }

        if (spawnedLadder != null)
        {
            Destroy(spawnedLadder);
            spawnedLadder = null;
        }
    }

    private Vector3 GetSpawnPositionOnNavMesh(Vector3 desiredPosition)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(desiredPosition, out hit, 3f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return desiredPosition;
    }

    private GameObject GetWeightedRandomEnemy()
    {
        if (enemyPrefabs.Count == 0 || enemyPrefabs.Count != enemyWeights.Count)
        {
            Debug.LogError("Enemy Prefabs and Enemy Weights lists must be assigned and equal in size!");
            return null;
        }

        // Dynamically adjust weights: Increase basic enemy weight as wave count grows 
        // to prevent rare enemies from over-spawning when enemy density increases.
        List<int> adjustedWeights = new List<int>(enemyWeights);
        adjustedWeights[0] += (currentWave - 1) * 5; 

        int totalWeight = 0;
        foreach (int weight in adjustedWeights)
        {
            totalWeight += weight;
        }

        int randomRoll = Random.Range(0, totalWeight);
        int accumulatedWeight = 0;

        for (int i = 0; i < adjustedWeights.Count; i++)
        {
            accumulatedWeight += adjustedWeights[i];
            if (randomRoll < accumulatedWeight)
            {
                return enemyPrefabs[i];
            }
        }

        return enemyPrefabs[0];
    }

    private void CompleteDungeon()
    {
        dungeonCompleted = true;

        // 1. Spawn Chest in the middle of the dungeon
        Vector3 spawnPosition = (chestSpawnPoint != null ? chestSpawnPoint.position : transform.position) + chestSpawnOffset;
        if (chestPrefab != null)
        {
            spawnedChest = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
            NetworkSpawnUtility.SpawnIfNetworked(spawnedChest);
            RewardChest rewardChest = spawnedChest.GetComponentInChildren<RewardChest>(true);
            if (rewardChest == null)
            {
                rewardChest = spawnedChest.AddComponent<RewardChest>();
            }

            rewardChest.Configure(rewardItems);
        }

        // 2. Disable the walls owned by Dungeonenter.
        if (dungeonEnter != null)
        {
            dungeonEnter.DisableWalls();
        }
    }

    public void ApplyNetworkCompletion()
    {
        dungeonCompleted = true;
        if (dungeonEnter != null)
        {
            dungeonEnter.DisableWalls();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerIsInsideTrigger = true;
            currentTriggerObject = gameObject;

            // If 3 waves are done and player enters trigger, disable walls
            if (dungeonCompleted)
            {
                OpenWalls(gameObject);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerIsInsideTrigger = false;
            currentTriggerObject = null;
        }
    }

    private bool IsPlayer(Collider col)
    {
        return col.CompareTag("Player") || col.CompareTag("Player1") || col.CompareTag("Player2");
    }

    private void OpenWalls(GameObject triggerObj)
    {
        foreach (Transform child in triggerObj.transform)
        {
            if (child.CompareTag("walls"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}
