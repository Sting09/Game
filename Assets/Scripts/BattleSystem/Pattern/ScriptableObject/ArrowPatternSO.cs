using UnityEngine;

[CreateAssetMenu(fileName = "ArrowPatternSO", menuName = "Battle System/Bullet Pattern/Arrow")]
public class ArrowPatternSO : ShootPatternSO
{
    [Header("Arrow Pattern")]
    [TextArea(4, 24)]
    public string description;
    [Tooltip("半边发射几条子弹，不算正中间（一共2n+1条子弹）")]
    public RangedInt shootWaysHalf = new(2, 0, 0);
    [Tooltip("一条子弹有几颗子弹")]
    public RangedInt bulletsPerWay = new(0, 0, 0);
    [Tooltip("正中心的子弹方向相对发弹点偏移多少度，顺时针为正")]
    public RangedFloat centerDirection = new(0, 0, 0);
    [Tooltip("发射的大于号形状，中间夹角多少度（大于180时为小于号）")]
    public RangedFloat edgeAngle = new(60, 0, 0);
    [Tooltip("最外侧两颗子弹分别与发弹点连线，夹角多少度（0-180）")]
    public RangedFloat internalAngle = new(60, 0, 0);
    [Tooltip("刚开始发射时，正中间的子弹和发弹点的距离")]
    public RangedFloat distance = new(0, 0, 0);
    [Tooltip("正中间的一条子弹中，速度最慢的一颗的速度（bulletPerWay为1时，以此为准）")]
    public RangedFloat centerMinSpeed = new(5, 0, 0);
    [Tooltip("正中间的一条子弹中，速度最慢的一颗的速度")]
    public RangedFloat centerMaxSpeed = new(7, 0, 0);

    public override ShootPattern CreateRuntimePattern(EmitterRuntime emitterRuntime)
    {
        return new ArrowShootPattern(this, emitterRuntime);
    }
}
