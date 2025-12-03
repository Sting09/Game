#if False

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaseShooterParametersSO", menuName = "Battle/Shooter/BaseShooterParametersSO")]
public class BaseShooterParametersSO : ScriptableObject
{
    [Header("Bullet Info")]
    [Tooltip("子弹预制体的key，字典在BaseShooter组件中")]
    public string bulletPrefabKey = "default";
    [Tooltip("子弹参数，用于初始化对象池中的子弹")]
    public BaseBulletParametersSO bulletParameters;



    [Header("Time Info")]
    [Tooltip("弹幕开始多少秒后开始发射")]
    public float shootDealy = 2;
    [Tooltip("发射器持续射击多少秒")]
    public float shootDuration = 9999;



    [Header("Shoot Info")]
    [Tooltip("一次发射几条子弹")]
    public RangedInt shootWayNum = new(5, 0, 0);
    [Tooltip("一条子弹有几颗子弹")]
    public RangedInt shootBulletNumPerWay = new(3, 0, 0);
    [Tooltip("这些条子弹组成多大的扇形角")]
    public RangedFloat shootRange = new(120, 0, 0);
    [Tooltip("朝什么发射")]
    public DirectionType shootDirectionType = DirectionType.CertainValue;
    [Tooltip("扇形角朝向的方向（相对发射器朝向逆时针旋转，单位：度）。shootDirectionType为CertainValue时生效")]
    public RangedFloat shootDirection = new(0, 0, 0);
    [Tooltip("一条子弹中最慢子弹的速度。每条一颗时以此为准")]
    public RangedFloat minSpeedEachWay = new(3, 0, 0);
    [Tooltip("一条子弹中最快子弹的速度。每条一颗时此参数无效")]
    public RangedFloat maxSpeedEachWay = new(5, 0, 0);



    [Header("Shoot Time Info")]
    [Tooltip("一波发射几次")]
    public int shootTimesPerWave = 5;
    [Tooltip("每次发射间隔多少秒")]
    public float shootInterval;
    [Tooltip("每波间隔多少秒")]
    public float waveInterval;



    [Header("ShootPoint Info")]
    [Tooltip("发射子弹位置的X坐标")]
    public PositionType baseShootPointPosX = PositionType.Self;
    public float offsetShootPointPosX = 0;
    public float randomShootPointPosX = 0;
    [Tooltip("baseShootPosX选择CertainValue时才有效")]
    public float valueShootPointPosX = 0;
    [Tooltip("发射子弹位置的Y坐标")]
    public PositionType baseShootPointPosY = PositionType.Self;
    public float offsetShootPointPosY = 0;
    public float randomShootPointPosY = 0;
    [Tooltip("baseShootPosX选择CertainValue时才有效")]
    public float valueShootPointPosY = 0;



    public virtual List<Vector2> GetAllShootPointsPositionOffset()
    {
        List<Vector2> res = new List<Vector2> { new Vector2(0, 0) };
        return res;
    }

    public virtual List<float> GetAllShootPointsDirectionOffset()
    {
        List<float> res = new List<float>{ 0f };
        return res;
    }

    public virtual void Shoot(Vector3 basePosition, Vector3 baseAngle, bool newWave = false)
    {
        BulletManager.Instance.AddBullet(basePosition, minSpeedEachWay.GetRandomValue(), 0f);
    }

    public virtual void ShootOneTime(Vector3 pos, float centerAngle)
    {
        float startAngle = 0f, angleDelta = 0f, startSpeed = 0f, speedDelta = 0f;
        float runtimeRange = shootRange.GetRandomValue();
        int runtimeWayNum = shootWayNum.GetRandomValue();
        int runtimeBulletNum = shootBulletNumPerWay.GetRandomValue();
        float runtimeMinSpeed = minSpeedEachWay.GetRandomValue();
        float runtimeMaxSpeed = maxSpeedEachWay.GetRandomValue();
        float runtimeDirection = shootDirection.GetRandomValue();
        float runtimeRandomDirection = runtimeDirection - shootDirection.baseValue;

        startSpeed = runtimeMinSpeed;
        speedDelta = runtimeBulletNum > 1 ? (runtimeMaxSpeed - runtimeMinSpeed)/(runtimeBulletNum - 1) : 0f;

        if(runtimeRange >= 360)     //子弹发射一圈
        {
            if (runtimeWayNum == 1)
            {
                startAngle = centerAngle + runtimeDirection;
            }
            else if (runtimeWayNum > 1)
            {
                angleDelta = 360f / runtimeWayNum;
                startAngle = centerAngle - 180f + (angleDelta / 2f) + runtimeDirection;
            }
        }
        else if (runtimeRange > 0)      //子弹呈一个扇形
        {
            if(runtimeWayNum == 1) 
            {
                startAngle = centerAngle + runtimeDirection;

            }
            else if (runtimeWayNum > 1)
            {
                startAngle = centerAngle - runtimeRange / 2f + runtimeDirection;
                angleDelta = runtimeRange / (runtimeWayNum - 1);
            }
        }

        for (int lineNum =0; lineNum < runtimeWayNum; lineNum++)    //lineNum：顺时针数第几列
        {
            for(int orderInLine = 0; orderInLine < runtimeBulletNum; orderInLine++)
            //速度由慢到快这是本列第几颗子弹
            {
                BulletManager.Instance.AddBullet(pos, 
                                                 startSpeed + orderInLine * speedDelta, 
                                                 startAngle + lineNum * angleDelta);
            }
        }
        
    }
}

/*public enum PositionType
{
    Self,
    Player,
    Object,
    CertainValue
}

public enum DirectionType
{
    Player,
    EnemyAndPlayer, //发弹点和玩家的连线
    Object,         //暂未实现。如需使用，建议根据Tag寻找物体。
    CertainValue
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
        return baseValue + offsetValue + Random.Range(-randomRange, randomRange);
    }
}


[System.Serializable]
public struct RangedInt
{
    public int baseValue;   // 基本值
    public int offsetValue;
    public int randomRange; // 随机范围（±多少）

    public RangedInt(int baseValue, int offsetValue,  int randomRange)
    {
        this.baseValue = baseValue;
        this.offsetValue = offsetValue;
        this.randomRange = randomRange;
    }

    public int GetRandomValue()
    {
        return baseValue + offsetValue + Random.Range(-randomRange, randomRange+1);
    }
}*/
#endif