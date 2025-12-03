using System;
using UnityEngine;
using UnityEngine.Events;

//泛型事件监听器基类
public class BaseEventListener<T> : MonoBehaviour
{
    [TextArea(3, 10)]
    public string description;
    //监听的事件类型
    public BaseEventSO<T> eventSO;
    //注册的函数
    public UnityEvent<T> response;


    //省去了以前EventHandler手动注册注销事件的步骤
    private void OnEnable()
    {
        if (eventSO != null)
        {
            eventSO.OnEventRaised += OnEventRaisedFunc;
        }
    }

    private void OnDisable()
    {
        if (eventSO != null)
        {
            eventSO.OnEventRaised -= OnEventRaisedFunc;
        }
    }

    //监听的事件触发时，执行所有注册的函数
    private void OnEventRaisedFunc(T value)
    {
        response.Invoke(value);
    }
}