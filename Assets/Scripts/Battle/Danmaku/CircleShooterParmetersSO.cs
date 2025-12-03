#if false
using NUnit.Framework;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

[CreateAssetMenu(fileName = "CircleShooterParmetersSO", menuName = "Battle/Shooter/CircleShooterParmetersSO")]
public class CircleShooterParmetersSO : BaseShooterParametersSO
{
    [Header("CircleShootPoint Info")]
    [Tooltip("有几个发弹点")]
    public int shootPointNum = 8;
    [Tooltip("发弹点的朝向类型，基于世界坐标")]
    public ShootPointDirectionType shootPointType = ShootPointDirectionType.Dispersed;
    [Tooltip("上一个选项为Consistent或RandomConsistent时生效，发弹点朝向同一角度")]
    public RangedFloat shootPointUniversalDirection = new(90f, 0, 0);
    [Tooltip("上一个选项为Custom时生效，自由输入各个发弹点的朝向")]
    public List<RangedFloat> customShootPointDirectionList;
    [Tooltip("发弹点组成多大的扇形角")]
    public RangedFloat shootPointRange = new(360f, 0, 0);
    [Tooltip("分布在多大半径的扇形角上")]
    public RangedFloat shootPointRadius = new(5f, 0, 0);
    [Tooltip("扇形角中心朝向什么角度。正下为90")]
    public RangedFloat shootPointRadiusDirection = new(90f, 0, 0);
    [Tooltip("发弹点之间的距离是否不均匀")]
    public bool inconsistentInterval = false;
    [Tooltip("上一个选项勾选时生效。输入发弹点之间的间隔。列表大小为发弹点数，相加之和小于扇形角。" +
        "数值为第i个发弹点相较于第i-1个（第0个元素为相较于起始点）逆时针旋转的角度")]
    public List<RangedFloat> customShootPointIntervalList;


    [Header("This Wave CircleShootPoint Info")]
    [Tooltip("一圈发弹点的圆心的基础位置，每波更新一次")]
    public RangedFloat centerPointX;
    public RangedFloat centerPointY;


    [Header("Runtime")]
    [Tooltip("辅助变量，不要修改")]
    public float runtimeCenterPosX = 0f;
    public float runtimeCenterPosY = 0f;
    public float runtimeRadius = 0f;
    public float runtimeRange = 0f;
    public float runtimeRadiusDirection = 0f;

    /// <summary>
    /// 计算各个发射器相较于发射点中心的位置偏移
    /// </summary>
    /// <returns></returns>
    public override List<Vector2> GetAllShootPointsPositionOffset()
    {
        List<Vector2> res = new();

        float radius = runtimeRadius;
        float range = runtimeRange;
        float angleStart;
        if (shootPointType == ShootPointDirectionType.AimToPlayer)
        {
            Vector3 startPos = new Vector3(runtimeCenterPosX, runtimeCenterPosY, 0);
            Vector3 endPos = BulletManager.Instance.GetPlayerPosition();
            angleStart = BulletManager.Instance.CalculateAngle(startPos, endPos) - range / 2f;
        }
        else
        {
            angleStart = runtimeRadiusDirection - range / 2f;
        }
                           
                           

        if (inconsistentInterval)
        {
            float total = angleStart;
            //玩家自己填写间隔，间隔不一致的情况
            for (int i = 0; i < shootPointNum; ++i)
            {
                total += customShootPointIntervalList[i].GetRandomValue();
                res.Add(CalculatePosition(radius, total));
            }
        }
        else
        {
            //间隔一致的情况。绝大多数情况下适用

            if (shootPointNum == 1)
            {
                //一个发弹点时特殊处理，直接返回半径方向即可
                res.Add(CalculatePosition(radius, runtimeRadiusDirection));
                return res;
            }

            //发射360度时要特殊处理
            float delta = range / (range == 360f ? shootPointNum : shootPointNum - 1);
            if (range == 360f)
            {
                angleStart += (delta / 2f);
            }

            for (int i = 0; i < shootPointNum; ++i)
            {
                res.Add(CalculatePosition(radius, angleStart + delta * i));
            }
        }
        return res;
    }

    /// <summary>
    /// 计算非瞄准玩家状态下，各个发射器的角度
    /// </summary>
    /// <returns></returns>
    public override List<float> GetAllShootPointsDirectionOffset()
    {
        List<float> res = new();

        switch (shootPointType)
        {
            //开花弹，均匀朝向圆弧的各个弧度
            case ShootPointDirectionType .Dispersed:
                if(shootPointNum == 1)
                {
                    res.Add(runtimeRadiusDirection);
                }
                else
                {
                    float start, delta;
                    if(runtimeRange >= 360)
                    {
                        delta = runtimeRange / shootPointNum;
                        start = runtimeRadiusDirection - runtimeRange / 2f + delta / 2f;
                    }
                    else
                    {
                        start = runtimeRadiusDirection - runtimeRange / 2f;
                        delta = runtimeRange / (shootPointNum - 1);
                    }
                    for(int i = 0;i < shootPointNum; ++i)
                    {
                        res.Add(start + delta * i);
                    }
                }
                break;
            // 全部朝一个方向，再各附加一个随机值
            case ShootPointDirectionType.RandomConsistent:
                for (int i = 0; i < shootPointNum; ++i)
                {
                    float eachValue = shootPointUniversalDirection.GetRandomValue();
                    res.Add(eachValue);
                }
                break;
            // 朝向完全一致
            case ShootPointDirectionType.Consistent:
                float value = shootPointUniversalDirection.GetRandomValue();
                for (int i = 0; i < shootPointNum; ++i)
                {
                    res.Add(value);
                }
                break;
            // 朝向圆心到玩家的方向
            case ShootPointDirectionType.ConsistentPlayer:
                Vector3 centerPos = new Vector3(runtimeCenterPosX, runtimeCenterPosY, 0);
                Vector3 playerPos = BulletManager.Instance.GetPlayerPosition();
                float direction = BulletManager.Instance.CalculateAngle(centerPos, playerPos);
                for (int i = 0; i < shootPointNum; ++i)
                {
                    res.Add(direction);
                }
                break;
            // 有一个发弹点朝向玩家
            case ShootPointDirectionType.AimToPlayer:
                Vector3 startPos = new Vector3(runtimeCenterPosX, runtimeCenterPosY, 0);
                Vector3 endPos = BulletManager.Instance.GetPlayerPosition();
                float centerDirection = BulletManager.Instance.CalculateAngle(startPos, endPos);
                if (shootPointNum == 1)
                {
                    res.Add(centerDirection);
                }
                else
                {
                    float start, delta;
                    if (runtimeRange >= 360)
                    {
                        delta = runtimeRange / shootPointNum;
                        start = centerDirection - runtimeRange / 2f + delta / 2f;
                    }
                    else
                    {
                        start = centerDirection - runtimeRange / 2f;
                        delta = runtimeRange / (shootPointNum - 1);
                    }
                    for (int i = 0; i < shootPointNum; ++i)
                    {
                        res.Add(start + delta * i);
                    }
                }
                break;
            // 定制化
            case ShootPointDirectionType.Custom:
                for (int i = 0; i < shootPointNum; ++i)
                {
                    res.Add(customShootPointDirectionList[i].GetRandomValue());
                }
                break;
        }
        return res;
    }

    public override void Shoot(Vector3 basePosition, Vector3 anglePosition, bool newWave = false)
    {
        //basePosition：圆形发射器的圆心
        //runtimeCenterPosition：运行时本波子弹的实际圆心
        //pos：一个发弹点本次发射子弹的实际位置
        //anglePosition：shootDirectionType 为 Player 时，值为玩家坐标；否则为（0 0 0）


        //先结合随机值，计算发射器的运行时圆心
        if (newWave)
        {
            //更新本波子弹的基准位置
            runtimeCenterPosX = basePosition.x + centerPointX.GetRandomValue();
            runtimeCenterPosY = basePosition.y + centerPointY.GetRandomValue();
            runtimeRadius = shootPointRadius.GetRandomValue();
            runtimeRange = shootPointRange.GetRandomValue();
            runtimeRadiusDirection = shootPointRadiusDirection.GetRandomValue();
        }

        List<Vector2> posOffset = GetAllShootPointsPositionOffset();
        List<float> angleOffset = GetAllShootPointsDirectionOffset();

        //运行时本波子弹的实际圆心
        Vector2 runtimeCenterPosition = new Vector2(runtimeCenterPosX, runtimeCenterPosY);

        for (int i = 0;i < shootPointNum; ++i)
        {
            //计算第i个发弹点的位置
            float x = runtimeCenterPosX + posOffset[i].x;
            float y = runtimeCenterPosY + posOffset[i].y;
            Vector3 pos = new Vector3(x, y, 0);         //这个发弹点本次发射子弹的实际位置


            //计算第i个发弹点的角度
            float angle = 0f;
            if (shootDirectionType == DirectionType.Player)
            {
                Vector3 playerPos = BulletManager.Instance.GetPlayerPosition();
                angle = BulletManager.Instance.CalculateAngle(pos, playerPos);
            }
            else
            {
                angle = angleOffset[i];
            }

            ShootOneTime(pos, angle);
        }
    }

    /// <summary>
    /// 给定半径和偏转角，返回相对于点(0, 0)的位置偏移
    /// </summary>
    /// <param name="radius">半径</param>
    /// <param name="angle">角度。右侧为0，正下90</param>
    /// <returns></returns>
    private Vector2 CalculatePosition(float radius, float angle)
    {
        // 传统的数学/C# MathF/Unity Mathf.Sin/Cos 函数默认是逆时针为正方向。
        // 要求顺时针旋转 angle 度。
        float standardAngle = -angle;

        // C# 的三角函数（Mathf.Sin/Cos）接受弧度 (Radians) 作为输入。
        float radians = standardAngle * Mathf.Deg2Rad;

        float x = radius * Mathf.Cos(radians);
        float y = radius * Mathf.Sin(radians);

        return new Vector2(x, y);
    }
}

public enum ShootPointDirectionType
{
    Dispersed,      //均匀分散，开花
    RandomConsistent, //朝向一致，再各附加一个不同的随机值
    Consistent,     //朝向完全一致，有随机也是随机同样的值
    ConsistentPlayer,   //朝向圆心到玩家的方向
    AimToPlayer,    //正中心的发弹点朝向玩家
    Custom          //自由编辑各发弹点之间的角度
}


#endif