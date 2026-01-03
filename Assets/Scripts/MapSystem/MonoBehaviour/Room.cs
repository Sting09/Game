using UnityEngine;

public class Room : MonoBehaviour
{
    public RoomDirection direction;
    public bool isVisible = false;          //是否对玩家可见
    public Tile parentTile;         //属于哪个地块
    private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }


    private void OnMouseDown()
    {
        // 1. 检查条件
        if (isVisible && CheckMoveCondition())
        {
            MovePlayerToHere();
        }
        else
        {
            //Debug.Log("条件不满足，无法移动至房间: " + gameObject.name);
        }
    }


    // 执行移动逻辑
    private void MovePlayerToHere()
    {
        //修改GameManager存储的玩家位置
        GameManager.Instance.playerCurrentRoom = this;

        //修改玩家坐标
        GameObject player = GameManager.Instance.playerObject;
        player.transform.position = this.gameObject.transform.position;

        //更新玩家视野
        GameManager.Instance.UpdatePlayerSight();
    }


    // 更新能不能被玩家看到
    public void UpdateSight()
    {
        if (CheckSightCondition())
        {
            // 该房间对玩家可见
            isVisible = true;
            spriteRenderer.color = new Color(255, 255, 255, 1f);
        }
        else
        {
            // 该房间对玩家不可见
            isVisible = false;
            spriteRenderer.color = new Color(255, 255, 255, 0f);
        }
    }

    // 检查玩家能否移动到目标格
    private bool CheckMoveCondition()
    {
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        bool sameTile = (this.parentTile == currentTile);
        int directionDelta = Mathf.Abs((int)currentRoom.direction - (int)direction);
        bool adjacentInternal = (directionDelta == 1 || directionDelta == 5) && sameTile;

        int indexDelta = parentTile.index - currentTile.index;
        int lineDelta = parentTile.line - currentTile.line;

        bool adjacentExternal = 
            //往左上角走
            (lineDelta == -1 && indexDelta == -1 && direction == RoomDirection.RightBottom && currentRoom.direction == RoomDirection.LeftTop) ||  
            //往左侧走
            (lineDelta == 0 && indexDelta == -1 && direction == RoomDirection.Right && currentRoom.direction == RoomDirection.Left) ||
            //往左下角走
            (lineDelta == 1 && indexDelta == 0 && direction == RoomDirection.RightTop && currentRoom.direction == RoomDirection.LeftBottom) ||
            //往右下角走
            (lineDelta ==1 && indexDelta==1 && direction==RoomDirection.LeftTop && currentRoom.direction==RoomDirection.RightBottom)||
            //往右侧走
            (lineDelta == 0 && indexDelta == 1 && direction == RoomDirection.Left && currentRoom.direction == RoomDirection.Right) ||
            //往右上角走
            (lineDelta == -1 && indexDelta == 0 && direction == RoomDirection.LeftBottom && currentRoom.direction == RoomDirection.RightTop);

        return adjacentExternal || adjacentInternal;
    }

    private bool CheckSightCondition()
    {
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        bool sameTile = (this.parentTile == currentTile);

        int indexDelta = parentTile.index - currentTile.index;
        int lineDelta = parentTile.line - currentTile.line;

        bool adjacentExternal =
            //往左上角走
            (lineDelta == -1 && indexDelta == -1 && direction == RoomDirection.RightBottom && currentRoom.direction == RoomDirection.LeftTop) ||
            //往左侧走
            (lineDelta == 0 && indexDelta == -1 && direction == RoomDirection.Right && currentRoom.direction == RoomDirection.Left) ||
            //往左下角走
            (lineDelta == 1 && indexDelta == 0 && direction == RoomDirection.RightTop && currentRoom.direction == RoomDirection.LeftBottom) ||
            //往右下角走
            (lineDelta == 1 && indexDelta == 1 && direction == RoomDirection.LeftTop && currentRoom.direction == RoomDirection.RightBottom) ||
            //往右侧走
            (lineDelta == 0 && indexDelta == 1 && direction == RoomDirection.Left && currentRoom.direction == RoomDirection.Right) ||
            //往右上角走
            (lineDelta == -1 && indexDelta == 0 && direction == RoomDirection.LeftBottom && currentRoom.direction == RoomDirection.RightTop);

        return adjacentExternal || sameTile;
    }
}
