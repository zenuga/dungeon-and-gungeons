using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class DungeonWaveManager : MonoBehaviour
{
    [Header("Enemy Setup")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public List<int> enemyWeights = new List<int>(); // Element 0 should be your basic enemy

    [Header("Wave Settings")]
    public int totalWaves = 3;
    public int baseEnemiesPerWave = 4;
    public int enemiesAddedPerWave = 3;
    public Transform[] spawnPoints;

    [Header("Rewards & Exit")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint; // Drag empty GameObject located in middle of room
    [FormerlySerializedAs("rewardWeaponTemplates")]
    public List<WeaponData> rewardItems = new List<WeaponData>();

    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool dungeonCompleted = false;
    private bool dungeonStarted = false;
    private bool playerIsInsideTrigger = false;
    private GameObject currentTriggerObject;
    private Dungeonenter dungeonEnter;

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
        if (dungeonCompleted || dungeonStarted)
        {
            return;
        }

        dungeonStarted = true;
        StartNextWave();
    }

    private void Update()
    {
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

            Transform spawnPoint = spawnPoints.Length > 0 ? spawnPoints[Random.Range(0, spawnPoints.Length)] : transform;
            Vector3 spawnPosition = GetSpawnPositionOnNavMesh(spawnPoint.position);

            GameObject spawnedEnemy = Instantiate(enemyToSpawn, spawnPosition, spawnPoint.rotation);
            RegisterEnemy(spawnedEnemy);
        }
    }

    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }

    public void UnregisterEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        activeEnemies.Remove(enemy);
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
        Vector3 spawnPosition = chestSpawnPoint != null ? chestSpawnPoint.position : transform.position;
        if (chestPrefab != null)
        {
            GameObject chest = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
            RewardChest rewardChest = chest.GetComponentInChildren<RewardChest>(true);
            if (rewardChest == null)
            {
                rewardChest = chest.AddComponent<RewardChest>();
            }

            rewardChest.Configure(rewardItems);
        }

        // 2. Disable the walls owned by Dungeonenter.
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