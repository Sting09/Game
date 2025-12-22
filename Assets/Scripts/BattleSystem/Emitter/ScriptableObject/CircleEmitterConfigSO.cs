using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CircleEmitterConfigSO", menuName = "Battle System/Emitter/CircleEmitterConfigSO")]
public class CircleEmitterConfigSO : AbstractEmitterConfigSO
{
    [Header("Shoot Point Info")]
    [Tooltip("发弹点数量")]
    public int shootPointNum = 5;
    [Tooltip("这些发弹点组成多大的扇形角")]
    public RangedFloat shootPointRange = new(360, 0, 0);
    [Tooltip("扇形半径")]
    public RangedFloat radius = new(0, 0, 0);
    [Tooltip("扇形角朝向的方向（相对发射器朝向逆时针旋转，单位：度）。")]
    public RangedFloat radiusDirection = new(0, 0, 0);
    [Tooltip("发弹点的朝向类型")]
    public CircleAngleType angleType = CircleAngleType.Uniform;
    [Tooltip("发弹点的角度（依据angleType不同而意义不同）")]
    public RangedFloat angle = new(90, 0, 0);
    [Tooltip("每个发弹点X坐标相对发射器X坐标的位置偏移")]
    public RangedFloat posOffsetX = new(0, 0, 0);
    [Tooltip("每个发弹点Y坐标相对发射器Y坐标的位置偏移")]
    public RangedFloat posOffsetY = new(0, 0, 0);
    [Tooltip("每个发弹点共用相同的随机值")]
    public bool sameRandomness = true;

    public override EmitterRuntime CreateRuntime()
    {
        return new CircleEmitterRuntime(this);
    }

    // 返回所有发弹点相对于发射器坐标的角度
    public override void GetAllShootPointsDirection(List<float> outputBuffer)
    {
        // 处理一个发弹点时的特殊情况
        if (shootPointNum < 1)
        {
            Debug.Log("发弹点数量设置错误，至少为2");
            return;
        }

        
    }

    //返回所有发弹点相对于发射器坐标的位置偏移
    public override void GetAllShootPointsPosOffset(List<Vector3> outputBuffer)
    {
        // 处理一个发弹点时的特殊情况
        if (shootPointNum < 1)
        {
            Debug.Log("发弹点数量设置错误，至少为2");
            return;
        }
        /*
        // 确定弧形中心点坐标
        Vector3 centerOffset = new(centerPosOffsetX.GetValue(), centerPosOffsetY.GetValue(), 0);

        // 确定每个发弹点的角度，循环里和radius共同计算位置
        float rangeValue = shootPointRange.GetValue();
        float radiusValue = radius.GetValue();
        float radiusDirectionValue = radiusDirection.GetValue();

        float deltaAngle = rangeValue > 359.9f ? rangeValue / shootPointNum : rangeValue / (shootPointNum - 1);
        float startAngle = rangeValue > 359.9f ?
                           radiusDirectionValue - 180f + deltaAngle / 2f : //360度，圆形
                           radiusDirectionValue - rangeValue / 2f;    //弧形


        Vector3 pointOffset = Vector3.zero;
        if (sameRandomness)
        {
            pointOffset = new(posOffsetX.GetValue(), posOffsetY.GetValue(), 0);
        }

        for (int i = 0; i < shootPointNum; i++)
        {
            // 中心坐标偏移
            outputBuffer[i] += centerOffset;
            // 弧形坐标偏移
            outputBuffer[i] += CalculatePosOffset(startAngle + deltaAngle * i, radiusValue);
            // 发射点坐标偏移
            outputBuffer[i] += sameRandomness ? pointOffset : new(posOffsetX.GetValue(), posOffsetY.GetValue(), 0);
        }*/
    }
}
