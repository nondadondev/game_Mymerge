using System.Collections.Generic;
using UnityEngine;

public enum PopupType
{
    SETTINGS,
    GAME_RESULT,
}

public class PopupDirector : MonoBehaviour
{
    public static PopupDirector i;

    [Header("등록된 팝업들")]
    public List<BasePopup> list_Popup = new List<BasePopup>();
    private Stack<BasePopup> stack_OpenPopup = new Stack<BasePopup>();

    public bool isAnyPopupOpen = false;
    
    private void Awake()
    {
        if (i == null) i = this;
        else Destroy(gameObject);
    }

    // 팝업 열기
    public void Show(PopupType popupType)
    {
        BasePopup popup = GetPopup(popupType);
        if (popup == null)
        {
            Debug.LogWarning($"[PopupDirector] '{popupType}' 팝업을 찾을 수 없습니다.");
            return;
        }

        isAnyPopupOpen = true;
        popup.Show();
        stack_OpenPopup.Push(popup);
    }

    // 팝업 닫기
    public void Hide(PopupType popupType)
    {
        BasePopup popup = GetPopup(popupType);
        if (popup == null)
        {
            Debug.LogWarning($"[PopupDirector] '{popupType}' 팝업을 찾을 수 없습니다.");
            return;
        }

        popup.Hide();

        // 스택에서도 제거
        if (stack_OpenPopup.Contains(popup))
        {
            var temp = new Stack<BasePopup>(stack_OpenPopup);
            stack_OpenPopup.Clear();
            foreach (var p in temp)
            {
                if (p != popup)
                    stack_OpenPopup.Push(p);
            }
        }
    }

    // 가장 최근 팝업 닫기
    public void HideTop()
    {
        if (stack_OpenPopup.Count == 0) return;

        BasePopup top = stack_OpenPopup.Pop();
        top.Hide();
    }

    // 모든 팝업 닫기
    public void HideAll()
    {
        foreach (var popup in list_Popup)
            popup.Hide();

        stack_OpenPopup.Clear();
    }

    private BasePopup GetPopup(PopupType popupType)
    {
        return list_Popup.Find(p => p.popupType == popupType);
    }

    public void TestIsAnyPopupOpen()
    {
        isAnyPopupOpen = false;
        for (int i = 0; i < list_Popup.Count; i++)
        {
            if (list_Popup[i].isPopupOpen)
            {
                isAnyPopupOpen = true;
                break;
            }
        }
    }
}