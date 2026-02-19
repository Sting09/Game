using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    public Button testWinBtn;
    public Button testLoseBtn;

    private void OnEnable()
    {
        testWinBtn.onClick.AddListener(TestWinBtn);
        testLoseBtn.onClick.AddListener(TestLoseBtn);
    }

    public void TestWinBtn()
    {
        if(BattleController.Instance.currentPhase == GamePhase.BattleFighting)
        {
            BattleController.Instance.StartCertainPhase(GamePhase.BattleWin);
        }
    }

    public void TestLoseBtn()
    {
        if (BattleController.Instance.currentPhase == GamePhase.BattleFighting)
        {
            BattleController.Instance.StartCertainPhase(GamePhase.BattleLose);
        }
    }
}
