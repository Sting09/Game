using UnityEngine;

public class StageDirector : MonoBehaviour
{
    public BaseShooter stageDirectorShooter;

    public void SetManagerParameter()
    {
        BattleManager.Instance.stageDirector = this;
    }

    public void LoadCurrentDanmaku()
    {
        stageDirectorShooter.danmakuToShoot.Clear();
        stageDirectorShooter.danmakuToShoot.Add(BattleContext.currentAttack.attackDirector);
    }
}
