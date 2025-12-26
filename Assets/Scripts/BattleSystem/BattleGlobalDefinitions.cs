using UnityEngine;

/// <summary>
/// 子弹的碰撞类型
/// </summary>
public enum BulletCollisionType
{
    Circle,
    Square
}

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

public enum CircleAngleType       //圆形发射器发弹点的朝向
{

    AllPlayer,             //全部瞄准玩家
    EnemyToPlayer,      //全部与敌人到玩家连线的方向相同
    AllObject,             //全部瞄准一个物体
    EnemyToObject,      //全部与敌人到一个物体连线的方向相同
    Universal,          //全部朝同一个世界坐标发射
    Uniform,             //均匀发射，呈圆形
    UniformPlayer        //均匀发射，呈圆形，扇形中心对准玩家
}


/// <summary>
/// 发射器属性
/// </summary>
public enum EmitterPropertyType
{
    //Emitter通用配置
    EmitterPosX, 
    EmitterPosY,
    EmitterDirection,

    //SingleEmitter专用配置
    Single_Angle,

    //Circle Pattern专用配置
    PatternCircle_Radius,
    PatternCircle_ShootDirection
}


/// <summary>
/// 事件变化方式（变化至、增加、减少）
/// </summary>
public enum EventModificationType
{
    ChangeTo,
    Add,
    Reduce
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
