// WeaponData.cs
using UnityEngine;

namespace WeaponSystem
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapon/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string WeaponName = "Default Weapon";

        [Header("触发策略")]
        public ShootStrategyBase ShootStrategy;

        [Header("基础属性")]
        public float BaseDamage = 10f;
        public int BaseOptionCount = 4; // 基础子机数量
        // 可以在这里继续添加武器的基础外观、图标等配置
    }
}