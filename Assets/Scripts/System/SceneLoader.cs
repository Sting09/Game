using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : SingletonMono<SceneLoader>
{
    [Header("UI")]
    public CanvasGroup fadeCanvasGroup;
    public Image loadingProgressBar;
    public Text loadingText;

    [Header("Transition")]
    [Tooltip("Fade in/out duration in seconds")]
    public float fadeDuration = 0.8f;

    [Tooltip("Minimum loading wait time in seconds")]
    public float minWaitTime = 0.5f;

    [Header("Scene Names")]
    public string battleSceneName = "Battle Scene";
    public string mapSceneName = "Map Scene";
    public string titleSceneName = "Title Scene";

    [Header("Map References")]
    public Camera mapCamera;
    public Canvas mapCanvas;

    private string currentLoadedScene = string.Empty;
    private bool isTransitioning;

    private void Start()
    {
        if (SceneManager.sceneCount == 1)
        {
            StartCoroutine(StartGameProcess());
            return;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == "Main Scene" || scene.name == "Bootstrap")
            {
                continue;
            }

            currentLoadedScene = scene.name;
            SceneManager.SetActiveScene(scene);
            break;
        }
    }

    /// <summary>
    /// Loads the battle scene in additive mode with fade transition.
    /// </summary>
    public IEnumerator LoadBattleScene()
    {
        if (isTransitioning)
        {
            yield break;
        }

        isTransitioning = true;
        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(Fade(1f));

        if (!SceneManager.GetSceneByName(battleSceneName).isLoaded)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(battleSceneName, LoadSceneMode.Additive);
            while (!op.isDone)
            {
                yield return null;
            }
        }

        if (mapCamera != null)
        {
            mapCamera.enabled = false;
        }

        if (mapCanvas != null)
        {
            mapCanvas.gameObject.SetActive(false);
        }

        Scene battleScene = SceneManager.GetSceneByName(battleSceneName);
        if (battleScene.IsValid())
        {
            SceneManager.SetActiveScene(battleScene);
            currentLoadedScene = battleSceneName;
        }

        yield return StartCoroutine(Fade(0f));
        fadeCanvasGroup.blocksRaycasts = false;
        isTransitioning = false;
    }

    /// <summary>
    /// Returns from battle scene to map scene with fade transition.
    /// </summary>
    public IEnumerator LoadMapScene()
    {
        if (isTransitioning)
        {
            yield break;
        }

        isTransitioning = true;
        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(Fade(1f));

        if (!SceneManager.GetSceneByName(mapSceneName).isLoaded)
        {
            AsyncOperation mapOp = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);
            while (!mapOp.isDone)
            {
                yield return null;
            }
        }

        Scene mapScene = SceneManager.GetSceneByName(mapSceneName);
        if (mapScene.IsValid())
        {
            SceneManager.SetActiveScene(mapScene);
            currentLoadedScene = mapSceneName;
        }

        Scene battleScene = SceneManager.GetSceneByName(battleSceneName);
        if (battleScene.isLoaded)
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(battleSceneName);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                {
                    yield return null;
                }
            }
        }

        if (mapCamera != null)
        {
            mapCamera.enabled = true;
        }

        if (mapCanvas != null)
        {
            mapCanvas.gameObject.SetActive(true);
        }

        yield return StartCoroutine(Fade(0f));
        fadeCanvasGroup.blocksRaycasts = false;
        isTransitioning = false;
    }

    /// <summary>
    /// Loads a non-main gameplay scene (Title/Map) and unloads the previous one.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadProcess(sceneName));
    }

    private IEnumerator StartGameProcess()
    {
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;

        AsyncOperation op = SceneManager.LoadSceneAsync(titleSceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }

        Scene titleScene = SceneManager.GetSceneByName(titleSceneName);
        if (titleScene.IsValid())
        {
            SceneManager.SetActiveScene(titleScene);
            currentLoadedScene = titleSceneName;
        }

        yield return StartCoroutine(Fade(0f));
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator LoadProcess(string sceneName)
    {
        fadeCanvasGroup.blocksRaycasts = true;
        yield return StartCoroutine(Fade(1f));

        string sceneToUnload = string.Empty;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == "Main Scene" || scene.name == "Bootstrap")
            {
                continue;
            }

            sceneToUnload = scene.name;
            break;
        }

        if (!string.IsNullOrEmpty(sceneToUnload))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneToUnload);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                {
                    yield return null;
                }
            }
        }

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        loadOp.allowSceneActivation = false;

        while (loadOp.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(loadOp.progress / 0.9f);
            if (loadingProgressBar != null)
            {
                loadingProgressBar.fillAmount = progress;
            }

            yield return null;
        }

        if (loadingProgressBar != null)
        {
            loadingProgressBar.fillAmount = 1f;
        }

        yield return new WaitForSeconds(minWaitTime);

        loadOp.allowSceneActivation = true;
        while (!loadOp.isDone)
        {
            yield return null;
        }

        Scene newScene = SceneManager.GetSceneByName(sceneName);
        if (newScene.IsValid())
        {
            SceneManager.SetActiveScene(newScene);
            currentLoadedScene = sceneName;
        }

        yield return StartCoroutine(Fade(0f));
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        float duration = fadeDuration > 0f ? fadeDuration : 0.01f;
        float speed = Mathf.Abs(fadeCanvasGroup.alpha - targetAlpha) / duration;

        while (!Mathf.Approximately(fadeCanvasGroup.alpha, targetAlpha))
        {
            fadeCanvasGroup.alpha = Mathf.MoveTowards(fadeCanvasGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }
}
