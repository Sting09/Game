using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(menuName = "Weapon/Option Strategy/Single Orbit")]
    public class OrbitMovementStrategy : OptionMovementStrategy
    {
        [Tooltip("环绕半径")]
        public float Radius = 2.0f;
        [Tooltip("环绕速度 (度/秒)")]
        public float OrbitSpeed = 90f;

        public override Vector3 GetTargetPosition(OptionController option, Vector3 playerPos, int totalOptions)
        {
            // 确保不会发生除以0的错误
            if (totalOptions <= 0) return playerPos;

            // 计算该子机在环上的角度 (均匀分布)
            float angleOffset = (360f / totalOptions) * option.Index;
            float currentAngle = (Time.time * OrbitSpeed) + angleOffset;

            // 计算出相对于玩家的偏移量
            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentAngle * Mathf.Deg2Rad),
                0) * Radius;

            return playerPos + offset;
        }
    }
}