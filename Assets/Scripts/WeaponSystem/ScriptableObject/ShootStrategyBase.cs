// ShootStrategyBase.cs
using UnityEngine;

namespace WeaponSystem
{
    /// <summary>
    /// 射击策略基类。定义了触发射击的规则。
    /// 绝对不要在此类中声明任何会随着时间变化的变量（如 timer），以保证 SO 是无状态的。
    /// </summary>
    public abstract class ShootStrategyBase : ScriptableObject
    {
        // 核心 Tick 方法，由 WeaponController 在 Update 中调用
        public abstract void Tick(WeaponController weapon, float deltaTime);
    }
}