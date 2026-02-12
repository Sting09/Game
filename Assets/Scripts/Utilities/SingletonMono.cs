using System;
using UnityEngine;


//单例模式模板
//让T始终有且只有一个

//使用方法
//创建：public class TestManager : SingletonMono<TestManager>{}
//引用：TestManager.Instance.Function();
//重写Awake：protected override void Awake() { base.Awake(); //... }

public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                // 在场景中查找是否已存在该类型的实例
                instance = FindFirstObjectByType<T>();
                // 如果场景中不存在该类型的实例，则创建一个新的GameObject并添加该组件
                if (instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name + "_Singleton");
                    instance = singletonObject.AddComponent<T>();
                }
            }
            return instance;
        }
    }


    //使用virtual虚函数，子类继承可能还需要用Awake()
    protected virtual void Awake()
    {
        // 1. 先判断是否是重复的
        if (instance != null && instance != this) // 这里的判断要严谨
        {
            // 发现重复！

            // 关键点：如果是重复的，不要执行后续的初始化代码！
            // 仅仅 Destroy 是不够的，因为 Destroy 是延时的。
            // 如果这里有其他初始化逻辑，必须阻断。

            //Debug.LogWarning($"检测到重复单例 {typeof(T).Name}，正在销毁新创建的实例 (GameObject: {name})");

            Destroy(gameObject);

            // 2. 及其重要：阻止后续代码执行（虽然 Awake 返回 void，但如果是协程就有用，这里主要是为了逻辑清晰）
            return;
        }

        // 3. 只有确认自己是正牌实例，才进行赋值和 DDOL
        instance = this as T;
        SetDontDestroyOnLoad(gameObject);

        // 初始化代码写在这里...
    }

    // 4. 防御性编程：防止幽灵帧执行 Start 或 OnEnable
    private void OnEnable()
    {
        if (instance != null && instance != this) return; // 如果我是个冒牌货，什么都别做
                                                          // 正常的 OnEnable 逻辑
    }


    public virtual void SetDontDestroyOnLoad(GameObject obj)
    {
        DontDestroyOnLoad(obj); // 保留在场景切换时不被销毁
    }
}