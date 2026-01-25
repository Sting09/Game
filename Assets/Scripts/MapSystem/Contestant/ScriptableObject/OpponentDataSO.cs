using UnityEngine;

[CreateAssetMenu(fileName = "OpponentDataSO", menuName = "Game Data/OpponentDataSO")]
public class OpponentDataSO : ScriptableObject
{
    public string opponentName;
    public Sprite opponentIcon;
    public AttackSO opponentAttack;
}
