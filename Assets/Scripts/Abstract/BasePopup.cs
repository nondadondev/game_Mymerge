using UnityEngine;
using UnityEngine.Serialization;

public abstract class BasePopup : MonoBehaviour
{
    [FormerlySerializedAs("popupName")] [Header("Popup 타입")]
    public PopupType popupType;

    protected virtual void Start()
    {
        if (PopupDirector.i != null && !PopupDirector.i.list_Popup.Contains(this))
            PopupDirector.i.list_Popup.Add(this);

        // 시작 시 비활성화
        gameObject.SetActive(false);
    }

    public virtual void Show()
    {
        gameObject.SetActive(true);
        Debug.Log($"[Popup] {popupType} 열림");
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
        Debug.Log($"[Popup] {popupType} 닫힘");
    }
}