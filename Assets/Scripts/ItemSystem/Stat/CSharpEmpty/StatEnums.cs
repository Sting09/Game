// StatEnums.cs
public enum StatType
{
    MaxHP = 0,
    Damage = 1,
    FireRate = 2,    // 射速
    MoveSpeed = 3,   // 移速
    BulletSize = 4   // 子弹大小
}

public enum StatModType
{
    Flat = 100,        // 固定值加成 (优先计算)
    PercentAdd = 200,  // 百分比加算
    PercentMult = 300  // 百分比乘算/独立乘区 (最后计算)
}