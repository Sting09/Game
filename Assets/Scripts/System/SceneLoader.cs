using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneLoader : SingletonMono<SceneLoader>
{
    [Header("UI组件")]
    public CanvasGroup fadeCanvasGroup;
    public Image loadingProgressBar;
    public Text loadingText;

    [Header("参数设置")]
    [Tooltip("黑屏淡入/淡出的单次时长（秒）")]
    public float fadeDuration = 0.8f;

    [Tooltip("加载进度条填满后，强制额外等待的时间（秒），防止画面一闪而过")]
    public float minWaitTime = 0.5f;

    // --- 新增：记录当前加载的“内容场景”名字 ---
    private string _currentLoadedScene = "";

    [Header("场景名配置")]
    public string battleSceneName = "Battle Scene";
    public string mapSceneName = "Map Scene";

    // 假设这是主摄像机（地图的）
    public Camera mapCamera;
    // 假设这是地图的UI容器，战斗时可能需要隐藏
    public Canvas mapCanvas;

    // 游戏启动时，自动加载标题画面 (在 Main Scene 的 Start 里调用)
    void Start()
    {
        // 1. 如果当前只有 Main Scene (正常打包运行情况)
        if (SceneManager.sceneCount == 1)
        {
            // --- 核心改动：不再静默加载，而是启动一个“开场流程” ---
            StartCoroutine(StartGameProcess());
        }
        // 2. 如果是编辑器调试 (比如你同时把 Main 和 Map 拖进 Hierarchy 运行)
        else
        {
            // 编辑器调试模式：因为我们把Alpha设为了1，如果不改回来，开发者就瞎了
            // 所以这里强制把遮罩变透明，方便调试
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;

            // 自动寻找那个不是 Main 的场景，并登记它 (保持原逻辑)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.name != "Main Scene" && s.name != "Bootstrap")
                {
                    _currentLoadedScene = s.name;
                    SceneManager.SetActiveScene(s);
                    break;
                }
            }
        }
    }



    public IEnumerator LoadBattleScene()
    {
        // 使用 SceneLoader 的遮罩变黑 (假设你有公开的 FadeIn 方法，或者直接用 SceneLoader 加载)
        // 这里为了演示清晰，手动写叠加逻辑，配合你的 SceneLoader 可能会更优雅

        // A. 异步叠加加载战斗场景
        AsyncOperation op = SceneManager.LoadSceneAsync(battleSceneName, LoadSceneMode.Additive);

        while (!op.isDone) yield return null;

        // B. 加载完成后，暂时关闭地图的渲染（省性能，且防止穿帮）
        // 设置摄像机
        //if (mapCamera != null) mapCamera.enabled = false;
        // 设置地图渲染
        //if (mapCanvas != null) mapCanvas.gameObject.SetActive(false);

        // C. 激活战斗场景 (确保光照等生效)
        Scene battleScene = SceneManager.GetSceneByName(battleSceneName);
        SceneManager.SetActiveScene(battleScene);
    }


    public IEnumerator LoadMapScene()
    {
        string mapSceneName = SceneLoader.Instance.mapSceneName;
        // 使用 SceneLoader 的遮罩变黑 (假设你有公开的 FadeIn 方法，或者直接用 SceneLoader 加载)
        // 这里为了演示清晰，手动写叠加逻辑，配合你的 SceneLoader 可能会更优雅

        // A. 异步叠加加载战斗场景
        AsyncOperation op = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);

        while (!op.isDone) yield return null;

        // B. 加载完成后，暂时关闭地图的渲染（省性能，且防止穿帮）
        // 设置摄像机
        //if (mapCamera != null) mapCamera.enabled = false;
        // 设置地图渲染
        //if (mapCanvas != null) mapCanvas.gameObject.SetActive(false);

        // C. 激活战斗场景 (确保光照等生效)
        Scene mapScene = SceneManager.GetSceneByName(mapSceneName);
        SceneManager.SetActiveScene(mapScene);
    }




    // --- 新增：专门处理游戏启动时的“开场流程” ---
    private IEnumerator StartGameProcess()
    {
        // A. 确保遮罩是黑的 (双重保险，防止你在编辑器里忘了改 Alpha)
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;

        string titleSceneName = "Title Scene";

        // B. 开始加载 Title Scene
        AsyncOperation op = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false; // 先卡住，不显示

        // C. 等待加载到 90%
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // D. 允许显示
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // E. 设置活动场景
        Scene s = SceneManager.GetSceneByName(titleSceneName);
        if (s.IsValid()) SceneManager.SetActiveScene(s);
        _currentLoadedScene = titleSceneName;

        // F. 【关键】：优雅地揭开帷幕 (从黑变透明)
        // 使用你提取的参数 fadeDuration
        yield return StartCoroutine(Fade(0f));

        fadeCanvasGroup.blocksRaycasts = false;
    }

    // 辅助协程：等待场景加载完设为 Active
    private IEnumerator SetActiveWhenLoaded(AsyncOperation op, string sceneName)
    {
        while (!op.isDone) yield return null;
        Scene s = SceneManager.GetSceneByName(sceneName);
        if (s.IsValid()) SceneManager.SetActiveScene(s);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadProcess(sceneName));
    }

    private IEnumerator LoadProcess(string sceneName)
    {
        // 1. 遮罩淡出 (变黑)
        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(Fade(1f));

        // 2. --- 核心修复：智能查找并卸载旧场景 (不依赖 _currentLoadedScene 变量) ---
        // 我们遍历当前所有已加载的场景，找到那个既不是 Main 也不是 Bootstrap 的场景，把它卸载掉。
        // 这种“查户口”的方式比依赖一个字符串变量要稳健得多。
        string sceneToUnload = "";

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            // 注意：请确保这里的 "Main Scene" 和 "Bootstrap" 与你的实际场景名一致
            if (s.name != "Main Scene" && s.name != "Bootstrap")
            {
                sceneToUnload = s.name;
                break; // 假设同一时间只有一个关卡场景，找到一个就够了
            }
        }

        // 如果找到了需要卸载的场景，就执行卸载
        if (!string.IsNullOrEmpty(sceneToUnload))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneToUnload);
            // 防止卸载空引用报错（虽然逻辑上不太可能）
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        // 3. --- 叠加加载新场景 (Additive) ---
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        // 4. 进度条逻辑
        while (loadOp.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(loadOp.progress / 0.9f);
            if (loadingProgressBar != null) loadingProgressBar.fillAmount = progress;
            yield return null;
        }

        if (loadingProgressBar != null) loadingProgressBar.fillAmount = 1f;

        yield return new WaitForSeconds(minWaitTime);

        loadOp.allowSceneActivation = true;
        while (!loadOp.isDone) yield return null;

        // 5. 激活新场景
        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid())
        {
            SceneManager.SetActiveScene(newScene);
        }

        // 虽然卸载不再依赖这个变量，但记录一下是个好习惯，方便以后Debug
        _currentLoadedScene = sceneName;

        // 6. 遮罩淡入 (变透明)
        yield return StartCoroutine(Fade(0f));
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        // 这里的计算公式改为使用 fadeDuration
        // 如果 fadeDuration 设为0，防止除以0错误，给一个极小值
        float duration = fadeDuration > 0 ? fadeDuration : 0.01f;
        float speed = Mathf.Abs(fadeCanvasGroup.alpha - targetAlpha) / duration;

        while (!Mathf.Approximately(fadeCanvasGroup.alpha, targetAlpha))
        {
            fadeCanvasGroup.alpha = Mathf.MoveTowards(fadeCanvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }
        fadeCanvasGroup.alpha = targetAlpha;
    }
}