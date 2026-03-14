using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleEndUIPanel : UIPanel<BattleEndUIPanel>
{
    public Button titleBtn;         //回到标题页面的Button
    public TextMeshProUGUI resultText;         //显示游戏结果的TextMeshPro

    protected override void Awake()
    {
        base.Awake();
        if (titleBtn == null)
        {
            Debug.Log("EndUIPanel返回标题场景的按钮未设置");
        }
        else
        {
            titleBtn.onClick.RemoveAllListeners();
            titleBtn.onClick.AddListener(BackToTitle);
        }
    }

    public override void Refresh()
    {

    }

    private void BackToTitle()
    {
        Close();
        SceneLoader.Instance.RestartGame();
    }

    public void OnGameEnd(bool winResult)
    {
        resultText.text = winResult ? "You Win!!" : "You Lose!!";
        Open();
    }
}
