using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "BossAction/ClearField")]
public class ClearFieldActionSO : BossTransitionActionSO
{
    public override IEnumerator Execute(BossController boss)
    {
        // 调用具体的单例方法清理弹幕和杂兵
        BulletDOTSManager.Instance.isPaused = true;
        EnemyDOTSManager.Instance.isPaused = true;
        BulletDOTSManager.Instance.ClearAllObjects(false);
        EnemyDOTSManager.Instance.ClearAllObjects(false);
        BulletDOTSManager.Instance.isPaused = false;
        EnemyDOTSManager.Instance.isPaused = false;
        yield return null;
    }
}
