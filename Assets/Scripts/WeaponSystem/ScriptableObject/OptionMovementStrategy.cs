using UnityEngine;

namespace WeaponSystem
{
    /// <summary>
    /// 子机运动策略。现在它是一个纯计算类，只负责计算并返回目标位置。
    /// </summary>
    public abstract class OptionMovementStrategy : ScriptableObject
    {
        /// <summary>
        /// 获取子机在该阵型下的理想目标位置
        /// </summary>
        /// <param name="option">子机引用（用于获取Index）</param>
        /// <param name="playerPos">玩家中心点</param>
        /// <param name="totalOptions">当前子机总数</param>
        /// <returns>计算出的目标世界坐标</returns>
        public abstract Vector3 GetTargetPosition(OptionController option, Vector3 playerPos, int totalOptions);
    }
}