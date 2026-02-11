using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    public string defaultSceneName;

    void Start()
    {
        // 初始化完成后，自动进入标题画面
        SceneLoader.Instance.LoadScene(defaultSceneName);
    }
}
