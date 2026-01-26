using UnityEngine;

[CreateAssetMenu(fileName = "BulletBasicConfigSO", menuName = "Battle System/Bullet/Basic Config")]
public class BulletBasicConfigSO : ScriptableObject
{
    [Header("Base Settings")]
    public string bulletName;        // 子弹唯一标识符
    public GameObject prefab;        // 对应的 Prefab
    [Tooltip("参考大小，事关子弹遮挡。填的值越大，显示越靠前")]
    public float zPriority;

    [Header("Collision Logic")]
    public BulletCollisionType collisionType;
    public float circleRadius;       // 圆形半径
    public Vector2 boxSize;   // 方形尺寸 (Width, Height)
}
