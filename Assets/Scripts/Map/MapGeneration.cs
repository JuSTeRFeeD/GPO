using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Tilemaps;
using Voronoi2;
using Random = UnityEngine.Random;

public class MapGeneration : MonoBehaviour
{
    [Header("Preferences")]
    [SerializeField] private Tile pointTile;
    [SerializeField] private Tile linePoint;
    [SerializeField] private Tile groundTile;
    [SerializeField] private Tile groundTile2;
    [Space]
    [SerializeField] private Tilemap ground;

    private int[,] voronoiMap; // Линии пересечений рисуются тут (заполнение уже на самой tilemap)

    [SerializeField] private Tile[] tiles;

    [Space]
    private Voronoi voroObject = new Voronoi(0.1f);
    private List<GraphEdge> ge;
    private List<Vector2> sites;

    [Header("Settings")]
    
    [SerializeField] private bool isEditMode = true;
    [Range(64, 3000)]
    [SerializeField] private int siteCount = 200;
    [Range(0, 0.9f)]
    [SerializeField] private float minSizeBetweenPoints = 0.7f;
    [Range(64, 512)]
    [SerializeField] private int mapSize = 512;
    
    [Space]
    [Range(0, 1.0f)]
    [SerializeField] private float fillRadius = 0.8f;
    [Range(0, 1f)]
    [SerializeField] private float cuteRadius = 0.8f;
    [Range(0, 1f)]
    [SerializeField] private float chanceToSaveBorder= 0.5f;
    
    [Space]
    [SerializeField] private int __devSeed = 0;

    private float FillRadius => (fillRadius * mapSize / 2);
    private Vector3Int MapCenter => new Vector3Int((int)((float)mapSize / 2), (int)((float)mapSize / 2), 0);


    // Objects Distribution
    [Space] [Header("Objects and structures")]
    [SerializeField] private GameObject spawnPlace;
    [SerializeField] private float minDistanceBetweenObject = 1f;
    [Space]
    [SerializeField] private LayerMask resourcesLayer; 
    [SerializeField] private Transform objectsContainer;
    [SerializeField] private GameObject[] treesPrefabs;
    [SerializeField] private GameObject[] bushesPrefabs;
    [SerializeField] private GameObject[] stonesPrefabs;
    
    [Range(0.0f, 0.5f)] [SerializeField] private float treeChance = 0.5f;
    [Range(0.0f, 100.0f)] [SerializeField] private float scaler = 0.5f;
    [SerializeField] private Tilemap perlinGround;
    
    private void Awake()
    {
        GenerateGround();
    }

    private void HandleDevButtons()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateGround();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            __devSeed--;
            GenerateGround();
            return;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            __devSeed++;
            GenerateGround();
            return;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.LogError("ERORR - P");
            Debug.Break();
        }
    }

    private void Update()
    {
        HandleDevButtons();
    }
    private void GenerateGround()
    {
        ClearMap();
        SpreadPoints();
        
        FillVoronoiGhostMap(); // Для границ карты
        FillTilesInRadius();
        FillEmpty();
        ObjectsDistribution();

        // Спавн поинт
        var spawnPos = new Vector2(MapCenter.x, MapCenter.y) + Random.insideUnitCircle * 5;
        Instantiate(spawnPlace, spawnPos, Quaternion.identity, objectsContainer);
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            player.transform.position = spawnPos;
        }
    }

    private void ClearMap()
    {
        foreach (Transform child in objectsContainer) {
            Destroy(child.gameObject);
        }
        ground.ClearAllTiles();
    }

    private List<GraphEdge> MakeVoronoiGraph(List<Vector2> sites, int width, int height)
    {
        double[] xVal = new double[sites.Count];
        double[] yVal = new double[sites.Count];
        for (int i = 0; i < sites.Count; i++)
        {
            xVal[i] = sites[i].x;
            yVal[i] = sites[i].y;
        }
        return voroObject.generateVoronoi(xVal, yVal, 0, width, 0, height);

    }

    private void SpreadPoints()
    {
        sites = new List<Vector2>();
        // #if DEV
        int seed = __devSeed; // Random.Range(0, 999999999);
        Random.InitState(seed);

        Debug.Log("SEED: " + seed);
        
        // Calculating small cube size
        float area = (mapSize * mapSize) / (float)siteCount;
        float areaLen = Mathf.Sqrt(area);
        for (float y = 0; y < mapSize - areaLen; y += areaLen)
        {
            bool slideY = false;
            for (float x = 0; x < mapSize - areaLen; x += areaLen)
            {
                // Calculating circle inside small cube and putting random point                
                float squareCenterX = x + areaLen / 2;
                float squareCenterY = y + (slideY ? areaLen / 2 : areaLen);
                float radius = areaLen * minSizeBetweenPoints;
                slideY = !slideY;

                var point = Random.insideUnitCircle * radius;
                
                sites.Add(new Vector2((float)(point.x + squareCenterX), (float)(point.y + squareCenterY)));        
            }
        }
        
        ge = MakeVoronoiGraph(sites, mapSize, mapSize);
    }

    private void FillVoronoiGhostMap()
    {
        voronoiMap = new int[mapSize, mapSize];
        // Рисуем
        for (var i = 0; i < ge.Count; i++)
        {
            var p1 = new Vector3Int((int)ge[i].x1, (int)ge[i].y1, 0);
            var p2 = new Vector3Int((int)ge[i].x2, (int)ge[i].y2, 0);

            VoronoiMapLine(p1, p2, ref voronoiMap);
            
            
            // Линии между пересечениями 
            // TileLine(p1, p2, linePoint);

            // Точки пересечения
            // ground.SetTile(new Vector3Int((int)ge[i].x1, (int)ge[i].y1, 0), pointTile);
            // ground.SetTile(new Vector3Int((int)ge[i].x2, (int)ge[i].y2, 0), pointTile);
        }
    }

    private void VoronoiMapLine(Vector3Int p1, Vector3Int p2, ref int[,] map)
    {
        while (p1.x != p2.x || p1.y != p2.y)
        {
            map[p1.x, p1.y] = 1;

            if (p1.x < p2.x)
                p1.x++;
            else if (p1.x > p2.x)
                p1.x--;

            if (p1.y < p2.y)
                p1.y++;
            else if (p1.y > p2.y)
                p1.y--;
        }
    }
    
    private void FillTilesInRadius()
    {
        for (var i = 0; i < sites.Count; i++)
        {
            if (PointInCircleByRadius(sites[i], FillRadius))
            {
                if (!PointInCircleByRadius(sites[i], FillRadius * cuteRadius) && Random.Range(0.0f, 1f) <= chanceToSaveBorder)
                    continue;
                
                Vector3Int point = new Vector3Int((int)sites[i].x, (int)sites[i].y, 0);
                FillArea(point);
            }
        }
    }
    
    private bool PointInCircleByRadius(Vector2 point, float radius)
    {
        var dist = Vector2.Distance(new Vector2(MapCenter.x, MapCenter.y), point);
        return (dist < radius);
    }
    
    private void FillArea(Vector3Int centroid)
    { 
        int limit = 256; 
        var filled = new List<Vector3Int>();
        filled.Add(centroid);
        while (true)
        {
            var filledOneAtLeast = false;
            var pointsToDelete = new List<Vector3Int>();
            for (var i = 0; i < filled.Count; i++)
            {
                var tile = tiles[Random.Range(0, tiles.Length)];
                var _point = filled[i];
                var next = _point + Vector3Int.right;
                if (voronoiMap[next.x, next.y] != 1 && !ground.HasTile(next))
                {
                    filledOneAtLeast = true;
                    ground.SetTile(next, tile);
                    filled.Add(next);
                }

                next = _point + Vector3Int.left;
                if (voronoiMap[next.x, next.y] != 1 && !ground.HasTile(next))
                {
                    filledOneAtLeast = true;
                    ground.SetTile(next, tile);
                    filled.Add(next);
                }

                next = _point + Vector3Int.up;
                if (voronoiMap[next.x, next.y] != 1 && !ground.HasTile(next))
                {
                    filledOneAtLeast = true;
                    ground.SetTile(next, tile);
                    filled.Add(next);
                }

                next = _point + Vector3Int.down;
                if (voronoiMap[next.x, next.y] != 1 && !ground.HasTile(next))
                {
                    filledOneAtLeast = true;
                    ground.SetTile(next, tile);
                    filled.Add(next);
                }
                pointsToDelete.Add(_point);
            }
            foreach (var p in pointsToDelete) filled.Remove(p);
            if (!filledOneAtLeast || limit-- < 0) break;
        }
    }

    private void FillEmpty() // Заполение пустого пространства между ячейками
    {
        for (var x = 0; x < mapSize; x++)
        {
            for (var y = 0; y < mapSize; y++)
            {
                if (ground.HasTile(new Vector3Int(x, y, 0))) continue;
                
                if ((ground.HasTile(new Vector3Int(x + 1, y, 0)) || ground.HasTile(new Vector3Int(x + 2, y, 0)))
                && ground.HasTile(new Vector3Int(x - 1, y, 0)))
                {
                    ground.SetTile(new Vector3Int(x, y, 0), pointTile);
                }
                if ((ground.HasTile(new Vector3Int(x, y + 1, 0)) || ground.HasTile(new Vector3Int(x, y + 2, 0)))
                && ground.HasTile(new Vector3Int(x, y - 1, 0)))
                {
                    ground.SetTile(new Vector3Int(x, y, 0), pointTile);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (isEditMode)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(MapCenter.x, MapCenter.y, 0), FillRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(new Vector3(MapCenter.x, MapCenter.y, 0), FillRadius * cuteRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(MapCenter.x, MapCenter.y, 0), new Vector3(mapSize, mapSize));
            
            Gizmos.color = Color.grey;
            float area = (mapSize * mapSize) / (float)siteCount;
            float areaLen = Mathf.Sqrt(area);
            for (float y = 0; y < mapSize - areaLen; y += areaLen)
            {
                bool slideY = false;
                for (float x = 0; x < mapSize - areaLen; x += areaLen)
                {
                    // Calculating circle inside small cube and putting random point                
                    float squareCenterX = x + areaLen / 2;
                    float squareCenterY = y + (slideY ? areaLen / 2 : areaLen);
                    float radius = areaLen * minSizeBetweenPoints;
                    slideY = !slideY;

                    Gizmos.DrawWireSphere(new Vector3(squareCenterX, squareCenterY, 0), radius);
                }
            }
        }

        if (ge == null) return;
        foreach (var t in ge)
        {
            var p1 = new Vector3Int((int)t.x1, (int)t.y1, 0);
            var p2 = new Vector3Int((int)t.x2, (int)t.y2, 0);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(p1, 2f);
            Gizmos.DrawWireSphere(p2, 2f);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(p1, p2);
        }
        for (int i = 0; i < sites.Count; i++)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector2(sites[i].x - 1.5f, sites[i].y - 1.5f), 2f);
        }
    }

    // Проверять объекты по близости чтобы не спавнить впритык
    // Объектов по близости нет или есть
    private bool CheckObjectNear(Vector2 pos, float radius)
    {
        Collider2D[] results = new Collider2D[10];
        Physics2D.OverlapCircleNonAlloc(pos, radius, results, resourcesLayer);
        foreach (var item in results) if (item != null) return false;
        return true;
    }
    
    // Размещение ресурсов
    private void ObjectsDistribution()
    {
        // Ресурсы
        var bushes = CalcNoise(0.4f, 0.59f);
        var trees = CalcNoise(0.5f, 0.9f);
        var stones = CalcNoise(0.1f, 0.5f, 20000, 20000);
        for (var i = 0; i < trees.Count; i++) 
        {
            var pos = trees[i];
            if (Random.Range(0.0f, 1f) > 0.9f 
                && ground.HasTile(pos) 
                && PointInCircleByRadius(new Vector2(pos.x, pos.y), FillRadius)
                && CheckObjectNear(new Vector2(pos.x, pos.y), minDistanceBetweenObject))
            {
                Instantiate(treesPrefabs[Random.Range(0, treesPrefabs.Length)], pos, Quaternion.identity, objectsContainer);
            }
        }
        for (var i = 0; i < bushes.Count; i++)
        {
            var pos = bushes[i];
            if (Random.Range(0.0f, 1f) > 0.98f 
                && ground.HasTile(pos) 
                && PointInCircleByRadius(new Vector2(pos.x, pos.y), FillRadius)
                && CheckObjectNear(new Vector2(pos.x, pos.y), minDistanceBetweenObject))
            {
                Instantiate(bushesPrefabs[Random.Range(0, bushesPrefabs.Length)], pos, Quaternion.identity, objectsContainer);
            }
        }
        for (var i = 0; i < bushes.Count; i++)
        {
            var pos = bushes[i];
            if (Random.Range(0.0f, 1f) > 0.96f 
                && ground.HasTile(pos) 
                && PointInCircleByRadius(new Vector2(pos.x, pos.y), FillRadius)
                && CheckObjectNear(new Vector2(pos.x, pos.y), minDistanceBetweenObject))
            {
                Instantiate(stonesPrefabs[Random.Range(0, stonesPrefabs.Length)], pos, Quaternion.identity, objectsContainer);
            }
        }
    }
    
    List<Vector3Int>  CalcNoise(float min, float max, float offsetX = 0, float offsetY = 0)
    {
        List<Vector3Int> poses = new List<Vector3Int>();
        float y = 0.0F;
        while (y < mapSize)
        {
            float x = 0.0F;
            while (x < mapSize)
            {
                float xCoord = offsetX + x / mapSize * scaler;
                float yCoord = offsetY + y / mapSize * scaler;
                float height = Mathf.PerlinNoise(xCoord, yCoord);
                if (height >= min && height <= max)
                {
                    poses.Add(new Vector3Int((int)x, (int)y, 0));
                }
                x++;
            }
            y++;
        }
        return poses;
    }
}
