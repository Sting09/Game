using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SingleEmitterConfigSO", menuName = "Battle System/Emitter/SingleEmitterConfigSO")]
public class SingleEmitterConfigSO : AbstractEmitterConfigSO
{
    [Header("Shoot Point Info")]
    [Tooltip("发弹点的朝向类型")]
    public AngleType angleType = AngleType.Increment;
    [ShowIf("angleType", AngleType.Increment)]
    [Tooltip("发弹点基于发射器朝向的偏转角度")]
    public RangedFloat increment = new(0, 0, 0);
    [ShowIf("angleType", AngleType.FixedValue)]
    [Tooltip("发弹点基于发射器朝向的偏转角度")]
    public RangedFloat fixedValue = new(0, 0, 0);
    [ShowIf("angleType", AngleType.FixedValue)]
    [Tooltip("发弹点朝向的角度")]
    public RangedFloat angle = new(90, 0, 0);
    [Tooltip("发弹点X坐标相对发射器X坐标的位置偏移")]
    public RangedFloat posOffsetX = new(0, 0, 0);
    [Tooltip("发弹点Y坐标相对发射器Y坐标的位置偏移")]
    public RangedFloat posOffsetY = new(0, 0, 0);




    public override void GetAllShootPointsPosOffset(List<Vector3> outputBuffer)
    {
        Vector3 pointPos = new(posOffsetX.GetValue(), posOffsetY.GetValue(), 0);
        outputBuffer[0] += pointPos;
    }

    public override void GetAllShootPointsDirection(List<float> outputBuffer)
    {
        if(angleType == AngleType.Increment)
            //SingleEmitter中，角度增量模式加上发弹点朝向
            outputBuffer[0] += increment.GetValue();
        if (angleType == AngleType.FixedValue)
            //固定值模式直接等于发弹点朝向
            outputBuffer[0] = fixedValue.GetValue();
    }

    public override EmitterRuntime CreateRuntime()
    {
        return new SingleEmitterRuntime(this);
    }
}
