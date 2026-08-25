using System.Collections.Generic;
using UnityEngine;

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

    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool dungeonCompleted = false;
    private bool playerIsInsideTrigger = false;
    private GameObject currentTriggerObject;

    public void DungeonEntered()
    {
        StartNextWave();
    }

    private void Update()
    {
        if (dungeonCompleted) return;

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
        currentWave++;
        int enemiesToSpawn = baseEnemiesPerWave + ((currentWave - 1) * enemiesAddedPerWave);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            GameObject enemyToSpawn = GetWeightedRandomEnemy();
            Transform spawnPoint = spawnPoints.Length > 0 ? spawnPoints[Random.Range(0, spawnPoints.Length)] : transform;
            
            GameObject spawnedEnemy = Instantiate(enemyToSpawn, spawnPoint.position, spawnPoint.rotation);
            activeEnemies.Add(spawnedEnemy);
        }
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
        Instantiate(chestPrefab, spawnPosition, Quaternion.identity);

        // 2. Disable walls if player is currently in the trigger zone
        if (playerIsInsideTrigger && currentTriggerObject != null)
        {
            OpenWalls(currentTriggerObject);
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