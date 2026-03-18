using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyBasicConfigSO", menuName = "Battle System/Enemy/Basic Config")]
public class EnemyBasicConfigSO : ScriptableObject
{
    [Header("Base Settings")]
    public string enemyName;        // 敌人唯一标识符
    public GameObject prefab;        // 对应的 Prefab
    [Tooltip("参考大小，事关子弹遮挡。填的值越大，显示越靠前")]
    public float zPriority;
    public bool isBoss = false;

    [Header("Collision Logic")]
    public BulletCollisionType collisionType;
    public float circleRadius;       // 圆形半径
    public Vector2 boxSize;   // 方形尺寸 (Width, Height)

    [Header("Life Settings")]
    public float maxHP;
    public List<DamageReductionStage> drTimeline;       //减伤

}



[System.Serializable]
public struct DamageReductionStage
{
    [Tooltip("减伤比例。1表示100%免伤，0表示无减伤，-0.5表示受伤增加50%")]
    public float reductionRate;

    [Tooltip("持续时间（秒）。如果小于等于0，表示永久持续（直到死亡）")]
    public float duration;
}
