using System.Collections.Generic;
using UnityEngine;

public class BattleController : SingletonMono<BattleController>
{
    public GamePhase currentPhase;  //当前的阶段
    public int currentPhaseIndex = -1;

    public List<PhaseSO> phaseList;     //所有战斗阶段的列表
    public Dictionary<GamePhase, int> phaseToIntDict;


    private void OnEnable()
    {
        // 初始化字典
        phaseToIntDict = new Dictionary<GamePhase, int>(phaseList.Count);
        for (int i = 0; i < phaseList.Count; i++)
        {
            phaseToIntDict.Add(phaseList[i].phase, i);
        }
    }

    public void BattleStart()
    {
        // 设置Attack，可以在这里写，也可以Prewarm事件触发
        //BattleContext.currentAttack

        // 从第一个阶段 Prewarm开始
        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;

        // 开始第一个阶段
        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    public void StartNextPhase()
    {
        // 确定下一个是什么阶段
        if (currentPhase == GamePhase.BattleWin || currentPhase == GamePhase.BattleLose)
        {
            currentPhaseIndex = phaseToIntDict[GamePhase.BattleEnd];
        }
        else
        {
            currentPhaseIndex++;
        }
        

        if (currentPhaseIndex >= phaseList.Count) { return; }

        currentPhase = phaseList[currentPhaseIndex].phase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    public void StartCertainPhase(GamePhase targetPhase)
    {
        // 如果有开始非战斗阶段的阶段，则参数出错，不执行
        if (!phaseList[phaseToIntDict[targetPhase]].isBattle) { return; }

        currentPhaseIndex = phaseToIntDict[targetPhase];
        currentPhase = targetPhase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }
}
