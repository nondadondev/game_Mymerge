using UnityEngine;

public class Popup_Settings : BasePopup
{
    public void OnClickBgmToggle()
    {
        Debug.Log("BGM 설정 토글!");
    }

    public void OnClickClose()
    {
        Hide();
    }
}