using System.Collections;
using UnityEngine;

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
        // 执行固有逻辑
        PhaseFunction();

        // 通知注册了事件的物体，执行各自的方法
        phaseEventSO.RaiseEvent(phase, this);

        // 当前阶段自动结束，则添加一个协程
        if(autoEndDuration >= 0)
        {
            runner.StartCoroutine(PhaseAutoEnd());
        }
    }

    public void PhaseEnd()
    {
        PhaseController.Instance.StartNextPhase();
    }

    public IEnumerator PhaseAutoEnd()
    {
        yield return new WaitForSeconds(autoEndDuration);

        PhaseEnd();
    }

    public void PhaseFunction()
    {
        Debug.Log(phase.ToString());
    }
}
