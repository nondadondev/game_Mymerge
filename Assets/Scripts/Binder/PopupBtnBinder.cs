using UnityEngine;

public class PopupBtnBinder : MonoBehaviour
{
    public PopupType popupType;

    public void Show()
    {
        PopupDirector.i.Show(popupType);
    }
    public void Hide()
    {
        PopupDirector.i.Hide(popupType);
    }
}
