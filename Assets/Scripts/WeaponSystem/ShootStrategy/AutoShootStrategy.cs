// AutoShootStrategy.cs
using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(fileName = "AutoShoot", menuName = "Weapon/Shoot Strategy/Auto Shoot")]
    public class AutoShootStrategy : ShootStrategyBase
    {
        [Tooltip("每隔多少秒发射一次")]
        public float Interval = 1f;

        public override void Tick(WeaponController weapon, float deltaTime)
        {
            weapon.ActionTimer += deltaTime;
            if (weapon.ActionTimer >= Interval)
            {
                // 减去 Interval 而不是归零，可以保证长期射击的帧率稳定性，不吞时间
                weapon.ActionTimer -= Interval;
                weapon.PerformShoot();
            }
        }
    }
}