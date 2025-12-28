using UnityEngine;

public abstract class ShootPattern
{
    protected ShootPatternSO config;
    protected EmitterRuntime ownerEmitterRuntime;  //发射这个样式的发射器
    protected string bulletName;
    protected int bulletTypeID;

    public ShootPattern(ShootPatternSO config, EmitterRuntime runtime)
    {
        this.config = config;
        ownerEmitterRuntime = runtime;
        this.bulletName = config.bulletName;
        bulletTypeID = BulletDOTSManager.Instance.m_NameToID[bulletName];
    }

    /// <summary>
    /// 重新随机该波次的弹幕样式参数
    /// </summary>
    public abstract void UpdatePattern();

    /// <summary>
    /// 发射一次规定样式的弹幕
    /// </summary>
    /// <param name="shootPointIndex">发射器的第几个发弹点发弹</param>
    /// <param name="timesInWave">本波次中第几次发射</param>
    /// <param name="waveTimes">第几波弹幕</param>
    /// <param name="pos">发弹点位置</param>
    /// <param name="dir">发弹点朝向</param>
    public abstract void ShootBullet(int shootPointIndex,
                                     int timesInWave,
                                     int waveTimes,
                                     Vector3 pos,
                                     float dir);
}
