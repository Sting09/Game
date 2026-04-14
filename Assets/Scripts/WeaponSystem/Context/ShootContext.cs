// ShootContext.cs
using UnityEngine;

namespace WeaponSystem
{
    /// <summary>
    /// 射击上下文。使用 struct 避免分配堆内存 (无 GC)。
    /// 包含了单次射击所需的所有环境信息。
    /// </summary>
    public struct ShootContext
    {
        public Vector3 PlayerPosition;
        public Vector2 AimDirection;    // 玩家当前的瞄准方向
        public float DamageMultiplier;  // 伤害倍率（经过道具修饰后的最终值）
        public int OptionCount;         // 当前生效的子机数量

        // 如果策划需要充能武器，还可以把充能比例传下去，用来决定子弹大小等
        public float ChargeNormalized;
    }
}