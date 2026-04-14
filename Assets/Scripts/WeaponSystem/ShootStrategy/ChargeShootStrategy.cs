// ChargeShootStrategy.cs
using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(fileName = "ChargeShoot", menuName = "Weapon/Shoot Strategy/Charge Shoot")]
    public class ChargeShootStrategy : ShootStrategyBase
    {
        [Tooltip("蓄满需要的时间")]
        public float MaxChargeTime = 1.5f;

        public override void Tick(WeaponController weapon, float deltaTime)
        {
            if (weapon.IsFireInputHeld)
            {
                // 累加蓄力时间
                weapon.ChargeTimer += deltaTime;
                weapon.ChargeTimer = Mathf.Clamp(weapon.ChargeTimer, 0, MaxChargeTime);
            }

            // 当玩家松开按键的那一帧
            if (weapon.WasFireInputReleasedThisFrame)
            {
                // 检查充能是否已满
                if (weapon.ChargeTimer >= MaxChargeTime)
                {
                    weapon.PerformShoot();
                }
                else
                {
                    Debug.Log("蓄力未满，取消发射");
                }
                weapon.ChargeTimer = 0f; // 无论是否发射，松开即清空蓄力
            }
        }
    }
}