using UnityEngine;


public abstract class ShootPatternSO : ScriptableObject
{
    public abstract ShootPattern CreateRuntimePattern();
}
