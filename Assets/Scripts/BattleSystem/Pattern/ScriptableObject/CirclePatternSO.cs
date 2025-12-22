using UnityEngine;
using static UnityEditor.PlayerSettings;

[CreateAssetMenu(fileName = "CirclePatternSO", menuName = "Battle System/Bullet Pattern/Circle")]
public class CirclePatternSO : ShootPatternSO
{
    [Header("Circle Pattern")]
    [TextArea(4, 24)]
    public string description;
    [Tooltip("一次发射几条子弹")]
    public RangedInt shootWays = new(5, 0, 0);
    [Tooltip("一条子弹有几颗子弹")]
    public RangedInt bulletsPerWay = new(3, 0, 0);
    [Tooltip("这些条子弹组成多大的扇形角")]
    public RangedFloat shootRange = new(120, 0, 0);
    [Tooltip("扇形初始半径")]
    public RangedFloat radius = new(0, 0, 0);
    [Tooltip("扇形角朝向的方向（相对发弹点朝向逆时针旋转，单位：度）。")]
    public RangedFloat radiusDirection = new(0, 0, 0);
    [Tooltip("发射朝向的方向（相对发弹点朝向逆时针旋转，单位：度）。")]
    public RangedFloat shootDirection = new(0, 0, 0);
    [Tooltip("一条子弹中最慢子弹的速度。每条一颗时以此为准")]
    public RangedFloat startBulletSpeed = new(3, 0, 0);
    [Tooltip("一条子弹中最快子弹的速度。每条一颗时此参数无效")]
    public RangedFloat endBulletSpeed = new(5, 0, 0);

    public override ShootPattern CreateRuntimePattern()
    {
        return new CircleShootPattern(this);
    }
}
