using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndUIPanel : UIPanel<EndUIPanel>
{
    public Button titleBtn;         //回到标题页面的Button
    public TextMeshProUGUI resultText;         //显示游戏结果的TextMeshPro
    public bool gameResult = false;             //游戏结果，是否胜利？

    protected override void Awake()
    {
        base.Awake();
        if(titleBtn == null)
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

    public void OnGameEnd()
    {
        resultText.text = gameResult ? "You Win!!" : "You Lose!!";
        Debug.Log(resultText.text);
        Open();
    }

    public void SetGameResult(bool result)
    {
        gameResult = result;
    }
}
