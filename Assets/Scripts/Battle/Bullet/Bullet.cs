using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    public Velocity speed;      //速度。有方向属性和大小属性
    public BaseBulletParametersSO parameters;
    public bool rotateToDirection;  //子弹贴图朝向始终与速度方向一致
    public float totalTime;     //子弹自加载后经过了多久
    public ObjectPool<GameObject> poolPointer;
}

public class Velocity
{
    public float speedValue; //速度大小
    public float direction; //角度制，相对世界方向，右侧为0，正下为90
}
