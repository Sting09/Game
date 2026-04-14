// PlayerStats.cs
using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    // 在编辑器里配置初始属性
    [Header("续航属性")]
    [Tooltip("生命上限")]
    public CharacterStat maxHP = new CharacterStat(100f);
    [Tooltip("伤害减免")]
    public CharacterStat defence = new CharacterStat(0f);

    [Header("射击属性")]
    [Tooltip("射击伤害")]
    public CharacterStat damage = new CharacterStat(10f);
    public CharacterStat fireRate = new CharacterStat(5f);
    public CharacterStat bulletSize = new CharacterStat(1f);

    [Header("其他属性")]
    public CharacterStat moveSpeed = new CharacterStat(8f);

    private CharacterStat[] statArray;

    private void Awake()
    {
        // 将单个配置映射到数组，实现O(1)的极速查询
        int count = Enum.GetValues(typeof(StatType)).Length;
        statArray = new CharacterStat[count];

        statArray[(int)StatType.MaxHP] = maxHP;
        statArray[(int)StatType.Damage] = damage;
        statArray[(int)StatType.FireRate] = fireRate;
        statArray[(int)StatType.MoveSpeed] = moveSpeed;
        statArray[(int)StatType.BulletSize] = bulletSize;
    }

    public float GetStat(StatType type)
    {
        return statArray[(int)type].Value;
    }

    public void AddModifier(StatType type, StatModifier modifier)
    {
        statArray[(int)type].AddModifier(modifier);
    }

    public void RemoveModifiersFromSource(StatType type, object source)
    {
        statArray[(int)type].RemoveAllModifiersFromSource(source);
    }
}