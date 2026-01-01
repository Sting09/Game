using UnityEngine;


public abstract class ShootPatternSO : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("要发射的子弹类型")]
    public string bulletName = "RedBullet";

    [Header("Bullet Event")]
    public string bulletBehavior = "None"; // 决定轨迹
    public abstract ShootPattern CreateRuntimePattern(EmitterRuntime emitterRuntime);
}
