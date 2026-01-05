using System.Collections.Generic;
using UnityEngine;

public class PhaseController : SingletonMono<PhaseController>
{
    public GamePhase currentPhase;  //当前的阶段
    public int currentPhaseIndex = -1;
    public List<PhaseSO> phaseList;

    public int turnNum = 10;       //一回合几个回合
    public int roundNum = 4;       //一共几个轮次

    public int currentTurn;        //当前是第几个回合
    public int currentRound;       //当前是第几个轮次

    private void OnEnable()
    {
        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;
    }

    private void Start()
    {
        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    public void StartNextPhase()
    {
        currentPhaseIndex++;

        if(currentPhaseIndex >= phaseList.Count) { return;  }

        currentPhase = phaseList[currentPhaseIndex].phase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }
}
