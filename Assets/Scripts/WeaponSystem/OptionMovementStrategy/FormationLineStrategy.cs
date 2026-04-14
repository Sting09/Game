using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(menuName = "Weapon/Option Strategy/Formation Line")]
    public class FormationLineStrategy : OptionMovementStrategy
    {
        [Tooltip("子机之间的间距")]
        public float Spacing = 1.0f;
        [Tooltip("阵型相对于玩家的整体偏移 (例如 Y 设为 1，则在玩家前方)")]
        public Vector3 FormationOffset = new Vector3(0, 1.5f, 0);

        public override Vector3 GetTargetPosition(OptionController option, Vector3 playerPos, int totalOptions)
        {
            if (totalOptions <= 0) return playerPos;

            // 计算居中偏移。例如 4 个子机，总宽度是 3 * Spacing，居中意味着向左偏移一半
            float totalWidth = (totalOptions - 1) * Spacing;
            float startX = -totalWidth / 2f;

            // 计算当前子机的局部坐标
            Vector3 localPos = new Vector3(startX + option.Index * Spacing, 0, 0);

            // 加上阵型的整体偏移和玩家的位置
            return playerPos + FormationOffset + localPos;
        }
    }
}