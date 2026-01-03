using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public int line;        //第几行？从0开始
    public int index;       //本行第几个？从0开始

    public GameObject roomPrefab;
    public GameObject roomsRoot;

    public List<RoomRef> rooms = new List<RoomRef>(6);

    private void Awake()
    {
        ParseNameData();        //获取line和index
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
        }
    }


    public Room GetRandomRoom()
    {
        int randomIndex = Random.Range(0, rooms.Count);
        return rooms[randomIndex].roomObj;
    }
}
