using System;
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
        // 检查条件:房间能点 且 当前是玩家阶段 且 玩家还能移动 且 是玩家能移动到的房间
        bool isPlayerPhase = PhaseController.Instance.currentPhase == GamePhase.PlayerPhase;
        bool playerCanMove = CheckPlayerMove();
        bool canMoveTo = CheckMoveCondition();

        if (isVisible && isPlayerPhase && playerCanMove && canMoveTo)
        {
            MovePlayerToHere();
        }
        else
        {
            //Debug.Log("条件不满足，无法移动至房间: " + gameObject.name);
        }
    }

    private bool CheckPlayerMove()
    {
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        bool sameTile = (this.parentTile == currentTile);

        //如果是同地块移动，直接返回玩家能否移动
        if (sameTile)
        {
            return GameManager.Instance.player.canMove;
        }
        //否则检验能否移动、检验能否跨区块
        else
        {
            return GameManager.Instance.player.canMoveOverTile && GameManager.Instance.player.canMove;
        }
    }


    // 执行移动逻辑
    private void MovePlayerToHere()
    {
        //如果跨地块，更新玩家状态
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        if(this.parentTile != currentTile)
        {
            GameManager.Instance.player.canMoveOverTile = false;
        }

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
        if (sameTile) return true;

        int indexDelta = parentTile.index - currentTile.index;
        int lineDelta = parentTile.line - currentTile.line;

        bool adjacentExternal =
            //往左上角走
            (lineDelta == -1 && indexDelta == -1 && (direction == RoomDirection.Right || direction == RoomDirection.RightBottom ||
            direction == RoomDirection.LeftBottom) && currentRoom.direction == RoomDirection.LeftTop) ||
            //往左侧走
            (lineDelta == 0 && indexDelta == -1 && (direction == RoomDirection.Right || direction == RoomDirection.RightBottom ||
            direction == RoomDirection.RightTop) && currentRoom.direction == RoomDirection.Left) ||
            //往左下角走
            (lineDelta == 1 && indexDelta == 0 && (direction == RoomDirection.RightTop || direction == RoomDirection.LeftTop ||
            direction == RoomDirection.Right) && currentRoom.direction == RoomDirection.LeftBottom) ||
            //往右下角走
            (lineDelta == 1 && indexDelta == 1 && (direction == RoomDirection.LeftTop || direction == RoomDirection.RightTop ||
            direction == RoomDirection.Left) && currentRoom.direction == RoomDirection.RightBottom) ||
            //往右侧走
            (lineDelta == 0 && indexDelta == 1 && (direction == RoomDirection.Left || direction == RoomDirection.LeftTop ||
            direction == RoomDirection.LeftBottom) && currentRoom.direction == RoomDirection.Right) ||
            //往右上角走
            (lineDelta == -1 && indexDelta == 0 && (direction == RoomDirection.LeftBottom || direction == RoomDirection.RightBottom ||
            direction == RoomDirection.Left) && currentRoom.direction == RoomDirection.RightTop);
        if (adjacentExternal) return true;

        bool twoStepExternal =
            //玩家在左下，房间在左侧
            (lineDelta == 0 && indexDelta == -1 && currentRoom.direction == RoomDirection.LeftBottom && direction == RoomDirection.Right) ||
            //玩家在左下，房间在右下
            (lineDelta == 1 && indexDelta == 1 && currentRoom.direction == RoomDirection.LeftBottom && direction == RoomDirection.LeftTop) ||
            //玩家在右下，房间在左下
            (lineDelta == 1 && indexDelta == 0 && currentRoom.direction == RoomDirection.RightBottom && direction == RoomDirection.RightTop) ||
            //玩家在右下，房间在右侧
            (lineDelta == 0 && indexDelta == 1 && currentRoom.direction == RoomDirection.RightBottom && direction == RoomDirection.Left) ||
            //玩家在右侧，房间在右上
            (lineDelta == -1 && indexDelta == 0 && currentRoom.direction == RoomDirection.Right && direction == RoomDirection.LeftBottom) ||
            //玩家在右侧，房间在右下
            (lineDelta == 1 && indexDelta == 1 && currentRoom.direction == RoomDirection.Right && direction == RoomDirection.LeftTop) ||
            //玩家在右上，房间在左上
            (lineDelta == -1 && indexDelta == -1 && currentRoom.direction == RoomDirection.RightTop && direction == RoomDirection.RightBottom) ||
            //玩家在右上，房间在右侧
            (lineDelta == 0 && indexDelta == 1 && currentRoom.direction == RoomDirection.RightTop && direction == RoomDirection.Left) ||
            //玩家在左上，房间在右上
            (lineDelta == -1 && indexDelta == 0 && currentRoom.direction == RoomDirection.LeftTop && direction == RoomDirection.LeftBottom) ||
            //玩家在左上，房间在左侧
            (lineDelta == 0 && indexDelta == -1 && currentRoom.direction == RoomDirection.LeftTop && direction == RoomDirection.Right) ||
            //玩家在左侧，房间在左上
            (lineDelta == -1 && indexDelta == -1 && currentRoom.direction == RoomDirection.Left && direction == RoomDirection.RightBottom) ||
            //玩家在左侧，房间在左下
            (lineDelta == 1 && indexDelta == 0 && currentRoom.direction == RoomDirection.Left && direction == RoomDirection.RightTop);
        return twoStepExternal;
    }
}
