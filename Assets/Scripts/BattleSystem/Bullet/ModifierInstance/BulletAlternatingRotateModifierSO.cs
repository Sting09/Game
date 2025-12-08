using UnityEngine;

[CreateAssetMenu(fileName = "BulletAlternatingRotateModifierSO", menuName = "Battle System/Bullet/Modifier/BulletAlternatingRotateModifierSO")]
public class BulletAlternatingRotateModifierSO : BulletModifierSO
{
    public float acceleration = 0f;      //加速度
    public float angularVelocity = 5f;   //角速度

    public override void Apply(float dt)
    {
        //奇数波偶数左右偏转相反
        
    }
}
