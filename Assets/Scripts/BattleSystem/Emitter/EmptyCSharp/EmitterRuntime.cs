using System.Collections.Generic;
using UnityEngine;

public abstract class EmitterRuntime
{
    //持有配置文件的引用（只读）
    protected AbstractEmitterConfigSO config;

    //每个发弹点发射的子弹样式
    protected ShootPattern pattern;

    //缓存对象，传给 config 去填充数据
    protected List<Vector3> posBuffer;      //每个发弹点的位置
    protected List<float> dirBuffer;        //每个发弹点的角度

    protected int timesInWave;        //当前波次中的第几次发射

    public EmitterRuntime(AbstractEmitterConfigSO emitter)
    {
        config = emitter;
        pattern = pattern.UpdatePattern(config);
        posBuffer = new List<Vector3>(10); // 给个初始容量，减少扩容开销
        dirBuffer = new List<float>(10);
        timesInWave = 0;
    }

    /// <summary>
    /// 发射器发射一次弹幕
    /// </summary>
    /// <param name="start">发弹敌人的Transform</param>
    public abstract void Shoot(Transform start, bool newWave = false);


    /// <summary>
    /// 让一个发弹点发射一次SO文件规定的样式的弹幕
    /// </summary>
    /// <param name="pos">该发弹点的世界坐标</param>
    /// <param name="dir">该发弹点基于世界坐标系的朝向</param>
    protected void ShootOnePoint(int emitterIndex, int timesInWave, Vector3 pos, float dir, ShootPattern pattern)
    {
        float startAngle = 0f, angleDelta = 0f;

        float startSpeed = pattern.minSpeed;
        float speedDelta = pattern.bulletsPerWay > 1 ? (pattern.maxSpeed - pattern.minSpeed) / (pattern.bulletsPerWay - 1) : 0f;

        if(pattern.ways == 1)
        {
            startAngle = dir;
        }
        else
        {
            if (pattern.range >= 360f)     //子弹发射一圈
            {
                angleDelta = 360f / pattern.ways;
                startAngle = dir - 180f + (angleDelta / 2f);
            }
            else if (pattern.range > 0)      //子弹呈一个扇形
            {
                angleDelta = pattern.range / (pattern.ways - 1);
                startAngle = dir - pattern.range / 2f;
            }
        }

        for (int lineNum = 0;lineNum < pattern.ways; lineNum++)    //lineNum：顺时针数第几列
        {
            for (int orderInLine = 0; orderInLine < pattern.bulletsPerWay; orderInLine++)
            //速度由慢到快这是本列第几颗子弹
            {
                BulletRuntimeInfo info = new BulletRuntimeInfo();
                info.shootPointIndex = emitterIndex;
                info.wayIndex = lineNum;
                info.orderInWay = orderInLine;
                info.orderInOneShoot = lineNum * pattern.ways + orderInLine;
                info.orderInWave = timesInWave * pattern.ways * pattern.bulletsPerWay +
                              lineNum * pattern.ways + orderInLine;
                info.speed = startSpeed + orderInLine * speedDelta;
                info.direction = startAngle + lineNum * angleDelta;
                info.lifetime = 0;

                //BulletManager.Instance.AddBullet(pos, info);
                BulletDOTSManager.Instance.AddBullet(pos, info);
            }
        }
        
    }
}
