using UnityEngine;

public abstract class BulletModifierSO : ScriptableObject
{
    [TextArea(4, 20)]
    [SerializeField] private string description;

    public abstract void Apply(BulletManagerData data, float dt);
}
