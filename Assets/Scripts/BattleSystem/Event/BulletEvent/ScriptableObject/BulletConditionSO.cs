using UnityEngine;

public abstract class BulletConditionSO : ScriptableObject
{
    [TextArea(4, 20)]
    [SerializeField] private string description;


    public abstract bool IfTrue(BulletManagerData data);
}
