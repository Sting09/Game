using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public int line;        //第几行？从0开始
    public int index;       //本行第几个？从0开始
    public TileState state;

    public GameObject roomPrefab;
    public GameObject roomsRoot;

    public List<RoomRef> rooms = new List<RoomRef>(6);

    public SpriteRenderer bgRenderer;

    public TileDataSO tileData;

    public bool isFixedTile;        //是否是固定刷新的地块（神居、混乱、禁地）

    private void Awake()
    {
        ParseNameData();        //获取line和index
        state = TileState.Normal;
    }

    /// <summary>
    /// 获取line和index
    /// </summary>
    public void ParseNameData()
    {
        // 名字格式严格为 "名字_Line_Index" (例如 Tile_1_2)
        string[] splitArray = gameObject.name.Split('_');

        // 为了安全，检查一下分割后的长度
        if (splitArray.Length >= 3)
        {
            int.TryParse(splitArray[splitArray.Length - 2], out line);
            int.TryParse(splitArray[splitArray.Length - 1], out index);
        }
        else
        {
            Debug.LogError($"物体 {gameObject.name} 的命名格式不正确，无法解析！");
        }
    }

    // 在编辑器不运行游戏时也能看到自动赋值，在Inspector组件 -> "Auto Fill Info"
    [ContextMenu("Auto Fill Info")]
    private void EditorAutoFill()
    {
        ParseNameData();
    }


    [ContextMenu("Generate Rooms")]
    public void GenerateRooms()
    {
        //清除当前的所有房间
        float edgeLength = GlobalSetting.Instance.globalVariable.tileEdgeLength / 3;
        rooms.Clear();

        while (roomsRoot.transform.childCount > 0)
        {
            DestroyImmediate(roomsRoot.transform.GetChild(0).gameObject);
        }

        for(int i = 0; i < 6; ++i)
        {
            GameObject room = Instantiate(roomPrefab, roomsRoot.transform);
            float angle = i * (-60f);
            room.name = $"Room_{i}";
            room.transform.localPosition = new Vector3( Mathf.Cos(angle * Mathf.Deg2Rad) * edgeLength, 
                                                        Mathf.Sin(angle * Mathf.Deg2Rad) * edgeLength, 0);

            rooms.Add(new RoomRef(room.GetComponent<Room>(), (RoomDirection)i));

            Room roomScript = room.GetComponent<Room>();
            roomScript.direction = (RoomDirection)i;
            roomScript.parentTile = this;

            roomScript.haveEnemy = false;
            roomScript.remainRewardNum = 6;
        }
    }


    public Room GetRandomRoom()
    {
        int randomIndex = Random.Range(0, rooms.Count);
        return rooms[randomIndex].roomObj;
    }
    

    /// <summary>
    /// 给第i个房间生成一个敌人
    /// </summary>
    /// <param name="roomIndex"></param>
    public void GenerateOneMonster(int roomIndex)
    {
        if (tileData.attackList == null || tileData.attackList.Count == 0)
        {
            Debug.Log("未配置该地块的敌人列表");
        }
        //获取该地块的一个随机attack
        AttackSO attack = tileData.attackList[Random.Range(0, tileData.attackList.Count)];

        rooms[roomIndex].roomObj.haveEnemy = true;
        rooms[roomIndex].roomObj.currentAttack = attack;
    }


    /// <summary>
    /// 即将消失时，闪红光
    /// </summary>
    public void DangerousLight()
    {
        bgRenderer.color = Color.red;
        state = TileState.WillShrink;
    }

    /// <summary>
    /// 地块消失
    /// </summary>
    public void TileShrink()
    {
        //遍历6个房间，房间不再能进入；敌人死亡、玩家死亡且游戏结束
        for(int i = 0; i < rooms.Count; i++)
        {
            //有玩家
            if (rooms[i].roomObj.havePlayer)
            {
                GameManager.Instance.PlayerDie();
            }
            //有敌人
            if (rooms[i].roomObj.currentOpponentIndex > 0)
            {
                GameManager.Instance.OpponentDie(rooms[i].roomObj.currentOpponentIndex);
            }
            rooms[i].roomObj.currentAttack = null;
            rooms[i].roomObj.currentOpponent = null;
            rooms[i].roomObj.currentOpponentIndex = -1;

            rooms[i].roomObj.isVisible = false;
        }

        this.gameObject.SetActive(false);
        state = TileState.AlreadyShrink;
    }
}
