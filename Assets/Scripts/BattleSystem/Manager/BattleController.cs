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
        phaseToIntDict = new Dictionary<GamePhase, int>(phaseList.Count);
        for (int i = 0; i < phaseList.Count; i++)
        {
            phaseToIntDict.Add(phaseList[i].phase, i);
        }

        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;
    }

    public void BattleStart()
    {
        //BattleContext.currentAttack
        currentPhaseIndex = 0;
        currentPhase = GamePhase.BattlePrewarm;

        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    public void StartNextPhase()
    {
        currentPhaseIndex++;

        if (currentPhaseIndex >= phaseList.Count) { return; }

        currentPhase = phaseList[currentPhaseIndex].phase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }
}
