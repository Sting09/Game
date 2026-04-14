using System.Collections;
using UnityEngine;

// 3. 具体的转场动作：延迟
[CreateAssetMenu(menuName = "BossAction/Delay")]
public class DelayActionSO : BossTransitionActionSO
{
    public float delayTime;
    public override IEnumerator Execute(BossController boss)
    {
        Debug.Log("下一个阶段前的延迟");
        yield return new WaitForSeconds(delayTime);
    }
}
