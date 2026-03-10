using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public enum RewardState { Unknown, Opened, Closed }

// 奖励运行时的实例对象，独立于静态配置
public class RewardInstance
{
    public Room Room { get; private set; }
    public RewardSO Data { get; private set; }
    public RewardState CurrentState { get; private set; }

    // 当前的选项
    public List<RewardOptionDef> currentOptions = new List<RewardOptionDef>();

    // 记录已经选择过的选项ID，便于条件判断 "选择某选项后才出现"
    public HashSet<string> SelectedOptionIDs { get; private set; } = new HashSet<string>();

    // 运行时被强制隐藏的选项
    public HashSet<string> ForceHiddenOptionIDs { get; private set; } = new HashSet<string>();

    public RewardInstance(RewardSO data, Room room)
    {
        Data = data;
        Room = room;
        CurrentState = data.defaultState;

        switch (CurrentState)
        {
            case RewardState.Unknown:
                foreach(var option in data.unknownOptions) { currentOptions.Add(option);}
                break;
            case RewardState.Opened:
                foreach (var option in data.openedOptions) { currentOptions.Add(option); }
                break;
            case RewardState.Closed:
                currentOptions.Clear();
                break;
        }
    }

    public void ChangeState(RewardState newState)
    {
        currentOptions.Clear();
        CurrentState = newState;
        switch (CurrentState)
        {
            case RewardState.Unknown:
                foreach (var option in Data.unknownOptions) { currentOptions.Add(option); }
                break;
            case RewardState.Opened:
                foreach (var option in Data.openedOptions) { currentOptions.Add(option); }
                break;
            case RewardState.Closed:
                currentOptions.Clear();
                break;
        }
        // 状态改变时，通知UI刷新
        RoomUIPanel.Instance.Refresh();
    }

    // 需求6: 强制进入关闭状态
    public void ForceClose()
    {
        ChangeState(RewardState.Closed);
    }

    // 辅助方法：供行为调用以动态隐藏其他选项
    public void HideOption(string optionID)
    {
        ForceHiddenOptionIDs.Add(optionID);
        RoomUIPanel.Instance.Refresh();
    }
}

// 单个选项的运行时状态，极大地增强了灵活性 (需求4)
public class OptionRuntimeState
{
    public RewardOptionDef Def { get; private set; }
    public string DisplayText;
    public bool IsInteractable;

    // 灵活的黑盒字典：允许 OnGenerate 预存数据给 Execute 使用 (完美解决需求4的灵活代价预演)
    public Dictionary<string, object> TempData = new Dictionary<string, object>();

    public OptionRuntimeState(RewardOptionDef def)
    {
        Def = def;
        DisplayText = def.rawText;
        IsInteractable = true;
    }
}