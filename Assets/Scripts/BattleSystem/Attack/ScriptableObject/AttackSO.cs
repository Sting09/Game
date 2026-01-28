using UnityEngine;

[CreateAssetMenu(fileName = "AttackSO", menuName = "Game Data/AttackSO")]
public class AttackSO : ScriptableObject
{
    public int level;       //难度
    public bool isOpponent;     //是否是对手。若为false，表示是野怪

    public float startTime;     //本波攻击的开始时间
}
