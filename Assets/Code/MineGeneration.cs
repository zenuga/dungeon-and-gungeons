using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChunkedMineGeneration : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject singleFloorPrefab;
    public GameObject dungeonPrefab;
    public GameObject shopPrefab;
    public Image AnimationImage;
    public int level = 1;

    [Header("UI & Spawn Settings")]
    public GameObject loadingImage;
    public Vector2Int spawnClearanceSize = new Vector2Int(10, 10);
    public float subFloorYOffset = -1.0f;
    [Tooltip("Offset applied to all dungeon/shop placement positions. Useful when the prefab pivot does not match the intended world origin.")]
    public Vector3 dungeonSpawnOffset = Vector3.zero;
    [Tooltip("Offset applied to the player spawn point created on the first generation.")]
    public Vector3 playerSpawnOffset = Vector3.zero;
    [Tooltip("Vertical height offset applied to wall blocks if their pivot is centered.")]
    public float wallYOffset = 0.0f;
    [Tooltip("Location players will be teleported to on second and subsequent mine generations.")]
    public Vector3 playerTeleportPosition = new Vector3(0f, 1f, 0f);
    [Tooltip("Offset from Player1's position where Player2 will be placed.")]
    public Vector3 player2SpawnOffset = new Vector3(1.5f, 0f, 0f);
    [Tooltip("Optional explicit spawn point for Player 1. If empty, the generated mine spawn is used.")]
    public Transform player1SpawnPoint;
    [Tooltip("Optional explicit spawn point for Player 2. If empty, Player 2 uses the legacy offset fallback.")]
    public Transform player2SpawnPoint;

    [Header("Grid Settings")]
    public int gridWidth = 250;
    public int gridLength = 250;
    public float spacing = 1.0f;

    private const int DungeonCount = 5;
    private static readonly Vector2Int DungeonSize = new Vector2Int(20, 20);
    private static readonly Vector2Int ShopSize = new Vector2Int(10, 5);

    private byte[,] _gridMap;
    private HashSet<Vector2Int> _destroyedBlocks = new HashSet<Vector2Int>();

    private Transform _playerTransform;
    private readonly List<GameObject> _generatedStructures = new List<GameObject>();
    private bool _generationInProgress;
    private Vector3 _currentMineSpawnPosition;

    private void Start()
    {
        StartCoroutine(GenerateMineAndChunks());
    }

    public void RegenerateMine()
    {
        StartCoroutine(GenerateMineAndChunks());
    }

    public IEnumerator GenerateMineAndChunks()
    {
        if (_generationInProgress)
        {
            yield break;
        }

        _generationInProgress = true;
        if (level >= 1 && AnimationImage != null)
        {
            AnimationImage.gameObject.SetActive(true);
        }

        if (loadingImage != null)
        {
            loadingImage.SetActive(true);
        }

        ClearGeneratedMine();
        yield return null;

        _gridMap = new byte[gridWidth, gridLength];

        // 1. Reserve Areas
        RectInt spawnRect = ReservePlayerSpawnArea();
        List<RectInt> occupiedRects = new List<RectInt> { spawnRect };

        for (int i = 0; i < DungeonCount; i++)
        {
            RectInt dungeonRect = GetRandomNonOverlappingRect(DungeonSize.x, DungeonSize.y, occupiedRects);
            occupiedRects.Add(dungeonRect);
            MarkGridArea(dungeonRect, 1);
            if (dungeonPrefab != null)
            {
                Vector3 spawnPosition = GetSpawnPosition(dungeonRect, dungeonSpawnOffset);
                _generatedStructures.Add(Instantiate(dungeonPrefab, spawnPosition, Quaternion.identity));
            }
        }

        RectInt shopRect = GetRandomNonOverlappingRect(ShopSize.x, ShopSize.y, occupiedRects);
        occupiedRects.Add(shopRect);
        MarkGridArea(shopRect, 2);
        if (shopPrefab != null)
        {
            Vector3 spawnPosition = GetSpawnPosition(shopRect, dungeonSpawnOffset);
            _generatedStructures.Add(Instantiate(shopPrefab, spawnPosition, Quaternion.identity));
        }

        // 2. Base Floor
        SpawnSingleScaledFloor();

        // 3. Spawn Walls Directly
        if (wallPrefab != null)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridLength; z++)
                {
                    if (_gridMap[x, z] != 0) continue;
                    if (_destroyedBlocks.Contains(new Vector2Int(x, z))) continue;

                    Vector3 pos = transform.position + new Vector3(x * spacing, wallYOffset, z * spacing);
                    GameObject wall = Instantiate(wallPrefab, pos, wallPrefab.transform.rotation, transform);
                    wall.name = $"wall_{x}_{z}";
                    wall.tag = "wall";

                    WallHealth wallHealth = wall.GetComponent<WallHealth>();
                    if (wallHealth == null)
                    {
                        wallHealth = wall.AddComponent<WallHealth>();
                    }
                    wallHealth.SetHealthForCurrentDepth();
                }

                yield return null; // Pause each row to prevent frame freezing on large grids
            }
        }

        // 4. Handle existing players without spawning duplicate characters.
        Vector3 spawnWorldPos = GetSpawnPosition(spawnRect, playerSpawnOffset);
        _currentMineSpawnPosition = GetPlayer1SpawnPosition(spawnWorldPos);

        yield return TeleportSpawnedPlayers(_currentMineSpawnPosition);

        _generationInProgress = false;
        HideGenerationImages();
    }

    private void HideGenerationImages()
    {
        if (loadingImage != null)
        {
            loadingImage.SetActive(false);
        }

        if (AnimationImage != null)
        {
            AnimationImage.gameObject.SetActive(false);
        }
    }

    private void ClearGeneratedMine()
    {
        foreach (GameObject structure in _generatedStructures)
        {
            if (structure != null)
            {
                Destroy(structure);
            }
        }
        _generatedStructures.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        _destroyedBlocks.Clear();
    }

    private void TeleportPlayer(GameObject playerObj, Vector3 targetPosition)
    {
        if (playerObj == null) return;

        CharacterController[] controllers = playerObj.GetComponentsInChildren<CharacterController>(true);
        foreach (CharacterController controller in controllers)
        {
            controller.enabled = false;
        }

        playerObj.transform.position = targetPosition;
        Physics.SyncTransforms();

        foreach (CharacterController controller in controllers)
        {
            controller.enabled = true;
        }
    }

    public void TeleportPlayerToMineSpawn(GameObject playerObj)
    {
        if (playerObj == null)
        {
            return;
        }

        Vector3 spawnPosition = _currentMineSpawnPosition;
        if (playerObj.CompareTag("Player2"))
        {
            spawnPosition = GetPlayer2SpawnPosition(_currentMineSpawnPosition);
        }

        TeleportPlayer(playerObj, spawnPosition);
    }

    private IEnumerator TeleportSpawnedPlayers(Vector3 targetPosition)
    {
        NetworkPlayerSpawner playerSpawner = FindFirstObjectByType<NetworkPlayerSpawner>();
        List<GameObject> players = new List<GameObject>();

        for (int attempt = 0; attempt < 120 && players.Count == 0; attempt++)
        {
            if (playerSpawner != null)
            {
                players = playerSpawner.GetSpawnedPlayerObjects();
            }

            if (players.Count == 0)
            {
                yield return null;
            }
        }

        GameObject player1 = FindPlayerInList(players, "Player1");
        GameObject player2 = FindPlayerInList(players, "Player2");

        if (player1 == null && players.Count > 0)
        {
            player1 = players[0];
        }

        if (player1 != null)
        {
            TeleportPlayer(player1, targetPosition);
        }

        if (player2 != null)
        {
            Vector3 player2Position = GetPlayer2SpawnPosition(targetPosition);
            TeleportPlayer(player2, player2Position);
        }

        foreach (GameObject player in players)
        {
            if (player != player1 && player != player2)
            {
                TeleportPlayer(player, targetPosition);
            }
        }

        if (players.Count > 0)
        {
            _playerTransform = players[0].transform;
        }
        else
        {
            _playerTransform = null;
            Debug.LogWarning("MineGeneration found no existing player objects with Player, Player1, or Player2 tags.", this);
        }
    }

    private static GameObject FindPlayerInList(List<GameObject> players, string tag)
    {
        foreach (GameObject player in players)
        {
            if (player != null && player.CompareTag(tag))
            {
                return player;
            }
        }

        return null;
    }

    public void RecordDestroyedBlock(int gridX, int gridZ)
    {
        _destroyedBlocks.Add(new Vector2Int(gridX, gridZ));
    }

    private RectInt ReservePlayerSpawnArea()
    {
        int startX = (gridWidth / 2) - (spawnClearanceSize.x / 2);
        int startZ = gridLength - spawnClearanceSize.y;
        RectInt spawnRect = new RectInt(startX, startZ, spawnClearanceSize.x, spawnClearanceSize.y);
        MarkGridArea(spawnRect, 3);
        return spawnRect;
    }

    private RectInt GetRandomNonOverlappingRect(int width, int height, List<RectInt> existingRects)
    {
        for (int i = 0; i < 500; i++)
        {
            RectInt candidate = new RectInt(Random.Range(0, gridWidth - width), Random.Range(0, gridLength - height), width, height);
            bool overlaps = false;
            foreach (RectInt existing in existingRects) 
            {
                if (candidate.Overlaps(existing)) { overlaps = true; break; }
            }
            if (!overlaps) return candidate;
        }
        return new RectInt(0, 0, width, height);
    }

    private void MarkGridArea(RectInt rect, byte value)
    {
        for (int x = rect.x; x < rect.x + rect.width; x++)
            for (int z = rect.y; z < rect.y + rect.height; z++)
                if (x >= 0 && x < gridWidth && z >= 0 && z < gridLength) _gridMap[x, z] = value;
    }

    private Vector3 GetWorldCenterPosition(RectInt rect)
    {
        return transform.position + new Vector3((rect.x + rect.width / 2f) * spacing, 0f, (rect.y + rect.height / 2f) * spacing);
    }

    private Vector3 GetSpawnPosition(RectInt rect, Vector3 offset)
    {
        return GetWorldCenterPosition(rect) + offset;
    }

    private Vector3 GetPlayer1SpawnPosition(Vector3 fallbackPosition)
    {
        return player1SpawnPoint != null ? player1SpawnPoint.position : fallbackPosition;
    }

    private Vector3 GetPlayer2SpawnPosition(Vector3 player1Position)
    {
        return player2SpawnPoint != null ? player2SpawnPoint.position : player1Position + player2SpawnOffset;
    }

    private void SpawnSingleScaledFloor()
    {
        if (singleFloorPrefab == null) return;
        float width = gridWidth * spacing, length = gridLength * spacing;
        Vector3 center = transform.position + new Vector3(width / 2f - spacing / 2f, subFloorYOffset, length / 2f - spacing / 2f);
        GameObject floor = Instantiate(singleFloorPrefab, center, Quaternion.identity, transform);
        MeshFilter mf = floor.GetComponent<MeshFilter>();
        floor.transform.localScale = (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.Contains("Plane")) 
            ? new Vector3(width / 10f, 1f, length / 10f) : new Vector3(width, 1f, length);
    }
}