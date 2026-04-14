using System;
using System.Collections.Generic;

// --- 1. 定义事件接口 ---
public interface IEvent { }

// --- 2. 具体的事件定义 (你可以根据需求无限扩展) ---
public struct PlayerDamagedEvent : IEvent
{
    public float DamageAmount;
    public int AttackerID; // 假设用ID追踪敌人
}

public struct EnemyKilledEvent : IEvent
{
    public int EnemyID;
    public UnityEngine.Vector3 DeathPosition;
}

public struct BulletFiredEvent : IEvent
{
    public int BulletID; // 用于你的BulletDOTSManager识别
    public bool IsPlayerBullet;
}

// --- 3. 泛型事件总线核心 ---
public static class EventBus<T> where T : IEvent
{
    // 使用委托存储所有的监听者
    private static Action<T> OnEvent;

    public static void Register(Action<T> listener)
    {
        OnEvent += listener;
    }

    public static void Unregister(Action<T> listener)
    {
        OnEvent -= listener;
    }

    public static void Raise(T gameEvent)
    {
        OnEvent?.Invoke(gameEvent);
    }
}

// --- 4. 全局便捷入口 (可选，只是为了少写点泛型括号) ---
public static class ItemEventBus
{
    public static void Publish<T>(T gameEvent) where T : IEvent
    {
        EventBus<T>.Raise(gameEvent);
    }
}


//--------------------------------------------------------
//-------------------使用示例-----------------------------
//--------------------------------------------------------
/*
// 触发事件: 在你的EnemyDOTSManager中，当敌人死亡时：
ItemEventBus.Publish(new EnemyKilledEvent { EnemyID = 101, DeathPosition = transform.position });


// 监听事件: 在需要回血的道具逻辑中：
EventBus<EnemyKilledEvent>.Register(OnEnemyKilled);
// ...
private void OnEnemyKilled(EnemyKilledEvent evt) { //执行回血逻辑  }
*/