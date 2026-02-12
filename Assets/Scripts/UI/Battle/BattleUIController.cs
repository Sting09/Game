using UnityEngine;

public class BattleUIController : MonoBehaviour
{
    public void TestWinBtn()
    {
        if(BattleController.Instance.currentPhase == GamePhase.BattleFighting)
        {
            BattleController.Instance.currentPhase = GamePhase.BattleWin;
            BattleController.Instance.currentPhaseIndex = BattleController.Instance.phaseToIntDict[GamePhase.BattleWin];
            BattleController.Instance.StartPhase();
        }
        BattleManager.Instance.EndBattle(true);
    }

    public void TestLoseBtn()
    {
        if (BattleController.Instance.currentPhase == GamePhase.BattleFighting)
        {
            BattleController.Instance.currentPhase = GamePhase.BattleLose;
            BattleController.Instance.currentPhaseIndex = BattleController.Instance.phaseToIntDict[GamePhase.BattleLose];
            BattleController.Instance.StartPhase();
        }
        BattleManager.Instance.EndBattle(false);
    }
}
