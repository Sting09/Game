using System;
using System.Collections.Generic;
using UnityEngine;

public class CircleEmitterRuntime : EmitterRuntime
{
    CircleEmitterConfigSO circleConfig;

    //局部变量，记录随机值
    int pointNum;       //发弹点数量
    float emitterPosX, emitterPosY, emitterDir; //本波次的发弹器坐标、朝向
    float rangeValue, radiusValue, radiusDirectionValue, deltaAngle, startAngle;    //本波次的弧形数据
    Vector3 emitterPos, startPos, endPos;


    // 构造函数：初始化引用和缓存
    public CircleEmitterRuntime(CircleEmitterConfigSO emitter) : base(emitter)
    {
        circleConfig = emitter;

        pointNum = emitter.shootPointNum;

        Vector3 zero = Vector3.zero; 
        for (int i = 0; i < pointNum; i++)
        {
            posBuffer.Add(zero);
            dirBuffer.Add(0);
        }
    }

    public override void Shoot(Transform start, bool newWave = false)
    {
        if (newWave)
        {
            waveTimes++;
            timesInWave = 0;
        }

        //更新发弹点的坐标和方向
        InitialDirection(start, dirBuffer, newWave);   //先不考虑发弹点角度类型。初始化发弹点角度
        CalcluateActualPos(start, posBuffer, newWave);
        CalculateActualDir(start, dirBuffer, newWave);   //一定先算位置再算角度

        //每个发弹点发射一次pattern
        for (int i = 0; i < pointNum; i++)
        {
            pattern.ShootBullet(i, timesInWave, waveTimes, posBuffer[i], dirBuffer[i]);  //Single发射器只有一个发弹点，不用遍历数组
        }

        timesInWave++;
    }

    /// <summary>
    /// 不考虑AngleType，但考虑EmitterDirectionType，给dirBuffer填入发弹点位置相对圆心位置的角度
    /// </summary>
    /// <param name="dirBuffer"></param>
    /// <param name="newWave"></param>
    private void InitialDirection(Transform start, List<float> dirBuffer, bool newWave)
    {
        if(newWave)
        {
            rangeValue = circleConfig.shootPointRange.GetValue();
            radiusValue = circleConfig.radius.GetValue();
            radiusDirectionValue = circleConfig.radiusDirection.GetValue();


            startPos = start.position;
            endPos = BattleManager.Instance.GetPlayerPos();


            emitterDir = 0f;
            switch (circleConfig.emitterDirectionType)
            {
                case DirectionType.Player:
                    emitterDir = BattleManager.Instance.CalculateAngle(startPos, endPos);
                    break;
                case DirectionType.Object:
                    //暂不实现
                    break;
                case DirectionType.FixedValue:
                    //新一波开始时，重置发射器角度
                    emitterDir = circleConfig.emitterDirection.GetValue();
                    break;
                default:
                    break;
            }


            deltaAngle = rangeValue > 359.9f ? rangeValue / pointNum : rangeValue / (pointNum - 1);
            startAngle = rangeValue > 359.9f ?
                         radiusDirectionValue - 180f + deltaAngle / 2f : //360度，圆形
                         radiusDirectionValue - rangeValue / 2f;    //弧形
        }

        for (int i = 0; i < pointNum; i++)
        {
            dirBuffer[i] = emitterDir + startAngle + deltaAngle * i;
        }
    }



    /// <summary>
    /// 计算该发射器在发射时，每个发弹点的世界坐标
    /// </summary>
    /// <param name="posBuffer">存储世界坐标的数组</param>
    private void CalcluateActualPos(Transform start, List<Vector3> posBuffer, bool newWave)
    {
        if (posBuffer == null || posBuffer.Count != pointNum) { return; }

        //发射器位置，每波更新。计算结果保存在变量emitterPos
        #region
        if (newWave) //新一波才更新发射器数据
        {

            float x = 0f, y = 0f;

            switch (config.emitterPosTypeX)
            {
                case PositionType.Self:
                    x = start.position.x + circleConfig.emitterPosX.GetValue();
                    break;

                case PositionType.Player:
                    x = BattleManager.Instance.GetPlayerPos().x + circleConfig.emitterPosX.GetValue();
                    break;

                case PositionType.Object:
                    //暂不实现，有需要再实现
                    break;

                case PositionType.FixedValue:
                    //新一波开始时，重置发射器位置
                    x = circleConfig.emitterPosX.GetValue();
                    break;

                default:
                    break;
            }

            switch (config.emitterPosTypeY)
            {
                case PositionType.Self:
                    y = start.position.y + circleConfig.emitterPosY.GetValue();
                    break;

                case PositionType.Player:
                    y = BattleManager.Instance.GetPlayerPos().y + circleConfig.emitterPosY.GetValue();
                    break;

                case PositionType.Object:
                    //暂未实现，有需要再实现
                    break;

                case PositionType.FixedValue:
                    //新一波开始时，重置发射器位置
                    y = circleConfig.emitterPosY.GetValue();
                    break;

                default:
                    break;
            }

            emitterPos = new Vector3(x, y, 0);
            emitterPosX = x;
            emitterPosY = y;
        }
        #endregion

        //确定弧形位置偏移 + 随机位置偏移，并把三个值加在一起
        #region
        //第一步：如果共享随机数，先确定随机数
        Vector3 pointOffset = Vector3.zero;
        if (circleConfig.sameRandomness)
        {
            pointOffset = new(circleConfig.posOffsetX.GetValue(), circleConfig.posOffsetY.GetValue(), 0);
        }
        for (int i = 0; i < pointNum; i++)
        {
            posBuffer[i] = emitterPos;  //初始化为圆心位置
            posBuffer[i] += CalculatePosOffset(dirBuffer[i], radiusValue);  //加上弧形位置偏移
            posBuffer[i] += circleConfig.sameRandomness ? pointOffset : new(circleConfig.posOffsetX.GetValue(), circleConfig.posOffsetY.GetValue(), 0);
        }
        #endregion
    }


    /// <summary>
    /// 计算该发射器在发射时，每个发弹点基于世界坐标系的朝向
    /// </summary>
    /// <param name="dirBuffer">存储基于世界坐标系朝向的数组</param>
    private void CalculateActualDir(Transform start, List<float> dirBuffer, bool newWave)
    {
        if (posBuffer == null || posBuffer.Count != pointNum) { return; }

        switch (circleConfig.angleType)
        {
            case CircleAngleType.AllPlayer:
                break;
            case CircleAngleType.EnemyToPlayer:
                break;
            case CircleAngleType.AllObject:
                break;
            case CircleAngleType.EnemyToObject:
                break;
            case CircleAngleType.Universal:
                break;
            case CircleAngleType.Uniform:
                //偏转相同，就给每个发弹角度再额外加一个angle
                float offset = 0f;
                if (circleConfig.sameRandomness)
                {
                    offset = circleConfig.angle.GetValue();
                }
                for (int i = 0; i < pointNum; i++)
                {
                    dirBuffer[i] += circleConfig.sameRandomness ? offset : circleConfig.angle.GetValue();
                }
                break;
            case CircleAngleType.UniformPlayer:
                //偏转相同，但是正中心的点瞄准玩家，其他点偏转相同
                break;
            default:
                break;
        }
    }



    private Vector3 CalculatePosOffset(float direction, float distance)
    {
        // 将角度转换为弧度，顺时针为正，需要对传入的 direction 取反
        float angleRad = -direction * Mathf.Deg2Rad;

        float x = distance * Mathf.Cos(angleRad);
        float y = distance * Mathf.Sin(angleRad);

        return new Vector3(x, y, 0f);
    }
}
