using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomUIPanel : MonoBehaviour
{
    public static RoomUIPanel Instance;

    [Header("UI References")]
    public GameObject panelRoot;   // 面板根节点 (上面挂一个Image负责拦截射线阻挡点击)
    public TextMeshProUGUI descriptionText;   // 描述文本
    public Button closeButton;     // 关闭按钮
    public Button searchButton;     //搜索按钮

    [Header("Option Button Prefab")]
    public Transform optionsContainer;
    public GameObject optionButtonPrefab; // 挂载 Button 和 Text组件

    private Room currentRoom;
    private List<GameObject> activeOptionButtons = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        closeButton.onClick.AddListener(Close);
        panelRoot.SetActive(false);
    }

    public void Open(Room room)
    {
        currentRoom = room;
        panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        panelRoot.SetActive(false);
        currentRoom = null;
    }

    public void Refresh()
    {
        if (currentRoom == null) return;
        ClearOptions();

        RewardInstance reward = currentRoom.ActiveReward;

        // 需求3: 房间没有奖励时
        if (reward == null)
        {
            descriptionText.text = "此处空无一物。";
            CreateSearchButton(); // 提供最初的"探索"按钮
            return;
        }

        // 有奖励时，根据状态获取配置
        List<RewardOptionDef> availableOptions = new List<RewardOptionDef>();
        switch (reward.CurrentState)
        {
            case RewardState.Unknown:
                descriptionText.text = reward.Data.unknownText;
                availableOptions = reward.Data.unknownOptions;
                break;
            case RewardState.Opened:
                descriptionText.text = reward.Data.openedText;
                availableOptions = reward.Data.openedOptions;
                break;
            case RewardState.Closed:
                descriptionText.text = reward.Data.closedText;
                availableOptions = new List<RewardOptionDef>(); // 需求5: Closed没有任何选项
                break;
        }

        // 生成选项并处理生命周期
        foreach (var optDef in availableOptions)
        {
            // 检查可见性
            if (reward.ForceHiddenOptionIDs.Contains(optDef.optionID)) continue;
            if (!CheckAllConditions(optDef.visibilityConditions, reward)) continue;

            // 初始化运行时状态
            OptionRuntimeState optState = new OptionRuntimeState(optDef);
            optState.IsInteractable = CheckAllConditions(optDef.interactableConditions, reward);

            // 触发OnGenerate，允许Action修改DisplayText或缓存预定义数据
            foreach (var action in optDef.actions)
            {
                if (action != null) action.OnGenerate(reward, optState);
            }

            CreateOptionUI(reward, optState);
        }
    }

    private void CreateSearchButton()
    {
        searchButton.onClick.AddListener(() => GameManager.Instance.PlayerSearch(currentRoom));
    }

    private void CreateOptionUI(RewardInstance reward, OptionRuntimeState optState)
    {
        GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
        btnObj.GetComponentInChildren<TextMeshProUGUI>().text = optState.DisplayText;

        Button btn = btnObj.GetComponent<Button>();
        btn.interactable = optState.IsInteractable;

        btn.onClick.AddListener(() =>
        {
            reward.SelectedOptionIDs.Add(optState.Def.optionID);

            // 依次执行所有行为
            foreach (var action in optState.Def.actions)
            {
                if (action != null) action.Execute(reward, optState);
            }

            Refresh(); // 执行完后刷新UI
        });

        activeOptionButtons.Add(btnObj);
    }

    private void ClearOptions()
    {
        foreach (var btn in activeOptionButtons) Destroy(btn);
        activeOptionButtons.Clear();
    }

    private bool CheckAllConditions(List<RewardCondition> conditions, RewardInstance reward)
    {
        if (conditions == null) return true;
        foreach (var cond in conditions)
            if (cond != null && !cond.IsMet(reward)) return false;
        return true;
    }
}