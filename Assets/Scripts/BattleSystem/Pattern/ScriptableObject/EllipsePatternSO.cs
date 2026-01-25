using UnityEngine;

[CreateAssetMenu(fileName = "EllipsePatternSO", menuName = "Battle System/Bullet Pattern/Ellipse")]
public class EllipsePatternSO : ShootPatternSO
{
    [Header("Ellipse Pattern")]
    [TextArea(4, 24)]
    public string description;
    [Tooltip("一次发射几条子弹（需要为4的倍数。只取基本值，不取随机值）")]
    public RangedInt shootWays = new(8, 0, 0);
    [Tooltip("一条子弹有几颗子弹（暂时为1，改也没有用）")]
    public RangedInt bulletsPerWay = new(1, 0, 0);
    [Tooltip("这些条子弹组成多大的扇形角（暂时为360，改也没有用）")]
    public RangedFloat shootRange = new(360, 0, 0);
    [Tooltip("最长边初始半径")]
    public RangedFloat radius = new(0, 0, 0);
    [Tooltip("扇形角朝向的方向（相对发弹点朝向逆时针旋转，单位：度）。（暂时没有用）")]
    public RangedFloat radiusDirection = new(0, 0, 0);
    [Tooltip("发射朝向的方向（相对发弹点朝向逆时针旋转，单位：度）。")]
    public RangedFloat shootDirection = new(0, 0, 0);
    [Tooltip("一圈子弹中最慢子弹的速度。")]
    public RangedFloat minBulletSpeed = new(3, 0, 0);
    [Tooltip("一圈子弹中最快子弹的速度。")]
    public RangedFloat maxBulletSpeed = new(5, 0, 0);
    [Tooltip("typeA为角度均匀分布；typeB为周长均匀分割")]
    public bool typeB = true;

    public override ShootPattern CreateRuntimePattern(EmitterRuntime emitterRuntime)
    {
        return new EllipseShootPattern(this, emitterRuntime);
    }
}
