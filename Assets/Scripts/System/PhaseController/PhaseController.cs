using System.Collections.Generic;
using UnityEngine;

public class PhaseController : SingletonMono<PhaseController>
{
    public GamePhase currentPhase;  //当前的阶段
    public int currentPhaseIndex = -1;

    public List<PhaseSO> phaseList;
    public Dictionary<GamePhase, int> phaseToIntDict;

    public int roundNum = 6;       //一共要打几个阎王
    public List<int> shrinkNum;     //本轮阎王打完要移除几个地块

    public int currentRound;                    //已经打败了几个阎王
    public float currentImpression;             //当前的暴露值

    public float defaultAutoEndDuration;        //用于重置每个阶段的自动结束计时

    public bool suddenDeath = false;                //是否进入了加赛阶段（十个阎王都打完将进入此阶段）

    private void OnEnable()
    {
        //构建字典
        phaseToIntDict = new Dictionary<GamePhase, int>(phaseList.Count);
        for (int i = 0; i<phaseList.Count; i++)
        {
            phaseToIntDict.Add(phaseList[i].phase, i);
        }

        //进入第一个阶段
        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;

        //计数器归零
        currentRound = 0;
        currentImpression = 0f;
    }


    /// <summary>
    /// 开始下一阶段。特殊值特殊处理，否则直接执行序号+1的阶段
    /// </summary>
    public void StartNextPhase()
    {
        currentPhaseIndex++;
        if (currentPhaseIndex >= phaseList.Count) { return;  }

        currentPhase = phaseList[currentPhaseIndex].phase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }


    /// <summary>
    /// 强制当前阶段转为指定的阶段
    /// </summary>
    /// <param name="targetPhase">指定的阶段</param>
    public void StartCertainPhase(GamePhase targetPhase)
    {
        currentPhaseIndex = phaseToIntDict[targetPhase];
        currentPhase = targetPhase;
        phaseList[currentPhaseIndex].PhaseStart(this);
    }


    // 测试用功能
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

    public void GameWinPhase()
    {
        StartCertainPhase(GamePhase.GameEnd);
    }

    public void GameLosePhase()
    {
        StartCertainPhase(GamePhase.GameEnd);
    }


    public void StartGame()
    {
        //进入第一个阶段
        currentPhaseIndex = 0;
        currentPhase = phaseList[currentPhaseIndex].phase;

        //计数器归零
        currentRound = 0;
        currentImpression = 0f;

        //游戏开始，执行第一个阶段
        phaseList[currentPhaseIndex].PhaseStart(this);
    }
}
