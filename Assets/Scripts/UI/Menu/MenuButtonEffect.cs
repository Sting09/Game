using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("配置项")]
    public GameObject backgroundEffect;
    public TMP_Text buttonText;

    [Header("参数设置")]
    public float defaultFontSize = 40f;
    public float selectedFontSize = 50f;

    // --- 1. 鼠标交互逻辑：只负责改变 EventSystem 的状态 ---

    // 鼠标移入：强行让 EventSystem 选中当前按钮
    // 这样做的结果是：之前的按钮会自动 Deselect，当前按钮会自动 Select
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 只有当当前选中的不是自己时才赋值，避免重复触发
        if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    // 鼠标移出：告诉 EventSystem 取消一切选中
    public void OnPointerExit(PointerEventData eventData)
    {
        // 只有当当前选中的是自己时，才取消。防止鼠标快速滑过A到了B，结果A把B的选中取消了
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // --- 2. 系统事件逻辑：只负责视觉表现 ---

    // 无论是键盘选中，还是上面的 OnPointerEnter 触发的选中，最终都会走到这里
    public void OnSelect(BaseEventData eventData)
    {
        UpdateVisuals(true);
    }

    // 无论是键盘移开，还是上面的 OnPointerExit 触发的取消，最终都会走到这里
    public void OnDeselect(BaseEventData eventData)
    {
        UpdateVisuals(false);
    }

    // --- 3. 统一的视觉刷新函数 ---
    private void UpdateVisuals(bool isSelected)
    {
        if (backgroundEffect != null)
            backgroundEffect.SetActive(isSelected);

        if (buttonText != null)
            buttonText.fontSize = isSelected ? selectedFontSize : defaultFontSize;
    }
}