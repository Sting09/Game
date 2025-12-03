using UnityEngine;

//专门用于传递object类型参数的事件
[CreateAssetMenu(fileName = "New Object Event", menuName = "Events/Object Event")]
public class ObjectEventSO : BaseEventSO<object>
{
    // 继承自BaseEventSO<object>，专门处理object类型的事件
}
