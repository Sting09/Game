using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AbstractEmitterConfigSO", menuName = "Battle System/Emitter/AbstractEmitterConfigSO")]
public abstract class AbstractEmitterConfigSO : ScriptableObject
{
    [Tooltip("发射子弹形状的描述")]
    [TextArea(4, 20)]
    public string description;
    [Tooltip("属于哪个弹幕DanmakuSO")]
    public DanmakuSO parentDanmaku;



    [Header("Bullet Info")]
    [Tooltip("子弹事件")]
    public List<BulletEvent> bulletEvents;




    [Header("Emitter Time Info")]
    [Tooltip("弹幕开始多少秒后开始发射")]
    public float shootDelay = 1f;
    [Tooltip("发射器持续射击多少秒。< 0 表示始终发射")]
    public float emitterDuration = -1f;



    [Header("Emitter Info")]
    [Tooltip("发射器X坐标类型")]
    public PositionType emitterPosTypeX = PositionType.Self;
    [Tooltip("发射器X坐标的值。前一个参数为FixedValue时生效")]
    public RangedFloat emitterPosX;
    [Tooltip("发射器Y坐标类型")]
    public PositionType emitterPosTypeY= PositionType.Self;
    [Tooltip("发射器Y坐标的值。前一个参数为FixedValue时生效")]
    public RangedFloat emitterPosY;
    [Tooltip("发射器的朝向类型")]
    public DirectionType emitterDirectionType = DirectionType.FixedValue;
    [ShowIf("emitterDirectionType", DirectionType.FixedValue)]
    [Tooltip("发射器的朝向。前一个参数为FixedValue时生效")]
    public RangedFloat emitterDirection = new(90, 0, 0);



    [Header("Shoot Pattern")]
    [Tooltip("发弹点每次发射子弹的样式")]
    public ShootPatternSO bulletPattern;


    [Header("Emitter Events")]
    public List<EmitterEventSO> emitterEvents;



    [Header("Shoot Time Info")]
    [Tooltip("一波发射几次")]
    public int shootTimesPerWave = 5;
    [Tooltip("每次发射间隔多少秒")]
    public float shootInterval = 0.5f;
    [Tooltip("每波间隔多少秒")]
    public float waveInterval = 2f;



    /// <summary>
    /// 返回所有发弹点相对于发射器坐标的位置偏移
    /// </summary>
    /// <param name="outputBuffer">存储结果的列表的引用</param>
    /// <returns></returns>
    public abstract void GetAllShootPointsPosOffset(List<Vector3> outputBuffer);

    /// <summary>
    /// 返回所有发弹点相对于发射器角度的偏转角度。角度制，正右为0，正下为90。
    /// </summary>
    /// <param name="outputBuffer">存储结果的列表的引用</param>
    /// <returns></returns>
    public abstract void GetAllShootPointsDirection(List<float> outputBuffer);

    /// <summary>
    /// 创建发射器的运行时
    /// </summary>
    /// <returns></returns>
    public abstract EmitterRuntime CreateRuntime();
}
