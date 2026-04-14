// HoldShootStrategy.cs
using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(fileName = "HoldShoot", menuName = "Weapon/Shoot Strategy/Hold Shoot")]
    public class HoldShootStrategy : ShootStrategyBase
    {
        [Tooltip("两次发射的最小间隔")]
        public float FireCooldown = 0.2f;

        public override void Tick(WeaponController weapon, float deltaTime)
        {
            weapon.ActionTimer += deltaTime;

            // 如果玩家按住了射击键，且冷却完毕
            if (weapon.IsFireInputHeld && weapon.ActionTimer >= FireCooldown)
            {
                weapon.ActionTimer = 0f; // 连发时直接归零即可
                weapon.PerformShoot();
            }
        }
    }
}