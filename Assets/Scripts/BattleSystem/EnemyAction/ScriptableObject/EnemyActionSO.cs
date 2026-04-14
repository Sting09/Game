// 1. 转场动作基类
using System.Collections;
using UnityEngine;

public abstract class BossTransitionActionSO : ScriptableObject
{
    // 执行转场动作，传入Boss引用以便获取位置、组件等信息
    public abstract IEnumerator Execute(BossController boss);
}


// 未来扩展：比如[播放特效]、[释放特殊过渡弹幕]、[玩家血量减半]等，只需新建类继承 BossTransitionActionSO 即可，完全符合开闭原则。