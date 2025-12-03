#if False

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SingleShooterParametersSO", menuName = "Battle/Shooter/SingleShooterParameterSO")]
public class SingleShooterParameterSO : BaseShooterParametersSO
{
    [Header("This Wave SingleShootPoint Info")]
    [Tooltip("发弹点的朝向。每波更新一次。")]
    public DirectionType baseShootPointDirection = DirectionType.CertainValue;
    public float offsetShootPointDirection = 0;
    public float randomShootPointDirection = 0;
    [Tooltip("baseShootPosX选择CertainValue时固定不变；其他选项时不用理")]
    public float valueShootPointDirection = 90f;
    [Tooltip("发弹点的基础位置，每波更新一次")]
    public RangedFloat singlePointX;
    public RangedFloat singlePointY;


    [Header("Runtime")]
    [Tooltip("辅助变量，不要修改")]
    public float runtimeDirection = 0f;
    public float runtimePosX = 0f;
    public float runtimePosY = 0f;


    public override List<Vector2> GetAllShootPointsPositionOffset()
    {
        List<Vector2> res = new();
        float posX = offsetShootPointPosX + Random.Range(-randomShootPointPosX, randomShootPointPosX);
        float posY = offsetShootPointPosY + Random.Range(-randomShootPointPosY, randomShootPointPosY);
        res.Add(new Vector2(posX, posY));
        return res;
    }

    public override List<float> GetAllShootPointsDirectionOffset()
    {
        float offset = offsetShootPointDirection + Random.Range(-randomShootPointDirection, randomShootPointDirection);
        offset += baseShootPointDirection == DirectionType.CertainValue ? valueShootPointDirection : 0;
        List<float> res = new() { offset };
        
        return res;
    }

    public override void Shoot(Vector3 basePosition, Vector3 anglePosition, bool newWave = false)
    {
        List<Vector2> posOffset = GetAllShootPointsPositionOffset();
        List<float> angleOffset = GetAllShootPointsDirectionOffset();
        Vector3 runtimePos = Vector3.zero;

        for(int i = 0; i < posOffset.Count; i++)
        {
            //----  发弹点位置计算  ----
            if(newWave)
            {
                //更新本波子弹的基准位置
                runtimePosX = basePosition.x + singlePointX.GetRandomValue();
                runtimePosY = basePosition.y + singlePointY.GetRandomValue();
            }
            float x = runtimePosX + posOffset[i].x;
            float y = runtimePosY + posOffset[i].y;
            Vector3 pos = new Vector3(x, y, 0);         //本次发射子弹的实际位置
            runtimePos = new Vector3(runtimePosX, runtimePosY, 0);  //本波子弹的基准位置


            //------------------------------------------

            //----  角度计算  ----
            float angle = 0f;
            //shootDirectionType为自机狙时，直接狙
            if(shootDirectionType == DirectionType.Player)
            {
                angle = BulletManager.Instance.CalculateAngle(pos, anglePosition);
            }
            //瞄准物体，直接狙击
            else if(shootDirectionType == DirectionType.Object)
            {

            }
            else
            {
                //根据baseShootPointDirection确定初始角度
                if(newWave)
                {
                    switch (baseShootPointDirection)
                    {
                        case (DirectionType.EnemyAndPlayer):
                            //发弹点朝向敌人和玩家的连线，传递玩家坐标
                            Vector3 playerPos = BulletManager.Instance.GetPlayerPosition();
                            angle = BulletManager.Instance.CalculateAngle(basePosition, playerPos);
                            runtimeDirection = angle + angleOffset[i];
                            break;
                        case (DirectionType.Player):
                            //发弹点朝向玩家，传递玩家坐标
                            playerPos = BulletManager.Instance.GetPlayerPosition();
                            angle = BulletManager.Instance.CalculateAngle(runtimePos, playerPos);
                            runtimeDirection = angle + angleOffset[i];
                            break;
                        case (DirectionType.CertainValue):
                            //发弹点朝向固定角度
                            runtimeDirection = angleOffset[i];
                            break;
                        case (DirectionType.Object):
                            //暂未实现
                            break;
                    }
                }
            }

            ShootOneTime(pos, angle);
        }
    }

}

#endif