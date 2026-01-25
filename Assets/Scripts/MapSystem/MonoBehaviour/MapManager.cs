using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class MapManager : SingletonMono<MapManager>
{
    // 序列化的列表，在Inspector里能看到它
    public List<Tile> allItems = new List<Tile>();
    // 默认Tile列表，玩家不能在此出生
    public List<Tile> fixedItems = new List<Tile>();
    // 所有地块的根物体
    public GameObject tilesRoot;
    // 运行时用的字典
    public Dictionary<Vector2Int, Tile> gridDictionary = new Dictionary<Vector2Int, Tile>();
    // 地块的预制体
    public GameObject tilePrefab;
    // 房间的预制体
    public GameObject roomPrefab;


    //----------------缩圈算法的缓存-------------------
    private Dictionary<Tile, int> _tileToIndex;
    private bool[] _selectedFlag;
    private bool[] _inFrontierFlag;
    private bool[] _visitedFlag;
    private int[] _frontierBuffer;
    private int _frontierCount;
    private int[] _bfsQueue;
    private int _queueHead, _queueTail;
    private int _cachedItemCount;

    // 六边形邻居偏移：左上、右上、左、右、左下、右下
    private static readonly int[] DLine = { -1, -1, 0, 0, 1, 1 };
    private static readonly int[] DIndex = { -1, 0, -1, 1, 0, 1 };






    //-----------------测试--------------------
    [Header("Test")]
    public bool test_showFullMap = false;
    public bool test_unlimitedCrossBound = false;


    protected override void Awake()
    {
        base.Awake();

        // 游戏开始时，allItems 已经有数据了！直接构建字典
        foreach (var item in allItems)
        {
            if (item == null) continue;

            item.ParseNameData();
            Vector2Int key = new Vector2Int(item.line, item.index);
            if (!gridDictionary.ContainsKey(key))
            {
                gridDictionary.Add(key, item);
            }
        }
    }





    //----------------------工具函数-----------------------
    /// <summary>
    /// 根据行和索引获取物体
    /// </summary>
    public GameObject GetObjectByCoordinate(int targetLine, int targetIndex)
    {
        Vector2Int key = new Vector2Int(targetLine, targetIndex);

        if (gridDictionary.ContainsKey(key))
        {
            return gridDictionary[key].gameObject;
        }
        else
        {
            Debug.LogWarning($"找不到位于 Line {targetLine}, Index {targetIndex} 的物体");
            return null;
        }
    }

    [ContextMenu("Generate Map Grid")]
    private void GenerateMapGrid()
    {
        RemoveMapGrid();

        int mapLevel = GlobalSetting.Instance.globalVariable.mapLevel;
        int middleIndex = mapLevel - 1;
        float deltaX = GlobalSetting.Instance.globalVariable.tileEdgeLength / 2f;
        float deltaY = deltaX * Mathf.Pow(3, 0.5f);

        for (int line = 0; line < mapLevel * 2 - 1; ++line)
        {
            int start = Mathf.Max(0, line - middleIndex);
            int length = mapLevel * 2 - 1 - Mathf.Abs(line - middleIndex);

            float x = (-length + 1) * deltaX;
            float y = (middleIndex - line) * deltaY;


            for (int index = start, num = 0; num < length; ++index, ++num)
            {
                //line是x坐标，index是y坐标

                GameObject tile = Instantiate(tilePrefab, tilesRoot.transform);
                tile.name = $"Tile_{line}_{index}";
                tile.transform.localPosition = new Vector3(x, y, 0);
                x += 2f * deltaX;
                Tile tileScript = tile.GetComponent<Tile>();
                tileScript.line = line;
                tileScript.index = index;

                tileScript.GenerateRooms();
            }
        }

        UpdateMapGrid();
    }

    [ContextMenu("Remove Map Grid")]
    private void RemoveMapGrid()
    {
        while (tilesRoot.transform.childCount > 0)
        {
            // 编辑器模式必须用 DestroyImmediate
            DestroyImmediate(tilesRoot.transform.GetChild(0).gameObject);
        }
    }

    [ContextMenu("Update Map Grid")]
    public void UpdateMapGrid()
    {
        if (tilesRoot == null)
        {
            Debug.LogError("请先在 Inspector 中赋值 Tiles Root！");
            return;
        }

        //------------ 找到19个地块 ------------------
        // 使用 targetRoot.GetComponentsInChildren
        allItems = tilesRoot.GetComponentsInChildren<Tile>(true).ToList();
        // 可选：按名字排序，看起来更整齐
        allItems = allItems.OrderBy(x => x.name).ToList();


    }

    [ContextMenu("Test Mode")]
    public void TestMode()
    {
        test_showFullMap = !test_showFullMap;
        test_unlimitedCrossBound = !test_unlimitedCrossBound;
    }

    public Tile GetRandomTile()
    {
        int randomIndex = UnityEngine.Random.Range(0, allItems.Count);
        return allItems[randomIndex];
    }






    //----------------------地块随机生成---------------------------
    /// <summary>
    /// 执行随机排布逻辑
    /// </summary>
    public void RandomizeTileData()
    {
        List<TileDataSO> a = GlobalSetting.Instance.gameDataConfig.randomTileDataList;
        List<TileDataSO> b = GlobalSetting.Instance.gameDataConfig.fixedTileDataList;

        // 1. 基本检查
        if (allItems.Count < b.Count)
        {
            Debug.LogError($"Tile数量 ({allItems.Count}) 少于必出的 b 列表数量 ({b.Count})，无法分配！");
            return;
        }

        if (a.Count == 0 && allItems.Count > b.Count)
        {
            Debug.LogWarning("列表 a 为空，但还有剩余空位，部分 Tile 将没有数据。");
        }

        // --- 第一步：构建待分配的数据池 ---
        // 修改点 1: 将池子的类型改为元组 (TileDataSO, bool)
        // Item1 存放数据，Item2 存放是否为 Fixed
        List<(TileDataSO data, bool isFixed)> tempPool = new List<(TileDataSO, bool)>();

        // A. 先把 b 列表全部加进去 (标记为 true)
        // tempPool.AddRange(b); // 原代码不能用了，因为类型不匹配，改为循环添加
        foreach (var item in b)
        {
            tempPool.Add((item, true));
        }

        // B. 计算还需要多少个来填满 allItems
        int slotsNeeded = allItems.Count - tempPool.Count;

        // C. 处理 a 列表的均匀分配
        if (slotsNeeded > 0 && a.Count > 0)
        {
            int fullLoops = slotsNeeded / a.Count;
            int remainder = slotsNeeded % a.Count;

            // 1. 填入完整的循环 (标记为 false)
            for (int i = 0; i < fullLoops; i++)
            {
                foreach (var item in a)
                {
                    tempPool.Add((item, false));
                }
            }

            // 2. 填入余数 (标记为 false)
            if (remainder > 0)
            {
                List<TileDataSO> tempA = new List<TileDataSO>(a);
                ShuffleList(tempA);

                for (int i = 0; i < remainder; i++)
                {
                    tempPool.Add((tempA[i], false));
                }
            }
        }

        // --- 第二步：将构建好的最终池子彻底打乱 ---
        // ShuffleList 支持泛型 T，所以这里不需要修改，它会自动处理元组类型
        ShuffleList(tempPool);

        // --- 第三步：赋值给 Tile ---
        int count = Mathf.Min(allItems.Count, tempPool.Count);

        for (int i = 0; i < count; i++)
        {
            if (allItems[i] != null)
            {
                // 修改点 2: 解构元组并分别赋值
                var poolItem = tempPool[i];

                allItems[i].tileData = poolItem.data;       // 赋值数据
                allItems[i].isFixedTile = poolItem.isFixed; // 赋值布尔值

                // 防止 data 为空（针对 a 列表为空的情况）
                if (poolItem.data != null)
                {
                    allItems[i].bgRenderer.sprite = poolItem.data.bgSprite;
                }
            }
        }
    }

    /// <summary>
    /// Fisher-Yates 洗牌算法 (通用工具函数) - 无需修改
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }





    //----------------------缩圈相关------------------------------
    private void EnsureBuffers()
    {
        int n = allItems.Count;

        if (_selectedFlag == null || _cachedItemCount != n)
        {
            _tileToIndex = new Dictionary<Tile, int>(n);
            _selectedFlag = new bool[n];
            _inFrontierFlag = new bool[n];
            _visitedFlag = new bool[n];
            _frontierBuffer = new int[n];
            _bfsQueue = new int[n];
            _cachedItemCount = n;
        }
        else
        {
            _tileToIndex.Clear();
        }

        for (int i = 0; i < n; i++)
            _tileToIndex[allItems[i]] = i;
    }

    /// <summary>
    /// 选择num个连通地块移除
    /// </summary>
    /// <param name="num"></param>
    /// <param name="resultBuffer"></param>
    public void GetRandomConnectedTilesNonAlloc(int num, List<Tile> resultBuffer)
    {
        resultBuffer.Clear();

        if (allItems == null || allItems.Count == 0 || num <= 0)
            return;

        EnsureBuffers();

        int n = allItems.Count;
        int maxSelect = n - 1;
        if (maxSelect <= 0)
            return;

        for (int target = Math.Min(num, maxSelect); target >= 1; target--)
        {
            if (TrySelectSet(target, resultBuffer))
                return;
        }
    }

    private bool TrySelectSet(int count, List<Tile> result)
    {
        int maxAttempts = Math.Max(50, allItems.Count * 3);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (TrySelectOnce(count, result))
                return true;
        }
        return false;
    }

    private bool TrySelectOnce(int count, List<Tile> result)
    {
        result.Clear();
        int n = allItems.Count;

        Array.Clear(_selectedFlag, 0, n);
        Array.Clear(_inFrontierFlag, 0, n);
        _frontierCount = 0;

        // 使用 UnityEngine.Random
        int startIdx = UnityEngine.Random.Range(0, n);
        result.Add(allItems[startIdx]);
        _selectedFlag[startIdx] = true;
        ExpandFrontier(startIdx);

        while (result.Count < count && _frontierCount > 0)
        {
            int pick = UnityEngine.Random.Range(0, _frontierCount);
            int nextIdx = _frontierBuffer[pick];

            _frontierBuffer[pick] = _frontierBuffer[--_frontierCount];
            _inFrontierFlag[nextIdx] = false;

            result.Add(allItems[nextIdx]);
            _selectedFlag[nextIdx] = true;
            ExpandFrontier(nextIdx);
        }

        if (result.Count < count)
            return false;

        return CheckRemainingConnected();
    }

    private void ExpandFrontier(int tileIdx)
    {
        Tile tile = allItems[tileIdx];
        int line = tile.line;
        int index = tile.index;

        for (int d = 0; d < 6; d++)
        {
            Vector2Int pos = new Vector2Int(line + DLine[d], index + DIndex[d]);

            if (gridDictionary.TryGetValue(pos, out Tile neighbor) &&
                _tileToIndex.TryGetValue(neighbor, out int neighborIdx))
            {
                if (!_selectedFlag[neighborIdx] && !_inFrontierFlag[neighborIdx])
                {
                    _frontierBuffer[_frontierCount++] = neighborIdx;
                    _inFrontierFlag[neighborIdx] = true;
                }
            }
        }
    }

    private bool CheckRemainingConnected()
    {
        int n = allItems.Count;

        int startIdx = -1;
        int remainingCount = 0;
        for (int i = 0; i < n; i++)
        {
            if (!_selectedFlag[i])
            {
                remainingCount++;
                if (startIdx < 0) startIdx = i;
            }
        }

        if (remainingCount <= 1)
            return true;

        Array.Clear(_visitedFlag, 0, n);
        _queueHead = 0;
        _queueTail = 0;

        _bfsQueue[_queueTail++] = startIdx;
        _visitedFlag[startIdx] = true;
        int visitedCount = 1;

        while (_queueHead < _queueTail)
        {
            int curIdx = _bfsQueue[_queueHead++];
            Tile current = allItems[curIdx];
            int line = current.line;
            int index = current.index;

            for (int d = 0; d < 6; d++)
            {
                Vector2Int pos = new Vector2Int(line + DLine[d], index + DIndex[d]);

                if (gridDictionary.TryGetValue(pos, out Tile neighbor) &&
                    _tileToIndex.TryGetValue(neighbor, out int neighborIdx))
                {
                    if (!_selectedFlag[neighborIdx] && !_visitedFlag[neighborIdx])
                    {
                        _visitedFlag[neighborIdx] = true;
                        _bfsQueue[_queueTail++] = neighborIdx;
                        visitedCount++;
                    }
                }
            }
        }

        return visitedCount == remainingCount;
    }
}