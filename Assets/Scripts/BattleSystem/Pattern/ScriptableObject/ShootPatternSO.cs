using UnityEngine;


public abstract class ShootPatternSO : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("要发射的子弹类型")]
    public string bulletName = "RedBullet";
    public abstract ShootPattern CreateRuntimePattern(EmitterRuntime emitterRuntime);
}
