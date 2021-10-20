using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Voronoi2;

public class MapGeneration : MonoBehaviour
{
    [Header("Preferences")]
    [SerializeField] private Tile point;
    [SerializeField] private Tile linePoint;
    [SerializeField] private Tile groundTile;
    [SerializeField] private Tile groundTile2;
    [SerializeField] private Tilemap ground;

    private Voronoi voroObject = new Voronoi(0.1f);
    private List<GraphEdge> ge;
    private List<Vector2> sites;

    [Header("Settings")]
    [Range(50, 1000)]
    [SerializeField] int siteCount = 200;
    [Range(50, 1000)]
    [SerializeField] int mapSize = 512;
    [Range(0, 0.7f)]
    [SerializeField] float cutValue = 0.2f; // Отсечение краев

    private void Start()
    {
        GenerateGround();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateGround();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.LogError("ERORR - P");
            Debug.Break();
        }
    }
    private void GenerateGround()
    {
        ground.ClearAllTiles();
        spreadPoints();
        FillTilesOnGround();
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

    private void spreadPoints()
    {
        sites = new List<Vector2>();
        int seed = Random.Range(0, 999999999);
        Random.InitState(seed);
        // TODO: Generate long 9 999 999 999 999 из двух интов

        Debug.Log("SEED: " + seed);

        for (int i = 0; i < siteCount; i++)
        {
            sites.Add(new Vector2((float)(Random.value * mapSize), (float)(Random.value * mapSize)));
        }

        ge = MakeVoronoiGraph(sites, mapSize, mapSize);
    }

    private void FillTilesOnGround()
    {
        // Отсекаем края
        int geStart = (int)(ge.Count * cutValue);
        int sitesStart = (int)(sites.Count * cutValue);

        int geEnd = ge.Count - geStart;
        int sitesEnd = sites.Count - geStart;

        // Края
        // Можно закоментить*
        for (int x = 0; x < mapSize; x++)
        {
            ground.SetTile(new Vector3Int(x, 0, 0), linePoint);
            ground.SetTile(new Vector3Int(x, mapSize, 0), linePoint);
        }
        for (int y = 0; y < mapSize; y++)
        {
            ground.SetTile(new Vector3Int(0, y, 0), linePoint);
            ground.SetTile(new Vector3Int(mapSize, y, 0), linePoint);
        }

        // Рисуем
        for (int i = geStart; i < geEnd; i++)
        {
            var p1 = new Vector3Int((int)ge[i].x1, (int)ge[i].y1, 0);
            var p2 = new Vector3Int((int)ge[i].x2, (int)ge[i].y2, 0);

            // Линии между пересечениями 
            TileLine(p1, p2, linePoint);

            // Точки пересечения
            ground.SetTile(new Vector3Int((int)ge[i].x1, (int)ge[i].y1, 0), point);
            ground.SetTile(new Vector3Int((int)ge[i].x2, (int)ge[i].y2, 0), point);
        }

        for (int i = sitesStart; i < sitesEnd; i++)
        {
            // Центры
            var p = new Vector3Int((int)(sites[i].x - 1.5f), (int)(sites[i].y - 1.5f), 0);
            ground.SetTile(p, point);
            
            FillArea(p, i % 2 == 0 ? groundTile : groundTile2);
        }
    }

    private void TileLine(Vector3Int p1, Vector3Int p2, Tile tile)
    {
        while (p1 != p2)
        {
            ground.SetTile(p1, tile);

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

    private void FillArea(Vector3Int centroid, Tile tile)
    {
        Vector3Int left = centroid + Vector3Int.left;
    }

    private Vector3Int v()
    {
        return Vector3Int.zero;
    }
    

    private void OnDrawGizmos()
    {
        if (ge == null) return;
        for (int i = 0; i < ge.Count; i++)
        {
            var p1 = new Vector3Int((int)ge[i].x1, (int)ge[i].y1, 0);
            var p2 = new Vector3Int((int)ge[i].x2, (int)ge[i].y2, 0);
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
}
