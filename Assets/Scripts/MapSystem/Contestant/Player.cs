using UnityEngine;

public class Player : MonoBehaviour
{
    public bool canMoveOverTile = true;     //玩家在当前回合是否能跨地块移动
    public bool canMove = true;

    public int maxBattleNum = 1;
    public int battleNum = 1;       //玩家本回合还能进行战斗的次数

    public int maxSearchNum = 1;
    public int searchNum = 1;       //玩家本回合还能进行探索的次数

    private float power = 0;
    public FloatEventSO playerPowerChangeEvent;       //玩家战力变化事件

    //每回合开始时，重置玩家状态
    public void TurnStartResetState()
    {
        canMove = true;
        canMoveOverTile = true;

        battleNum = maxBattleNum;
        searchNum = maxSearchNum;
    }

    public bool CheckPlayerMove(Room targetRoom)
    {
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        Tile currentTile = currentRoom.parentTile;

        bool sameTile = (targetRoom.parentTile == currentTile);

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

    public bool CheckPlayerBattle(Room targetRoom)
    {
        return battleNum > 0;
    }

    public bool CheckPlayerSearch(Room targetRoom)
    {
        return searchNum > 0;
    }



    /// <summary>
    /// 更改玩家战力，供AI参考要不要挑战
    /// </summary>
    /// <param name="value">变化值，可以为负数</param>
    /// <returns>变化后的战力</returns>
    public float ChangePower(float value)
    {
        power += value;

        //通知修改UI
        playerPowerChangeEvent.RaiseEvent(power, this);
        //返回变化后的值
        return power;
    }
}
