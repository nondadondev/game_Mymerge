using System;
using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    public static ButtonManager i;

    private void Awake()
    {
        i = this;
    }

    public void Act_BtnStart()
    {
        GameManager.i.GameStart();
        PopupDirector.i.Hide(PopupType.SETTINGS);
        PopupDirector.i.Hide(PopupType.GAME_RESULT);
    } 
    public void Act_ReStart()
    {
        GameManager.i.GameStart();
        PopupDirector.i.Hide(PopupType.SETTINGS);
        PopupDirector.i.Hide(PopupType.GAME_RESULT);
    } 
}
