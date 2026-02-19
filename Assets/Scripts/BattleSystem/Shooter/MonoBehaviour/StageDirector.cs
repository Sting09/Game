using UnityEngine;

public class StageDirector : MonoBehaviour
{
    public void SetManagerParameter()
    {
        BattleManager.Instance.stageDirector = this;
    }
}
