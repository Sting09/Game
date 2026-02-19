using UnityEngine;

public abstract class ShootPattern
{
    protected ShootPatternSO config;
    protected EmitterRuntime ownerEmitterRuntime;  //发射这个样式的发射器
    protected string bulletName;
    protected int bulletTypeID;
    protected int bulletBehaviourID;
    public float bulletDuration;
    public bool isRelative;          //是否跟随发弹源移动
    public ShootObjType type;
    public float damage;

    public ShootPattern(ShootPatternSO config, EmitterRuntime runtime)
    {
        this.config = config;
        ownerEmitterRuntime = runtime;
        this.bulletName = config.bulletName;
        type = config.objType;
        switch (config.objType)
        {
            case ShootObjType.Bullet:
                bulletTypeID = BulletDOTSManager.Instance.GetVisualID(bulletName);
                bulletBehaviourID = config.bulletBehavior != "None" ? BulletDOTSManager.Instance.GetBehaviorID(config.bulletBehavior) : -1;
                break;
            case ShootObjType.BulletGroup:
                break;
            case ShootObjType.PlayerBullet:
                bulletTypeID = PlayerShootingManager.Instance.GetVisualID(bulletName);
                bulletBehaviourID = config.bulletBehavior != "None" ? PlayerShootingManager.Instance.GetBehaviorID(config.bulletBehavior) : -1;
                break;
            case ShootObjType.Enemy:
                bulletTypeID = EnemyDOTSManager.Instance.GetVisualID(bulletName);
                bulletBehaviourID = config.bulletBehavior != "None" ? EnemyDOTSManager.Instance.GetBehaviorID(config.bulletBehavior) : -1;
                break;
            default:
                bulletTypeID = -1;
                break;
        }
        bulletDuration = config.bulletDuration;
        isRelative = config.isRelative;
        damage = config.bulletDamage;
    }

    /// <summary>
    /// 重新随机该波次的弹幕样式参数
    /// </summary>
    public abstract void UpdatePattern();

    /// <summary>
    /// 发射一次规定样式的弹幕
    /// </summary>
    /// <param name="bulletInfo">子弹信息，填写必要的内容，再传给pattern填写</param>
    /// <param name="pos">发弹点位置</param>
    /// <param name="dir">发弹点朝向</param>
    public abstract void ShootBullet(BulletRuntimeInfo bulletInfo,
                                     Vector3 pos,
                                     float dir);
}
