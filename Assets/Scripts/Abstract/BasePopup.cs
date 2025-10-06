using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public abstract class BasePopup : MonoBehaviour
{
    [FormerlySerializedAs("popupName")] [Header("Popup 타입")]
    public PopupType popupType;

    protected CanvasGroup canvasGroup;
    [Header("공통 버튼")]
    [SerializeField] private Button btn_Close;
    
    protected virtual void Start()
    {
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // PopupDirector에 1차 자동 등록 (디렉터가 이미 준비된 경우)
        if (PopupDirector.i != null && PopupDirector.i.list_Popup.Contains(this) == false)
        {
            PopupDirector.i.list_Popup.Add(this);
        }

        // Close 버튼 연결
        if (btn_Close != null)
            btn_Close.onClick.AddListener(Hide);
        
        // 시작 시 숨김 처리 (SetActive(false) 대신 CanvasGroup만으로 감춤)
        HideImmediate();
    }

    protected virtual void OnEnable()
    {
        // 실행 순서에 따라 Awake 시점에 Director가 아직 없을 수 있음 → 재시도
        if (PopupDirector.i != null && PopupDirector.i.list_Popup.Contains(this) == false)
        {
            PopupDirector.i.list_Popup.Add(this);
        }
    }

    protected virtual void OnDestroy()
    {
        // 파괴 시 목록 정리 (메모리/참조 누수 방지)
        if (PopupDirector.i != null)
        {
            PopupDirector.i.list_Popup.Remove(this);
        }
    }

    public virtual void Show()
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("[Popup] CanvasGroup이 없습니다. RequireComponent가 적용되어야 합니다.");
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Debug.Log($"[Popup] {popupType} 열림");
    }

    public virtual void Hide()
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("[Popup] CanvasGroup이 없습니다. RequireComponent가 적용되어야 합니다.");
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Debug.Log($"[Popup] {popupType} 닫힘");
    }

    // SetActive(false) 대신 초기 비활성화용
    protected void HideImmediate()
    {
        if (canvasGroup == null)
        {
            // 혹시 Awake 순서 꼬임 대비
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
