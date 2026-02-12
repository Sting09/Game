using UnityEngine;

public class PlayerBattle : MonoBehaviour
{
    public void BattleManagerGetThis()
    {
        BattleManager.Instance.player = this.gameObject;
    }
}
