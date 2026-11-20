using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISpatialHashGridClient
{
    Vector2 Position { get; }
    Vector2 Size { get; }
    Dictionary<int, int> ListIndexByCellIndex { get; set; }
    int QueryID { get; set; }
}

public class SpatialHashGrid
{
    protected List<ISpatialHashGridClient>[] cells;
    protected Vector2[] bounds;
    protected Vector2Int dimensions;
    protected int queryIds;

    /// <summary>
    /// 按边界与网格维度初始化网格单元。
    /// </summary>
    public SpatialHashGrid(Vector2[] bounds, Vector2Int dimensions)
    {
        this.bounds = bounds;
        this.dimensions = dimensions;
        this.queryIds = 0;
        this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new List<ISpatialHashGridClient>();
    }

    /// <summary>
    /// 以指定位置为中心重建网格。
    /// </summary>
    public void Rebuild(Vector2 position)
    {
        Vector2 size = bounds[1] - bounds[0];
        Rebuild(new Vector2[] { position - size/2, position + size/2 }, dimensions);
    }

    /// <summary>
    /// 按新的边界与维度重建网格，并重新插入所有客户端。
    /// </summary>
    public void Rebuild(Vector2[] bounds, Vector2Int dimensions)
    {
        // 取出所有旧客户端
        HashSet<ISpatialHashGridClient> oldClients = new HashSet<ISpatialHashGridClient>();
        for (int i = 0; i < cells.Length; i++)
        {
            foreach (ISpatialHashGridClient client in cells[i])
            {
                oldClients.Add(client);
            }
        }

        // 重新初始化所有网格单元
        this.bounds = bounds;
        this.dimensions = dimensions;
        this.cells = new List<ISpatialHashGridClient>[dimensions.x * dimensions.y];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = new List<ISpatialHashGridClient>();

        // 重新插入旧客户端
        foreach (ISpatialHashGridClient oldClient in oldClients)
        {
            InsertClient(oldClient);
        }   
    }

    /// <summary>
    /// 查询与给定矩形区域重叠的客户端。
    /// </summary>
    public List<ISpatialHashGridClient> FindNearby(Vector2 position, Vector2 size)
    {
        Vector2Int i1 = GetCellIndex(position.x - size.x/2.0f, position.y - size.y/2.0f);
        Vector2Int i2 = GetCellIndex(position.x + size.x/2.0f, position.y + size.y/2.0f);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++;

        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                int cellIndex = CellToIndex(x, y);
    
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    if (client.QueryID != queryId)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    /// <summary>
    /// 查询圆形范围内的所有客户端。
    /// </summary>
    public List<ISpatialHashGridClient> FindNearbyInRadius(Vector2 position, float radius)
    {
        // 求出圆四个方向上的边界点所在单元下标
        int xMin = GetCellXIndex(position.x-radius);
        int xMax = GetCellXIndex(position.x+radius);
        int yMin = GetCellYIndex(position.y-radius);
        int yMax = GetCellYIndex(position.y+radius);

        List<ISpatialHashGridClient> nearbyClients = new List<ISpatialHashGridClient>();
        int queryId = queryIds++;

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                int cellIndex = CellToIndex(x, y);
    
                foreach (ISpatialHashGridClient client in cells[cellIndex])
                {
                    if (client.QueryID != queryId && Vector2.Distance(client.Position, position) < radius)
                    {
                        client.QueryID = queryId;
                        nearbyClients.Add(client);
                    }
                }
            }
        }

        return nearbyClients;
    }

    /// <summary>
    /// 查询圆形范围内距离最近的客户端。
    /// </summary>
    public ISpatialHashGridClient FindClosestInRadius(Vector2 position, float radius)
    {
        List<ISpatialHashGridClient> nearby = FindNearbyInRadius(position, radius);
        if (nearby.Count == 0) return null;
        int closestIdx = 0;
        float minDistance = Vector2.Distance(nearby[closestIdx].Position, position);
        for (int i = 1; i < nearby.Count; i++)
        {
            float distance = Vector2.Distance(nearby[i].Position, position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestIdx = i;
            }
        }
        return nearby[closestIdx];
    }

    // public ISpatialHashGridClient FindNearest(Vector2 position, Vector2 size)
    // {
    //     Vector2Int i1 = GetCellIndex(position.x, position.y);
    //     int cellIndex = CellToIndex(x, y);
    //     foreach (ISpatialHashGridClient client in cells[cellIndex])
    //     {
    //         if (client.QueryID != queryId)
    //         {
    //             client.QueryID = queryId;
    //             nearbyClients.Add(client);
    //         }
    //     }
    // }

    /// <summary>
    /// 把客户端插入其所在的网格单元。
    /// </summary>
    public void InsertClient(ISpatialHashGridClient client)
    {
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x/2.0f, client.Position.y - client.Size.y/2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x/2.0f, client.Position.y + client.Size.y/2.0f);
        
        client.ListIndexByCellIndex = new Dictionary<int, int>();
        
        for (int x = i1.x, xn = i2.x; x <= xn; x++)
        {
            for (int y = i1.y, yn = i2.y; y <= yn; y++)
            {
                // 把该客户端加入其所在网格单元的客户端列表中
                int cellIndex = CellToIndex(x, y);
                cells[cellIndex].Add(client);

                // 把单元信息记录到客户端上，供后续查询使用
                client.ListIndexByCellIndex[cellIndex] = cells[cellIndex].Count-1;
            }
        }
    }

    /// <summary>
    /// 客户端移动后更新其所在单元。
    /// </summary>
    public void UpdateClient(ISpatialHashGridClient client)
    {
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x/2.0f, client.Position.y - client.Size.y/2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x/2.0f, client.Position.y + client.Size.y/2.0f);

        int cellIndexMin = CellToIndex(i1.x, i1.y);
        int cellIndexMax = CellToIndex(i2.x, i2.y);

        if (client.ListIndexByCellIndex.ContainsKey(cellIndexMin) && client.ListIndexByCellIndex.ContainsKey(cellIndexMax))
            return;

        RemoveClient(client);
        InsertClient(client);
    }

    /// <summary>
    /// 把客户端从所在单元移除。
    /// </summary>
    public void RemoveClient(ISpatialHashGridClient client)
    {
        
        foreach (var indices in client.ListIndexByCellIndex)
        {
            int endIndex = cells[indices.Key].Count - 1;
            // 用列表末尾的客户端顶替要删除的客户端
            ISpatialHashGridClient endClient = cells[indices.Key][endIndex];
            if (endClient != client)
            {
                endClient.ListIndexByCellIndex[indices.Key] = indices.Value;
                cells[indices.Key][indices.Value] = endClient;
            }
            // 然后移除列表末尾元素
            cells[indices.Key].RemoveAt(endIndex);
        }
    }

    /// <summary>
    /// 判断客户端是否已接近网格边缘（需要重建网格）。
    /// </summary>
    public bool CloseToEdge(ISpatialHashGridClient client)
    {
        Vector2Int i1 = GetCellIndex(client.Position.x - client.Size.x/2.0f, client.Position.y - client.Size.y/2.0f);
        Vector2Int i2 = GetCellIndex(client.Position.x + client.Size.x/2.0f, client.Position.y + client.Size.y/2.0f);
        return i1.x == 0 || i1.y == 0 || i2.x == dimensions.x-1 || i2.y == dimensions.y-1;
    }

    /// <summary>
    /// 把世界坐标换算成网格下标。
    /// </summary>
    private Vector2Int GetCellIndex(float xPosition, float yPosition)
    {
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));

        int xIndex = Mathf.FloorToInt(u * (dimensions.x-1));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y-1));

        return new Vector2Int(xIndex, yIndex);
    }

    /// <summary>
    /// 按 X 坐标计算列下标。
    /// </summary>
    private int GetCellXIndex(float xPosition)
    {
        float u = Mathf.Clamp01((xPosition - bounds[0][0]) / (bounds[1][0] - bounds[0][0]));
        int xIndex = Mathf.FloorToInt(u * (dimensions.x-1));
        return xIndex;
    }

    /// <summary>
    /// 按 Y 坐标计算行下标。
    /// </summary>
    private int GetCellYIndex(float yPosition)
    {
        float v = Mathf.Clamp01((yPosition - bounds[0][1]) / (bounds[1][1] - bounds[0][1]));
        int yIndex = Mathf.FloorToInt(v * (dimensions.y-1));
        return yIndex;
    }

    /// <summary>
    /// 把二维网格下标换算成一维数组下标。
    /// </summary>
    private int CellToIndex(int x, int y)
    {
        return x + y * dimensions.x;
    }
}