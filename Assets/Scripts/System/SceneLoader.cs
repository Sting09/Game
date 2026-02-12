using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : SingletonMono<SceneLoader>
{
    [Header("UI引用")]
    public CanvasGroup fadeCanvasGroup;
    public Image loadingProgressBar;
    public Text loadingText;

    [Header("渐变动画")]
    [Tooltip("渐入黑屏的持续时间（秒）")]
    public float fadeInDuration = 0.8f;

    [Tooltip("渐出黑屏的持续时间（秒）")]
    public float fadeOutDuration = 0.8f;

    [Tooltip("完全黑屏后的最小停留时间（秒）")]
    public float blackoutHoldDuration = 0f;

    [Tooltip("加载进度达到100%后的最小等待时间（秒）")]
    public float minWaitTime = 0.5f;

    [Header("场景名称")]
    public string titleSceneName = "Title Scene";
    public string battleSceneName = "Battle Scene";
    public string mapSceneName = "Map Scene";

    private string currentLoadedScene = "";

    private void Start()
    {
        if (SceneManager.sceneCount == 1)
        {
            StartCoroutine(StartGameProcess());
            return;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.name != "Main Scene" && loadedScene.name != "Bootstrap")
            {
                currentLoadedScene = loadedScene.name;
                SceneManager.SetActiveScene(loadedScene);
                break;
            }
        }
    }

    /// <summary>
    /// 启动时加载标题场景，并执行一次黑屏渐出。
    /// </summary>
    public IEnumerator StartGameProcess()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }

        yield return StartCoroutine(LoadAdditiveScene(titleSceneName, true));

        Scene titleScene = SceneManager.GetSceneByName(titleSceneName);
        if (titleScene.IsValid())
        {
            SceneManager.SetActiveScene(titleScene);
            currentLoadedScene = titleSceneName;
        }

        yield return StartCoroutine(FadeOut());

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 切换到指定场景（卸载当前业务场景后再以 Additive 加载新场景）。
    /// </summary>
    /// <param name="sceneName">要加载的场景名。</param>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadProcess(sceneName));
    }

    /// <summary>
    /// 从地图进入战斗场景。
    /// </summary>
    /// <param name="onBlackScreen">进入全黑后执行的逻辑（如隐藏地图对象）。</param>
    public IEnumerator LoadBattleScene(Action onBlackScreen = null)
    {
        yield return StartCoroutine(FadeIn());
        onBlackScreen?.Invoke();
        yield return StartCoroutine(HoldBlackScreen());

        if (!IsSceneLoaded(battleSceneName))
        {
            yield return StartCoroutine(LoadAdditiveScene(battleSceneName, false));
        }

        Scene battleScene = SceneManager.GetSceneByName(battleSceneName);
        if (battleScene.IsValid())
        {
            SceneManager.SetActiveScene(battleScene);
        }

        yield return StartCoroutine(FadeOut());

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 从战斗返回地图场景，先黑屏再执行回退逻辑。
    /// </summary>
    /// <param name="onBlackScreen">进入全黑后执行的逻辑（如结算战斗并显示地图）。</param>
    public IEnumerator ReturnToMapScene(Action onBlackScreen)
    {
        yield return StartCoroutine(FadeIn());

        onBlackScreen?.Invoke();
        yield return StartCoroutine(HoldBlackScreen());

        if (IsSceneLoaded(battleSceneName))
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(battleSceneName);
            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                {
                    yield return null;
                }
            }
        }

        Scene mapScene = SceneManager.GetSceneByName(mapSceneName);
        if (mapScene.IsValid())
        {
            SceneManager.SetActiveScene(mapScene);
        }

        yield return StartCoroutine(FadeOut());

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator LoadProcess(string sceneName)
    {
        yield return StartCoroutine(FadeIn());

        string sceneToUnload = string.Empty;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.name != "Main Scene" && loadedScene.name != "Bootstrap")
            {
                sceneToUnload = loadedScene.name;
                break;
            }
        }

        if (!string.IsNullOrEmpty(sceneToUnload))
        {
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload);
            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                {
                    yield return null;
                }
            }
        }

        yield return StartCoroutine(HoldBlackScreen());
        yield return StartCoroutine(LoadAdditiveScene(sceneName, true));

        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid())
        {
            SceneManager.SetActiveScene(newScene);
            currentLoadedScene = sceneName;
        }

        yield return StartCoroutine(FadeOut());

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator LoadAdditiveScene(string sceneName, bool showLoadingProgress)
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            if (showLoadingProgress && loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = Mathf.Clamp01(loadOperation.progress / 0.9f);
            }

            yield return null;
        }

        if (showLoadingProgress && loadingProgressBar != null)
        {
            loadingProgressBar.fillAmount = 1f;
        }

        if (minWaitTime > 0f)
        {
            yield return new WaitForSeconds(minWaitTime);
        }

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }

    private bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    private IEnumerator HoldBlackScreen()
    {
        if (blackoutHoldDuration > 0f)
        {
            yield return new WaitForSeconds(blackoutHoldDuration);
        }
    }

    private IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(FadeTo(1f, fadeInDuration));
    }

    private IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        yield return StartCoroutine(FadeTo(0f, fadeOutDuration));
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float safeDuration = duration > 0f ? duration : 0.01f;
        float speed = Mathf.Abs(fadeCanvasGroup.alpha - targetAlpha) / safeDuration;

        while (!Mathf.Approximately(fadeCanvasGroup.alpha, targetAlpha))
        {
            fadeCanvasGroup.alpha = Mathf.MoveTowards(fadeCanvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }
}
