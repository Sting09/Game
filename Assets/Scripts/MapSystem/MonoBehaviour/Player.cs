using UnityEngine;

public class Player : MonoBehaviour
{
    public bool canMoveOverTile = true;     //玩家在当前回合是否能跨地块移动
    public bool canMove = true;

    //每回合开始时，重置玩家状态
    public void TurnStartResetState()
    {
        canMove = true;
        canMoveOverTile = true;
    }
}
