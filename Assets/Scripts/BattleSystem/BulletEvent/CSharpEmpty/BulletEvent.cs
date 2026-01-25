using System;
using UnityEngine;

[Serializable]
public class BulletEvent
{
    public string description;
    public int repeatTimes;
    public BulletConditionSO condition;
    public BulletModifierSO modifier;
}
