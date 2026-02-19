using System;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Player Data")]
    public int retryTimesRemain;          //剩余重开次数
    public float currentHP;                 //当前HP值
    public float maxHP;                     //最大HP值
    public int bossDefeatedNum;             //已击败阎罗数

    [Header("Player State")]
    public bool canMoveOverTile = true;     //玩家在当前回合是否能跨地块移动
    public bool canMove = true;

    public int maxBattleNum = -1;   // -1 表示无限
    public int battleNum = -1;       //玩家本回合还能进行战斗的次数

    public int maxSearchNum = -1;       //-1 表示无限
    public int searchNum = -1;       //玩家本回合还能进行探索的次数
    public float currentSearchCost;

    public float power = 0;
    public float currentImpression = 0;

    [Header("Related Events")]
    public FloatEventSO playerPowerChangeEvent;       //玩家战力变化事件
    public FloatEventSO playerImpressionChangeEvent;       //玩家战力变化事件
    public FloatEventSO playerHPChangeEvent;            //玩家HP变化事件
    public FloatEventSO gameLoseEvent;
    public FloatEventSO playerDeathEvent;


    //每回合开始时，重置玩家状态
    public void TurnStartResetState()
    {
        canMove = true;
        canMoveOverTile = true;

        battleNum = maxBattleNum;
        searchNum = maxSearchNum;

        currentSearchCost = 0;
    }


    //重置HP等数值
    public void ResetBattleData()
    {
        currentHP = maxHP;
        playerHPChangeEvent.RaiseEvent(currentHP, this);
    }

    /// <summary>
    /// 检查玩家能否移动到目标房间
    /// </summary>
    /// <param name="targetRoom"></param>
    /// <returns></returns>
    public bool CheckPlayerMove(Room targetRoom)
    {
        Room currentRoom = GameManager.Instance.playerCurrentRoom;
        if(currentRoom == null || targetRoom == null) { return false; }

        Tile currentTile = currentRoom.parentTile;

        bool sameTile = (targetRoom.parentTile == currentTile);

        //如果是同地块移动，直接返回玩家能否移动
        if (sameTile)
        {
            return canMove;
        }
        //否则检验能否移动、检验能否跨区块
        else
        {
            return canMoveOverTile && canMove;
        }
    }


    /// <summary>
    /// 检查玩家能否战斗
    /// </summary>
    /// <param name="targetRoom"></param>
    /// <returns></returns>
    public bool CheckPlayerBattle(Room targetRoom)
    {
        return battleNum != 0;
    }


    /// <summary>
    /// 检查玩家能否探索
    /// </summary>
    /// <param name="targetRoom"></param>
    /// <returns></returns>
    public bool CheckPlayerSearch(Room targetRoom)
    {
        return searchNum != 0;
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


    /// <summary>
    /// 更改玩家战力，供AI参考要不要挑战
    /// </summary>
    /// <param name="value">变化值，可以为负数</param>
    /// <returns>变化后的战力</returns>
    public float ChangeImpression(float value)
    {
        currentImpression += value;

        //通知修改UI
        playerImpressionChangeEvent.RaiseEvent(currentImpression, this);
        //返回变化后的值
        return currentImpression;
    }


    public float PlayerTakeDamage(float value)
    {
        currentHP -= value;
        currentHP = Mathf.Max(currentHP, 0);

        playerHPChangeEvent.RaiseEvent(currentHP, this);

        // 玩家HP降至0，减少重试次数，触发玩家死亡事件
        if (currentHP <= 0)
        {
            ReduceRetryTimes(1);
            playerDeathEvent.RaiseEvent(currentHP, this);
        }

        return currentHP;
    }

    public float PlayerHeal(float value)
    {
        currentHP += value;
        currentHP = Mathf.Min(currentHP, maxHP);
        return currentHP;
    }


    public int ReduceRetryTimes(int num)
    {
        retryTimesRemain -= num;

        //玩家重试次数降为负数，触发游戏失败事件
        if(retryTimesRemain < 0)
        {
            gameLoseEvent.RaiseEvent(retryTimesRemain, this);
        }

        return retryTimesRemain;
    }


    public int AddBossDefeatedNum(int num)
    {
        bossDefeatedNum += num;
        return bossDefeatedNum;
    }
}
