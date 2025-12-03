using UnityEngine;
using UnityEngine.Events;

//泛型事件基类
public class BaseEventSO<T> : ScriptableObject
{
    //功能描述，给开发者看，不参与逻辑
    [TextArea]
    public string description;

    //参数为T的事件（通常为函数）调用
    public UnityAction<T> OnEventRaised;
    //上一个调用该事件的对象
    public string lastSender;

    public void RaiseEvent(T value, object sender)
    {
        //OnEventRaised事件上注册的函数使用参数value执行
        OnEventRaised?.Invoke(value);
        lastSender = sender.ToString(); 
    }
}