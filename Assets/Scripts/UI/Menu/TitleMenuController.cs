using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class TitleMenuController : MonoBehaviour
{
    [Header("配置")]
    public Transform buttonsContainer;
    public Button continueButton;

    private Button _firstActiveButton; // 缓存第一个有效按钮

    void Start()
    {
        // 1. 存档检测逻辑 (保持你原本的)
        bool hasSaveData = CheckIfSaveExists();
        if (continueButton != null) continueButton.gameObject.SetActive(hasSaveData);

        // 2. 找到并缓存第一个有效的按钮
        RefreshFirstButton();

        // 3. 初始高亮第一个
        StartCoroutine(AutoSelectFirstButton());
    }

    void Update()
    {
        // --- 核心逻辑：当没有选中任何东西时，监听键盘 ---
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            // 如果玩家按下了 垂直方向键 (键盘上下 或 手柄摇杆)
            if (Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f)
            {
                if (_firstActiveButton != null)
                {
                    EventSystem.current.SetSelectedGameObject(_firstActiveButton.gameObject);
                }
                else
                {
                    // 为了防止引用丢失（比如按钮动态删除了），重新找一次
                    RefreshFirstButton();
                    if (_firstActiveButton != null)
                        EventSystem.current.SetSelectedGameObject(_firstActiveButton.gameObject);
                }
            }
        }
    }

    // 辅助：找到列表里的第一个激活按钮
    void RefreshFirstButton()
    {
        foreach (Transform child in buttonsContainer)
        {
            if (child.gameObject.activeInHierarchy)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null)
                {
                    _firstActiveButton = btn;
                    break; // 找到第一个就收工
                }
            }
        }
    }

    IEnumerator AutoSelectFirstButton()
    {
        yield return null;
        if (_firstActiveButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_firstActiveButton.gameObject);
        }
    }

    bool CheckIfSaveExists() { return false; } // 你的存档逻辑


    public void OnNewGameClicked()
    {
        // 这里可以加一个音效，比如 AudioManager.Play("Click");
        // 加载地图场景，假设你的场景名叫 "MapScene"
        SceneLoader.Instance.LoadScene("Map Scene");
    }

    public void OnContinueClicked()
    {
        // 继续游戏通常需要读取存档里的场景名
        // string savedScene = SaveSystem.LoadSceneName();
        // SceneLoader.Instance.LoadLevel(savedScene);

        // 暂时先写死
        SceneLoader.Instance.LoadScene("Map Scene");
    }
}