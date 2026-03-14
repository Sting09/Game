using UnityEngine;

public abstract class UIPanel<T> : MonoBehaviour
{
    public static UIPanel<T> Instance;

    [Header("UI References")]
    public GameObject panelRoot;   // 面板根节点 (上面挂一个Image负责拦截射线阻挡点击)
    public bool defaultActiveState = false;

    protected virtual void Awake()
    {
        Instance = this;
        panelRoot.SetActive(defaultActiveState);
    }

    public virtual void Open()
    {
        panelRoot.SetActive(true);
        Refresh();
    }

    public virtual void Close()
    {
        panelRoot.SetActive(false);
    }

    public abstract void Refresh();
}