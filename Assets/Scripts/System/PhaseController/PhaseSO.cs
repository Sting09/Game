using System.Collections;
using UnityEngine;
using static Unity.Collections.Unicode;

[CreateAssetMenu(fileName = "PhaseSO", menuName = "System/Phase/PhaseSO")]
public class PhaseSO : ScriptableObject
{
    [Header("Basic Info")]
    public GamePhase phase;             //什么阶段？
    public PhaseEventSO phaseEventSO;   //阶段开始触发的对应事件
    public float autoEndDuration = 1f;               //阶段自动结束
    public string phaseName;
    [TextArea(3, 20)]
    public string phaseDescription;


    public void PhaseStart(MonoBehaviour runner)
    {
        runner.StartCoroutine(PhaseSequenceRoutine(runner));
    }


    /// <summary>
    /// 控制协程运行的主协程
    /// </summary>
    /// <param name="runner"></param>
    /// <returns></returns>
    private IEnumerator PhaseSequenceRoutine(MonoBehaviour runner)
    {
        // yield return 配合 StartCoroutine 会暂停当前协程，直到目标协程运行结束
        yield return runner.StartCoroutine(PhaseStartFunction());

        // 通知注册了事件的物体
        // RaiseEvent 通常是同步的。如果监听者也启动了协程，这里不会等待监听者结束。
        phaseEventSO.RaiseEvent(phase, this);

        yield return runner.StartCoroutine(PhaseFunction());

        // 处理自动结束逻辑
        if (autoEndDuration >= 0)
        {
            yield return runner.StartCoroutine(PhaseAutoEnd());

            yield return runner.StartCoroutine(PhaseEndFunction());
        }
    }


    /// <summary>
    /// 触发阶段开始事件之前，本阶段要执行的任务
    /// </summary>
    public virtual IEnumerator PhaseStartFunction()
    {
        yield return null;
        Debug.Log(phase.ToString());
    }

    /// <summary>
    /// 触发阶段开始事件之后，本阶段要执行的任务
    /// </summary>
    public virtual IEnumerator PhaseFunction()
    {
        yield return null;
    }


    public IEnumerator PhaseAutoEnd()
    {
        yield return new WaitForSeconds(autoEndDuration);
    }


    public virtual IEnumerator PhaseEndFunction()
    {
        yield return null;

        PhaseController.Instance.StartNextPhase();
    }
}
