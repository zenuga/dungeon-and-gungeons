using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkedMineGeneration : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject singleFloorPrefab;
    public GameObject dungeonPrefab;
    public GameObject shopPrefab;
    public GameObject playerPrefab;

    [Header("UI & Spawn Settings")]
    public GameObject loadingImage;
    public Vector2Int spawnClearanceSize = new Vector2Int(10, 10);
    public float subFloorYOffset = -1.0f;

    [Header("Grid & Chunk Settings")]
    public int gridWidth = 250;
    public int gridLength = 250;
    public int chunkSize = 25; // 25x25 cells per chunk
    public float spacing = 1.0f;

    [Header("Chunk Loading Settings")]
    [Tooltip("Distance (in chunks) where blocks become individual interactable prefabs.")]
    public int uncombineDistanceInChunks = 1; 
    
    [Tooltip("Distance (in chunks) where blocks are rendered as 1 massive mesh.")]
    public int viewDistanceInChunks = 3;
    
    [Tooltip("How often (in seconds) to check player position for chunk updates.")]
    public float chunkUpdateInterval = 0.5f;

    private const int DungeonCount = 5;
    private static readonly Vector2Int DungeonSize = new Vector2Int(50, 50);
    private static readonly Vector2Int ShopSize = new Vector2Int(20, 10);

    private byte[,] _gridMap;

    private Dictionary<Vector2Int, ChunkData> _chunks = new Dictionary<Vector2Int, ChunkData>();
    private HashSet<Vector2Int> _destroyedBlocks = new HashSet<Vector2Int>();
    
    private Transform _playerTransform;
    private Vector2Int _currentPlayerChunk;

    private class ChunkData
    {
        public GameObject ChunkObject;
        public bool IsCombined;
        public List<GameObject> IndividualBlocks = new List<GameObject>(); // Tracks active real cubes
    }

    private void Start()
    {
        StartCoroutine(GenerateMineAndChunks());
    }

    private IEnumerator GenerateMineAndChunks()
    {
        _gridMap = new byte[gridWidth, gridLength];

        // 1. Reserve Areas
        RectInt spawnRect = ReservePlayerSpawnArea();
        List<RectInt> occupiedRects = new List<RectInt> { spawnRect };

        for (int i = 0; i < DungeonCount; i++)
        {
            RectInt dungeonRect = GetRandomNonOverlappingRect(DungeonSize.x, DungeonSize.y, occupiedRects);
            occupiedRects.Add(dungeonRect);
            MarkGridArea(dungeonRect, 1);
            if (dungeonPrefab != null) Instantiate(dungeonPrefab, GetWorldCenterPosition(dungeonRect), Quaternion.identity);
        }

        RectInt shopRect = GetRandomNonOverlappingRect(ShopSize.x, ShopSize.y, occupiedRects);
        occupiedRects.Add(shopRect);
        MarkGridArea(shopRect, 2);
        if (shopPrefab != null) Instantiate(shopPrefab, GetWorldCenterPosition(shopRect), Quaternion.identity);

        // 2. Base Floor
        SpawnSingleScaledFloor();

        // 3. Initialize Chunks
        int chunksX = Mathf.CeilToInt((float)gridWidth / chunkSize);
        int chunksZ = Mathf.CeilToInt((float)gridLength / chunkSize);

        for (int chunkX = 0; chunkX < chunksX; chunkX++)
        {
            for (int chunkZ = 0; chunkZ < chunksZ; chunkZ++)
            {
                CreateAndBuildChunk(chunkX, chunkZ);
                yield return null; 
            }
        }

        // 4. Spawn Player
        if (playerPrefab != null)
        {
            GameObject spawnedPlayer = Instantiate(playerPrefab, GetWorldCenterPosition(spawnRect), Quaternion.identity);
            _playerTransform = spawnedPlayer.transform;
        }

        if (loadingImage != null) loadingImage.SetActive(false);

        // 5. Start Chunk Update Loop
        StartCoroutine(UpdateChunksRoutine());
    }

    private void CreateAndBuildChunk(int chunkX, int chunkZ)
    {
        Vector2Int chunkCoord = new Vector2Int(chunkX, chunkZ);
        GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkZ}");
        chunkObj.transform.parent = this.transform;

        ChunkData data = new ChunkData
        {
            ChunkObject = chunkObj,
            IsCombined = false // Will be set to true in BuildChunkMesh
        };

        _chunks.Add(chunkCoord, data);
        
        // Start out as a combined mesh, but hidden
        BuildChunkMesh(chunkCoord, data);
        chunkObj.SetActive(false); 
    }

    private IEnumerator UpdateChunksRoutine()
    {
        while (true)
        {
            if (_playerTransform != null) UpdateChunkVisibilities();
            yield return new WaitForSeconds(chunkUpdateInterval);
        }
    }

    private void UpdateChunkVisibilities()
    {
        int playerChunkX = Mathf.FloorToInt((_playerTransform.position.x - transform.position.x) / (chunkSize * spacing));
        int playerChunkZ = Mathf.FloorToInt((_playerTransform.position.z - transform.position.z) / (chunkSize * spacing));
        _currentPlayerChunk = new Vector2Int(playerChunkX, playerChunkZ);

        foreach (var kvp in _chunks)
        {
            Vector2Int chunkCoord = kvp.Key;
            ChunkData chunkData = kvp.Value;

            // Calculate chunk distance from player
            int distX = Mathf.Abs(chunkCoord.x - _currentPlayerChunk.x);
            int distZ = Mathf.Abs(chunkCoord.y - _currentPlayerChunk.y);
            int maxDist = Mathf.Max(distX, distZ);

            if (maxDist <= uncombineDistanceInChunks)
            {
                // NEARBY: Should be individual cubes
                if (!chunkData.ChunkObject.activeSelf) chunkData.ChunkObject.SetActive(true);
                if (chunkData.IsCombined) SpawnIndividualBlocks(chunkCoord, chunkData);
            }
            else if (maxDist <= viewDistanceInChunks)
            {
                // FAR: Should be one massive mesh
                if (!chunkData.ChunkObject.activeSelf) chunkData.ChunkObject.SetActive(true);
                if (!chunkData.IsCombined) BuildChunkMesh(chunkCoord, chunkData);
            }
            else
            {
                // TOO FAR: Should be disabled
                if (chunkData.ChunkObject.activeSelf) chunkData.ChunkObject.SetActive(false);
            }
        }
    }

    // =========================================================================
    // STATE 1: UNCOMBINED (Individual Prefabs for interaction)
    // =========================================================================
    private void SpawnIndividualBlocks(Vector2Int chunkCoord, ChunkData chunkData)
    {
        // 1. Destroy the combined mesh components so we don't double-render
        Destroy(chunkData.ChunkObject.GetComponent<MeshFilter>());
        Destroy(chunkData.ChunkObject.GetComponent<MeshRenderer>());
        Destroy(chunkData.ChunkObject.GetComponent<MeshCollider>());

        int startX = chunkCoord.x * chunkSize;
        int startZ = chunkCoord.y * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, gridWidth);
        int endZ = Mathf.Min(startZ + chunkSize, gridLength);

        // 2. Spawn real GameObjects
        for (int x = startX; x < endX; x++)
        {
            for (int z = startZ; z < endZ; z++)
            {
                if (_gridMap[x, z] != 0) continue; 
                if (_destroyedBlocks.Contains(new Vector2Int(x, z))) continue; 

                Vector3 pos = transform.position + new Vector3(x * spacing, 0f, z * spacing);
                GameObject realBlock = Instantiate(wallPrefab, pos, Quaternion.identity, chunkData.ChunkObject.transform);
                
                // Add a script or tag here if your raycast needs to identify it
                realBlock.name = $"Wall_{x}_{z}";
                
                chunkData.IndividualBlocks.Add(realBlock);
            }
        }

        chunkData.IsCombined = false;
    }

    // =========================================================================
    // STATE 2: COMBINED (1 Optimized Mesh for distance)
    // =========================================================================
    private void BuildChunkMesh(Vector2Int chunkCoord, ChunkData chunkData)
    {
        // 1. Destroy individual blocks if they exist to free RAM
        foreach (GameObject block in chunkData.IndividualBlocks)
        {
            if (block != null) Destroy(block);
        }
        chunkData.IndividualBlocks.Clear();

        int startX = chunkCoord.x * chunkSize;
        int startZ = chunkCoord.y * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, gridWidth);
        int endZ = Mathf.Min(startZ + chunkSize, gridLength);

        List<CombineInstance> combineList = new List<CombineInstance>();
        Mesh sourceMesh = wallPrefab.GetComponent<MeshFilter>().sharedMesh;

        // 2. Build the combined mesh
        for (int x = startX; x < endX; x++)
        {
            for (int z = startZ; z < endZ; z++)
            {
                if (_gridMap[x, z] != 0) continue;
                if (_destroyedBlocks.Contains(new Vector2Int(x, z))) continue;

                Vector3 pos = transform.position + new Vector3(x * spacing, 0f, z * spacing);
                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
                combineList.Add(new CombineInstance { mesh = sourceMesh, transform = matrix });
            }
        }

        if (combineList.Count > 0)
        {
            MeshFilter mf = chunkData.ChunkObject.AddComponent<MeshFilter>();
            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combineList.ToArray(), true, true);
            mf.mesh = combinedMesh;

            MeshRenderer mr = chunkData.ChunkObject.AddComponent<MeshRenderer>();
            mr.material = wallPrefab.GetComponent<MeshRenderer>().sharedMaterial;

            MeshCollider mc = chunkData.ChunkObject.AddComponent<MeshCollider>();
            mc.sharedMesh = combinedMesh;
        }

        chunkData.IsCombined = true;
    }

    // =========================================================================
    // MINING LOGIC
    // =========================================================================
    public void RecordDestroyedBlock(int gridX, int gridZ)
    {
        // When your player mines a block, call this function so it never regenerates!
        _destroyedBlocks.Add(new Vector2Int(gridX, gridZ));
    }


    // =========================================================================
    // UTILITIES
    // =========================================================================
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
            foreach (RectInt existing in existingRects) if (candidate.Overlaps(existing)) { overlaps = true; break; }
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

    private void SpawnSingleScaledFloor()
    {
        if (singleFloorPrefab == null) return;
        float width = gridWidth * spacing, length = gridLength * spacing;
        Vector3 center = transform.position + new Vector3(width / 2f - spacing / 2f, subFloorYOffset, length / 2f - spacing / 2f);
        GameObject floor = Instantiate(singleFloorPrefab, center, Quaternion.identity, transform);
        MeshFilter mf = floor.GetComponent<MeshFilter>();
        floor.transform.localScale = (mf != null && mf.sharedMesh.name.Contains("Plane")) 
            ? new Vector3(width / 10f, 1f, length / 10f) : new Vector3(width, 1f, length);
    }
}