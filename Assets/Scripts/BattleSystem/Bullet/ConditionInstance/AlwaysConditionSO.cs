using UnityEngine;

[CreateAssetMenu(fileName = "AlwaysConditionSO", menuName = "Battle System/Bullet/Condition/AlwaysConditionSO")]
public class AlwaysConditionSO : BulletConditionSO
{
    public override bool IfTrue(BulletManagerData data)
    {
        return data.isActive;
    }
}
