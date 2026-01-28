using UnityEngine;


public abstract class ShootPatternSO : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("要发射的子弹类型")]
    public string bulletName = "RedBullet";
    [Tooltip("子弹的存活时长。超时将被移除")]
    public float bulletDuration = 10f;
    [Tooltip("子弹是否跟随发弹源移动")]
    public bool isRelative = false;
    [Tooltip("子弹是否跟随发弹源移动")]
    public ShootObjType objType = ShootObjType.Bullet;


    [Header("Bullet Event")]
    public string bulletBehavior = "None"; // 决定轨迹
    public abstract ShootPattern CreateRuntimePattern(EmitterRuntime emitterRuntime);
}
