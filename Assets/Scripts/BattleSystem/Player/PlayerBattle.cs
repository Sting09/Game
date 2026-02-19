using UnityEngine;

public class PlayerBattle : MonoBehaviour
{
    public void BattleManagerGetThis()
    {
        BattleManager.Instance.player = this.gameObject;
    }


    public void SetPlayerActive(bool state)
    {
        gameObject.SetActive(state);
    }
}
