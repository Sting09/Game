using UnityEngine;

[CreateAssetMenu(fileName = "ArrowPatternSO", menuName = "Scriptable Objects/ArrowPatternSO")]
public class ArrowPatternSO : ShootPatternSO
{
    [Header("Arrow Pattern")]
    [TextArea(4, 24)]
    public string description;

    public override ShootPattern CreateRuntimePattern()
    {
        return new ArrowShootPattern(this);
    }
}
