using System.Collections.Generic;
using UnityEngine;

public class SingleEmitterRuntime : EmitterRuntime
{
    SingleEmitterConfigSO singleConfig;
    float emitterPosX, emitterPosY, emitterDir;


    // 构造函数：初始化引用和缓存
    public SingleEmitterRuntime(SingleEmitterConfigSO emitter) : base(emitter)
    {
        singleConfig = emitter;
        
        dirBuffer.Add(0);
        emitterPosX = 0;
        emitterPosY = 0;
        emitterDir = 0;
    }

    public override void Shoot(Transform start, bool newWave = false)
    {
        if (newWave)
        {
            waveTimes++;
            timesInWave = 0;
        }
        CalcluateActualPos(start, posBuffer, newWave);
        CalculateActualDir(start, dirBuffer, newWave);   //一定先算位置再算角度
        pattern.ShootBullet(0, timesInWave, waveTimes, posBuffer[0], dirBuffer[0]);  //Single发射器只有一个发弹点，不用遍历数组
        timesInWave++;
    }

    /// <summary>
    /// 计算该发射器在发射时，每个发弹点的世界坐标
    /// </summary>
    /// <param name="posBuffer">存储世界坐标的数组</param>
    private void CalcluateActualPos(Transform start, List<Vector3> posBuffer, bool newWave = false)
    {
        if(posBuffer == null || posBuffer.Count > 1 ) { return; }
        if (posBuffer.Count == 0) { posBuffer.Add(Vector3.zero); }

        //Single位置计算：发射器位置 + 发弹点偏移

        //发射器位置
        #region
        if(newWave) //新一波才更新发射器数据
        {

            float x = 0f, y = 0f;

            switch (config.emitterPosTypeX)
            {
                case PositionType.Self:
                    x = start.position.x + singleConfig.emitterPosX.GetValue();
                    break;

                case PositionType.Player:
                    x = BattleManager.Instance.GetPlayerPos().x + singleConfig.emitterPosX.GetValue();
                    break;

                case PositionType.Object:
                    //暂不实现，有需要再实现
                    break;

                case PositionType.FixedValue:
                    //新一波开始时，重置发射器位置
                    x = singleConfig.emitterPosX.GetValue();
                    break;

                default:
                    break;
            }

            switch (config.emitterPosTypeY)
            {
                case PositionType.Self:
                    y = start.position.y + singleConfig.emitterPosY.GetValue();
                    break;

                case PositionType.Player:
                    y = BattleManager.Instance.GetPlayerPos().y + singleConfig.emitterPosY.GetValue();
                    break;

                case PositionType.Object:
                    //暂未实现，有需要再实现
                    break;

                case PositionType.FixedValue:
                    //新一波开始时，重置发射器位置
                    y = singleConfig.emitterPosY.GetValue();
                    break;

                default:
                    break;
            }

            posBuffer[0] = new Vector3(x, y, 0);
            emitterPosX = x;
            emitterPosY = y;
        }
        else
        {
            posBuffer[0] = new Vector3(emitterPosX, emitterPosY, 0);
        }
        #endregion

        //发弹点偏移
        singleConfig.GetAllShootPointsPosOffset(posBuffer);
    }


    /// <summary>
    /// 计算该发射器在发射时，每个发弹点基于世界坐标系的朝向
    /// </summary>
    /// <param name="dirBuffer">存储基于世界坐标系朝向的数组</param>
    private void CalculateActualDir(Transform start, List<float> dirBuffer, bool newWave = false)
    {
        if (dirBuffer == null || dirBuffer.Count > 1) { return; }
        if (dirBuffer.Count == 0) { dirBuffer.Add(0f); }

        Vector3 startPos = start.position;
        Vector3 endPos = BattleManager.Instance.GetPlayerPos();

        //Single角度计算：敌人角度始终为0 + 发射器角度 + 发弹点角度（可能覆盖发射器角度）

        //发射器角度
        if(newWave)
        {
            switch (singleConfig.emitterDirectionType)
            {
                case DirectionType.Player:
                    dirBuffer[0] = BattleManager.Instance.CalculateAngle(startPos, endPos);
                    break;
                case DirectionType.Object:
                    //暂不实现
                    break;
                case DirectionType.FixedValue:
                    //新一波开始时，重置发射器角度
                    dirBuffer[0] = singleConfig.emitterDirection.GetValue();
                    break;
                default:
                    break;
            }
            emitterDir = dirBuffer[0];
        }
        else
        {
            dirBuffer[0] = emitterDir;
        }

        //发弹点角度
        switch (singleConfig.angleType)
        {
            case AngleType.Player:
                dirBuffer[0] = BattleManager.Instance.CalculateAngle(posBuffer[0], endPos);
                break;
            case AngleType.EnemyToPlayer:
                dirBuffer[0] = BattleManager.Instance.CalculateAngle(startPos, endPos);
                break;
            case AngleType.Object:
                //极少使用，暂不实现
                break;
            case AngleType.EnemyToObject:
                //极少使用，暂不实现
                break;
            case AngleType.Increment:
                singleConfig.GetAllShootPointsDirection(dirBuffer);
                break;
            case AngleType.FixedValue:
                singleConfig.GetAllShootPointsDirection(dirBuffer);
                break;
            default:
                break;
        }
    }
}
