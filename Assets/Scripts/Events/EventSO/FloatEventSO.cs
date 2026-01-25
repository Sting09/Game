using UnityEngine;

//专门用于传递object类型参数的事件
[CreateAssetMenu(fileName = "New Object Event", menuName = "Events/Float Event")]
public class FloatEventSO : BaseEventSO<float>
{
    // 参数为float类型的事件
}