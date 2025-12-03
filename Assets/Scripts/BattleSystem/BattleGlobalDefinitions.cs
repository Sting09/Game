using UnityEngine;

/// <summary>
/// 发射器的位置
/// </summary>
public enum PositionType    //发射器的位置
{
    Self,           //发射器坐标
    Player,         //玩家坐标
    Object,         //场景中的物体。暂未实现，有需要再实现
    FixedValue    //手填坐标
}

/// <summary>
/// 发射器的朝向
/// </summary>
public enum DirectionType   //发射器的朝向
{
    Player,         //朝向玩家
    Object,         //朝向场景中的物体。暂未实现，有需要再实现
    FixedValue    //朝向固定角度，手填数值
}

/// <summary>
/// 发弹点的朝向
/// </summary>
public enum AngleType       //发弹点的朝向
{
    Player,             //瞄准玩家
    EnemyToPlayer,      //与敌人到玩家连线的方向相同
    Object,             //瞄准一个物体
    EnemyToObject,      //与敌人到一个物体连线的方向相同
    Increment,          //在发射器朝向的基础上加一个增量
    FixedValue          //与发射器无关，朝向固定方向
}

[System.Serializable]
public struct RangedFloat
{
    public float baseValue;   // 基本值
    public float offsetValue;   //固定偏移值
    public float randomRange; // 随机范围（±多少）

    public RangedFloat(float baseValue, float offsetVale, float randomRange)
    {
        this.baseValue = baseValue;
        this.offsetValue = offsetVale;
        this.randomRange = randomRange;
    }

    public float GetRandomValue()
    {
        return offsetValue + Random.Range(-randomRange, randomRange);
    }

    public float GetValue()
    {
        return baseValue + offsetValue + Random.Range(-randomRange, randomRange);
    }
}


[System.Serializable]
public struct RangedInt
{
    public int baseValue;   // 基本值
    public int offsetValue; // 固定大小的offset
    public int randomRange; // 随机范围（±多少）

    public RangedInt(int baseValue, int offsetValue, int randomRange)
    {
        this.baseValue = baseValue;
        this.offsetValue = offsetValue;
        this.randomRange = randomRange;
    }

    public int GetRandomValue()
    {
        return offsetValue + Random.Range(-randomRange, randomRange + 1);
    }
    public int GetValue()
    {
        return baseValue + offsetValue + Random.Range(-randomRange, randomRange);
    }
}


public struct ShootPattern
{
    public float range;             //这些条子弹组成扇形角的大小
    public int ways;                //一共几条子弹
    public int bulletsPerWay;       //每条子弹几颗子弹
    public float minSpeed;          //一条子弹中最慢子弹的速度（只有一颗时以此为准）
    public float maxSpeed;          //一条子弹中最快子弹的速度


    public ShootPattern UpdatePattern(AbstractEmitterConfigSO config)
    {
        range = config.shootRange.GetValue();
        ways = config.shootWays.GetValue();
        bulletsPerWay = config.bulletsPerWay.GetValue();
        minSpeed = config.minBulletSpeed.GetValue();
        maxSpeed = config.maxBulletSpeed.GetValue();

        return this;
    }
}
