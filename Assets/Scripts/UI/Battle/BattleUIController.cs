using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIController : MonoBehaviour
{
    public Button testWinBtn;
    public Button testLoseBtn;
    public Button backMapBtn;
    public Button retryBtn;
    public TextMeshProUGUI hpText;
    public GameObject pausePanel;

    private void OnEnable()
    {
        testWinBtn.onClick.AddListener(TestWinBtn);
        testLoseBtn.onClick.AddListener(TestLoseBtn);
        backMapBtn.onClick.AddListener(BackMapBtn);
        retryBtn.onClick.AddListener(RetryBtn);
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

    public void BackMapBtn()
    {
        if (BattleController.Instance.currentPhase == GamePhase.BattlePause)
        {
            BattleController.Instance.StartCertainPhase(GamePhase.BattleLose);
        }
    }

    public void RetryBtn()
    {
        GameManager.Instance.player.ResetBattleData();
        if (BattleController.Instance.currentPhase == GamePhase.BattlePause)
        {
            BattleController.Instance.StartCertainPhase(GamePhase.BattlePrewarm);
        }
        BattleManager.Instance.player.GetComponent<PlayerMovementController>().ResetPlayerPosition();
    }

    public void UpdateHPText()
    {
        hpText.SetText("HP: {0}", GameManager.Instance.player.currentHP);
    }

    public void SetPausePanelState(bool state)
    {
        pausePanel.SetActive(state);
    }
}
