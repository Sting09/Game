using System;
using UnityEngine;

public class Room : MonoBehaviour
{
    public RoomDirection direction;
    public bool isVisible = false;          //是否对玩家可见
    public Tile parentTile;                 //属于哪个地块
    private SpriteRenderer spriteRenderer;

    [Header("Room Info")]
    public bool haveEnemy = false;              //当前房间是否有对手
    public bool havePlayer = false;             //是否是玩家所在
    public int remainRewardNum;        //剩余奖励数目，数目越大越容易得到奖励
    public int alreadyRewardFailTimes = 0;  //搜索奖励已经失败了几次
    public AttackSO currentAttack;     //当前的AttackSO
    public int currentOpponentIndex = -1;    //当前的敌人index。具体信息在GameManager的opponentInfo
    public OpponentDataSO currentOpponent;      //当前敌人的SO
    //被玩家探索发现的设施

    private int minMediumLevel;
    private int minHighLevel;
    private Sprite lowMonsterIcon, mediumMonsterIcon, highMonsterIcon;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        minMediumLevel = GlobalSetting.Instance.globalVariable.minMediumLevel;
        minHighLevel = GlobalSetting.Instance.globalVariable.minHighLevel;
        lowMonsterIcon = GlobalSetting.Instance.globalVariable.lowLevelMonsterIcon;
        mediumMonsterIcon = GlobalSetting.Instance.globalVariable.mediumLevelMonsterIcon;
        highMonsterIcon = GlobalSetting.Instance.globalVariable.highLevelMonsterIcon;
    }


    private void OnMouseDown()
    {
        // 不在玩家行动阶段，直接返回
        bool isPlayerPhase = PhaseController.Instance.currentPhase == GamePhase.PlayerPhase;
        if(! isPlayerPhase ) { return; }

        bool playerCanMove = GameManager.Instance.player.CheckPlayerMove(this);
        bool playerCanBattle = GameManager.Instance.player.CheckPlayerBattle(this);
        bool playerCanSearch = GameManager.Instance.player.CheckPlayerSearch(this);
        bool canMoveTo = CheckMoveCondition();


        //尝试移动到一个没有敌人的房间。检查：房间状态、玩家状态、位置能否到达、是否有敌人
        if (isVisible && playerCanMove && canMoveTo && (!haveEnemy))
        {
            MovePlayerToHere();
        }
        //尝试与一个敌人发起战斗。检查：房间状态、玩家状态、位置是否相邻、是否有敌人
        else if (isVisible && playerCanBattle && canMoveTo && haveEnemy)
        {
            GameManager.Instance.PlayerBattle(this);
        }
        //尝试探索一个房间。检查：房间没有敌人、是玩家所在房间、玩家状态、房间状态、有奖励
        else if (isVisible && havePlayer && playerCanSearch && (!haveEnemy) && remainRewardNum > 0)
        {
            GameManager.Instance.PlayerSearch(this);
        }
    }

    


    // 执行移动逻辑
    private void MovePlayerToHere()
    {
        //如果跨地块，更新玩家状态，本回合不能再跨越地块
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        if(this.parentTile != currentTile)
        {
            if (!MapManager.Instance.test_unlimitedCrossBound)
            {
                GameManager.Instance.player.canMoveOverTile = false;
            }
        }

        //修改GameManager存储的玩家位置
        GameManager.Instance.playerCurrentRoom.havePlayer = false;
        GameManager.Instance.playerCurrentRoom = this;
        havePlayer = true;

        //修改玩家obj坐标
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
            spriteRenderer.enabled = true;
            //更新图标
            if(currentOpponentIndex >= 0)
            {
                //如果有对手
                spriteRenderer.sprite = currentOpponent.opponentIcon;
            }
            else if (currentOpponentIndex < 0 && currentAttack?.isOpponent == false)
            {
                //如果有野怪
                int level = currentAttack.level;
                if (level < minMediumLevel)     //小骷髅图标
                {
                    spriteRenderer.sprite = lowMonsterIcon;
                }
                else if(level < minHighLevel)    //精英骷髅图标
                {
                    spriteRenderer.sprite = mediumMonsterIcon;
                }
                else    //骷髅王图标
                {
                    spriteRenderer.sprite = highMonsterIcon;
                }
            }
            else if ( false )
            {
                //如果有设施
            }
            else
            {
                //都没有就用默认
                Sprite defaultRoomIcon = GlobalSetting.Instance.globalVariable.defaultRoomIcon;
                spriteRenderer.sprite = defaultRoomIcon;
            }
        }
        else
        {
            // 该房间对玩家不可见
            isVisible = false;
            spriteRenderer.enabled = false;
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
        if(MapManager.Instance.test_showFullMap) { return true; }

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
