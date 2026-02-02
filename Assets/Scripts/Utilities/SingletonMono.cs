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
                    DontDestroyOnLoad(singletonObject); // 保留在场景切换时不被销毁
                }
            }
            return instance;
        }
    }

    //使用virtual虚函数，子类继承可能还需要用Awake()
    protected virtual void Awake()
    {
        // 确保在场景切换时不会销毁该实例
        DontDestroyOnLoad(gameObject);
        // 检查是否存在重复的实例
        if (instance == null)
        {
            instance = this as T;
        }
        else
        {
            Debug.LogWarning("存在重复的单例" + typeof(T).Name + "删除");
            Destroy(gameObject);
        }
    }
}