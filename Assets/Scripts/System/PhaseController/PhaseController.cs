using System.Collections.Generic;
using UnityEngine;

public class PhaseController : SingletonMono<PhaseController>
{
    public GamePhase currentPhase;  //当前的阶段
    public int currentPhaseIndex = -1;

    public List<PhaseSO> phaseList;
    public Dictionary<GamePhase, int> phaseToIntDict;

    public int roundNum = 4;       //一共几个轮次
    public List<int> turnNum;       //一回合几个回合
    public List<int> shrinkNum;     //本轮次移除几个地块

    public int currentTurn;        //当前是第几个回合
    public int currentRound;       //当前是第几个轮次

    public float defaultAutoEndDuration;        //用于重置每个阶段的自动结束计时

    public bool suddenDeath = false;                //是否进入了加赛阶段

    private void OnEnable()
    {
        phaseToIntDict = new Dictionary<GamePhase, int>(phaseList.Count);
        for (int i = 0; i<phaseList.Count; i++)
        {
            phaseToIntDict.Add(phaseList[i].phase, i);
        }

        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;

        currentTurn = 0;
        currentRound = 0;
    }

    private void Start()
    {
        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    public void StartNextPhase()
    {
        // 回合结束阶段，回到回合开始阶段
        if (currentPhase == GamePhase.TurnEnd)
        {
            currentTurn++;
            //已经进入加赛，回到回合开始阶段
            if (suddenDeath)
            {
                currentPhaseIndex = phaseToIntDict[GamePhase.TurnStart];
            }
            //否则先检查本轮次是否结束
            else
            {
                currentPhaseIndex = currentTurn < turnNum[currentRound] ? phaseToIntDict[GamePhase.TurnStart] : currentPhaseIndex + 1;
            }
        }
        //一轮结束时，回到下一轮开始时
        else if (currentPhase == GamePhase.RoundEnd)
        {
            currentRound++;
            currentTurn = 0;    //一轮结束时重置currentTurn
            currentPhaseIndex = currentRound < roundNum ? phaseToIntDict[GamePhase.RoundStart] : currentPhaseIndex + 1;
        }
        else if (currentPhase == GamePhase.SuddenDeath)
        {
            suddenDeath = true;
            currentPhaseIndex = phaseToIntDict[GamePhase.RoundStart];
        }
        else
        {
            currentPhaseIndex++;
        }

        if(currentPhaseIndex >= phaseList.Count) { return;  }

        currentPhase = phaseList[currentPhaseIndex].phase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }

    /// <summary>
    /// 强制当前阶段转为指定的阶段
    /// </summary>
    /// <param name="targetPhase"></param>
    public void StartCertainPhase(GamePhase targetPhase)
    {
        currentPhaseIndex = phaseToIntDict[targetPhase];
        currentPhase = targetPhase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }


    [ContextMenu("Reset Auto End Duration")]
    public void ResetAutoEndDuration()
    {
        foreach(var phase in phaseList)
        {
            if(phase.autoEndDuration >= 0)
            {
                phase.autoEndDuration = defaultAutoEndDuration;
            }
        }
    }


    // 玩家在PlayerPhase，点击回合结束按钮
    public void EndPlayerPhase()
    {
        //首先检查当前是PlayerPhase
        if (currentPhase == GamePhase.PlayerPhase)
        {
            //如果是，执行PlayerPhase的PhaseEndFunction()协程
            StartCoroutine(phaseList[currentPhaseIndex].PhaseEndFunction());
        }
    }
}
