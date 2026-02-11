using System;
using UnityEngine;

// 不需要挂载到物体，纯数据类
public static class BattleContext
{
    // 1. 传进去的数据：房间信息、敌人信息
    public static Room roomData;
    public static AttackSO currentAttack;

    // 2. 传回来的逻辑：战斗结束后的回调函数
    // bool 参数代表：true=胜利，false=失败
    public static Action<bool> OnBattleResult;
}