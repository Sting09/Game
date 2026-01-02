using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapManager : SingletonMono<MapManager>
{
    // 序列化的列表，在Inspector里能看到它
    public List<Tile> allItems = new List<Tile>();
    // 所有地块的根物体
    public GameObject tilesRoot;
    // 运行时用的字典
    private Dictionary<Vector2Int, Tile> gridDictionary = new Dictionary<Vector2Int, Tile>();
    // 地块的预制体
    public GameObject tilePrefab;
    // 房间的预制体
    public GameObject roomPrefab;


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
}