using UnityEngine;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class CircleShootPattern : ShootPattern
{
    private CirclePatternSO circleConfig;

    public float range;             //这些条子弹组成扇形角的大小
    public float radius;            //每条子弹初始位置组成弧形的半径
    public float radiusDirection;   //每条子弹初始位置组成弧形的半径方向
    public float shootDirection;    //基于半径方向，再偏转一个角度
    public int ways;                //一共几条子弹

    public int bulletsPerWay;       //每条子弹几颗子弹
    public float startSpeed;          //一条子弹中最慢子弹的速度（只有一颗时以此为准）
    public float endSpeed;          //一条子弹中最快子弹的速度

    public CircleShootPattern(CirclePatternSO config, EmitterRuntime runtime) : base(config, runtime)
    {
        circleConfig = config;
    }

    public override void UpdatePattern()
    {
        range = circleConfig.shootRange.GetValue();

        //目前只能让事件改变radius，其他要改也好改，纯体力活，用到再写
        radius = circleConfig.radius.GetValue();
        radius = ownerEmitterRuntime.GetPropertyValue(EmitterPropertyType.PatternCircle_Radius, radius);

        radiusDirection = circleConfig.radiusDirection.GetValue();

        shootDirection = circleConfig.shootDirection.GetValue();
        shootDirection = ownerEmitterRuntime.GetPropertyValue(EmitterPropertyType.PatternCircle_ShootDirection, shootDirection);

        ways = circleConfig.shootWays.GetValue();
        bulletsPerWay = circleConfig.bulletsPerWay.GetValue();
        startSpeed = circleConfig.startBulletSpeed.GetValue();
        startSpeed = ownerEmitterRuntime.GetPropertyValue(EmitterPropertyType.PatternCircle_MinSpeed, startSpeed);
        endSpeed = circleConfig.endBulletSpeed.GetValue();
    }

    public override void ShootBullet(BulletRuntimeInfo info, Vector3 pos, float dir)
    {
        UpdatePattern();        //每次发射更新pattern。未来可能设计成每波更新
        dir += radiusDirection;

        float startAngle = 0f, angleDelta = 0f;

        float startSpeed = this.startSpeed;
        float speedDelta = bulletsPerWay > 1 ? (endSpeed - this.startSpeed) / (bulletsPerWay - 1) : 0f;

        if (ways == 1)
        {
            startAngle = dir;
        }
        else
        {
            if (range >= 360f)     //子弹发射一圈
            {
                angleDelta = 360f / ways;
                startAngle = dir - 180f + (angleDelta / 2f);
            }
            else if (range > 0)      //子弹呈一个扇形
            {
                angleDelta = range / (ways - 1);
                startAngle = dir - range / 2f;
            }
        }

        for (int lineNum = 0; lineNum < ways; lineNum++)    //lineNum：顺时针数第几列
        {
            //pos是发弹点位置，还要基于半径、半径方向、lineNum加一个偏移
            Vector3 offset = CalculatePosOffset(startAngle + lineNum * angleDelta, radius);

            for (int orderInLine = 0; orderInLine < bulletsPerWay; orderInLine++)
            //速度由慢到快这是本列第几颗子弹
            {
                info.wayIndex = lineNum;
                info.orderInWay = orderInLine;
                info.orderInOneShoot = lineNum * ways + orderInLine;
                info.orderInWave = info.timesInWave * ways * bulletsPerWay +
                              lineNum * ways + orderInLine;

                info.speed = startSpeed + orderInLine * speedDelta;
                //发射方向还要再加上一个shootDirection
                info.direction = startAngle + lineNum * angleDelta + shootDirection;

                //pos加上偏移
                //BulletManager.Instance.AddBullet(pos, info);
                switch (type)
                {
                    case ShootObjType.Bullet:
                        BulletDOTSManager.Instance.AddBullet(bulletTypeID, bulletBehaviourID, pos + offset, info);
                        break;
                    case ShootObjType.BulletGroup:
                        break;
                    case ShootObjType.Enemy:
                        EnemyDOTSManager.Instance.AddEnemy(bulletTypeID, bulletBehaviourID, pos + offset, info);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 返回一条子弹初始位置相对于圆心的位置偏移
    /// </summary>
    /// <param name="direction">圆心到初始位置的角度偏移，正右为0</param>
    /// <param name="distance">圆心到初始位置的距离</param>
    /// <returns>坐标偏移</returns>
    private Vector3 CalculatePosOffset(float direction, float distance)
    {
        // 将角度转换为弧度，顺时针为正，需要对传入的 direction 取反
        float angleRad = -direction * Mathf.Deg2Rad;

        float x = distance * Mathf.Cos(angleRad);
        float y = distance * Mathf.Sin(angleRad);

        return new Vector3(x, y, 0f);
    }
}
