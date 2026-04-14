// CharacterStat.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterStat
{
    public float BaseValue;

    private readonly List<StatModifier> statModifiers;
    private bool isDirty = true;
    private float cachedValue;

    public CharacterStat(float baseValue)
    {
        BaseValue = baseValue;
        statModifiers = new List<StatModifier>();
    }

    public float Value
    {
        get
        {
            if (isDirty)
            {
                cachedValue = CalculateFinalValue();
                isDirty = false;
            }
            return cachedValue;
        }
    }

    public void AddModifier(StatModifier mod)
    {
        isDirty = true;
        statModifiers.Add(mod);
        // 根据枚举的整型值排序，保证计算顺序：Flat -> PercentAdd -> PercentMult
        statModifiers.Sort((a, b) => a.Type.CompareTo(b.Type));
    }

    public bool RemoveAllModifiersFromSource(object source)
    {
        bool didRemove = false;
        // 倒序遍历删除，防止列表索引越界
        for (int i = statModifiers.Count - 1; i >= 0; i--)
        {
            if (statModifiers[i].Source == source)
            {
                isDirty = true;
                didRemove = true;
                statModifiers.RemoveAt(i);
            }
        }
        return didRemove;
    }

    private float CalculateFinalValue()
    {
        float finalValue = BaseValue;
        float sumPercentAdd = 0;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            StatModifier mod = statModifiers[i];

            if (mod.Type == StatModType.Flat)
            {
                finalValue += mod.Value;
            }
            else if (mod.Type == StatModType.PercentAdd)
            {
                sumPercentAdd += mod.Value;
                // 如果到了末尾，或者下一个不是加算百分比了，就把积攒的百分比结算掉
                if (i + 1 >= statModifiers.Count || statModifiers[i + 1].Type != StatModType.PercentAdd)
                {
                    finalValue *= (1.0f + sumPercentAdd);
                    sumPercentAdd = 0;
                }
            }
            else if (mod.Type == StatModType.PercentMult)
            {
                finalValue *= mod.Value;
            }
        }
        // 精度截断，防止浮点数误差
        return (float)Math.Round(finalValue, 4);
    }
}