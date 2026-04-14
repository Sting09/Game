using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
{
    public BossDataSO bossData;
    public BaseShooter baseShooter;
    public int enemyID; // 在EnemyDOTSManager中的索引/ID

    private int currentPhaseIndex = 0;

    public PhaseEventSO bossAttackEndEvent;
    public PhaseEventSO bossBattleWinEvent;

    public void OnEnable()
    {
        if(EnemyDOTSManager.Instance.newBoss)
        {
            EnemyDOTSManager.Instance.currentBossPhase = 0;
            EnemyDOTSManager.Instance.newBoss = false;
        }
        EnemyDOTSManager.Instance.bossController = this;
        currentPhaseIndex = EnemyDOTSManager.Instance.currentBossPhase;

        ShootCurrentDanmaku();
    }

    // boss一个阶段结束时会被打上标记；每帧结束时，检查标记，如果有标记，执行此函数
    public void OnDanmakuEnd()
    {
        bossAttackEndEvent.RaiseEvent(GamePhase.BattleFighting, this);

        // 判断是否是最后阶段
        if (currentPhaseIndex >= bossData.phases.Count)
        {
            Die();
            return;
        }

        StartCoroutine(DanmakuEndAction());
    }

    private IEnumerator DanmakuEndAction()
    {
        // 1. 停止当前弹幕发射
        baseShooter.StopShooting();

        // 2. 依次执行策划配置的转场 Action
        BossPhaseData currentPhase = bossData.phases[currentPhaseIndex];
        foreach (var action in currentPhase.onPhaseEndActions)
        {
            yield return StartCoroutine(action.Execute(this));
        }

        // 3. 准备进入下一阶段
        currentPhaseIndex++;
        
        if (currentPhaseIndex < bossData.phases.Count)
        {
            EnemyDOTSManager.Instance.currentBossPhase = currentPhaseIndex;

            ShootCurrentDanmaku();
        }
        else
        {
            EnemyDOTSManager.Instance.ResetBossHPAndInvulnerability(0, false);
        }
    }

    public void ShootCurrentDanmaku()
    {
        // 启动一个总管协程来控制严格的执行顺序
        StartCoroutine(ShootCurrentDanmakuSequence());
    }

    private IEnumerator ShootCurrentDanmakuSequence()
    {
        // 1. 触发并等待 DanmakuStartAction 协程完全结束
        if (currentPhaseIndex < bossData.phases.Count)
        {
            yield return StartCoroutine(DanmakuStartAction());
        }

        // 2. 协程彻底结束后，才会执行后续的弹幕装填
        baseShooter.danmakuToShoot.Clear();
        baseShooter.danmakuToShoot.Add(bossData.phases[currentPhaseIndex].phaseDanmaku);
        baseShooter.LoadDanmaku();

        EnemyDOTSManager.Instance.ResetBossHPAndInvulnerability(bossData.phases[currentPhaseIndex].maxHP, false);

        // 3. 最后开始射击
        baseShooter.StartShooting();
    }
    private IEnumerator DanmakuStartAction()
    {
        // 1. 确保停止当前弹幕发射
        baseShooter.StopShooting();

        // 2. 依次执行策划配置的转场 Action
        BossPhaseData currentPhase = bossData.phases[currentPhaseIndex];
        foreach (var action in currentPhase.onPhaseStartActions)
        {
            yield return StartCoroutine(action.Execute(this));
        }
    }


    private void Die()
    {
        // 触发击败Boss事件
        bossBattleWinEvent.RaiseEvent(GamePhase.BattleWin, this);

        // Boss 真正死亡
        EnemyDOTSManager.Instance.RemoveBoss();
        //Destroy(gameObject); // 或者放回对象池

        EnemyDOTSManager.Instance.currentBossPhase = 0;
        EnemyDOTSManager.Instance.newBoss = true;

        // 触发战斗胜利
        BattleController.Instance.StartCertainPhase(GamePhase.BattleWin);
    }
}