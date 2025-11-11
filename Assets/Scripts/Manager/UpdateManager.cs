using System;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Random = System.Random;

public class UpdateManager : MonoBehaviour
{
    public static UpdateManager i;
    private Camera _camera;
    private EventSystem _eventSystem;
    private void Awake()
    {
        i = this;
        _camera = Camera.main;
        _eventSystem  = EventSystem.current;
    }
    
    
    void Update()
    {
        if (Pointer.current != null)
        {            
            if (EventSystem.current.IsPointerOverGameObject())
                return; // ← UI 클릭이면 아래 로직 실행 안 함
            if (PopupDirector.i.isAnyPopupOpen)
            {
                return;
            }
            
            // 클릭/터치가 시작됐을 때
            if (Pointer.current.press.wasReleasedThisFrame)
            {
                Vector2 screenPos = Pointer.current.position.ReadValue();
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, 10f) // z는 카메라와의 거리
                );

                if (BoxManager.i.IsInBox(worldPos))
                {
                    if (PowerManager.i.powerCount > 0)
                    {
                        PowerManager.i.ExplodeAt(worldPos);
                    }
                    else
                    {
                        PowerManager.i.AlarmNoPowerCount();
                    }
                }
                else if(BallManager.i.isNowBallWaiting == false)
                {
                    worldPos.y = BallManager.i.height_Top; // 높이를 0으로 고정

                    BallManager.i.PushBall(worldPos);
                }
            }
            
            // === 마우스를 따라다니는 공 이동 처리 ===
            if (BallManager.i.nowBall != null)
            {
                Vector2 screenPos = Pointer.current.position.ReadValue();
                Vector3 worldPos = GetClampedWorldPos(
                    screenPos,
                    BallManager.i.nowBallSize,
                    BallManager.i.height_Top
                );

                BallManager.i.nowBall.position = worldPos;
                BallManager.i.trf_guideLine.position = worldPos;
            }
        }
        
        if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
        {
            if (BallManager.i.sampleLevel > 1)
            {
                BallManager.i.sampleLevel--;
            }
        }
        else if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
        {
            if (BallManager.i.sampleLevel < 8)
            {
                BallManager.i.sampleLevel++;
            }
        }else if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Time.timeScale = 1;
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Time.timeScale = 0.1f;
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Time.timeScale = 0.01f;
        }
        else if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            PopupDirector.i.Show(PopupType.SETTINGS);
        }
        else if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            PopupDirector.i.Hide(PopupType.SETTINGS);
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame)
        {
            Debug.Log("screen ratio = "  + ScreenTester.i.GetScreenRatio());
        }else if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            GameManager.i.GameFail();
        }else if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            GameManager.i.GameRestart();
        }else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            GameManager.i.GameStart();
        }else if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            BallManager.i.PunchBall();
        }
        
    }

    public Vector3 GetClampedWorldPos()
    {
        return GetClampedWorldPos(Pointer.current.position.ReadValue(),
            BallManager.i.GetBallSize(1),
            BoxManager.i.anchor_BoxTopTop.position.y);
    }
    public Vector3 GetClampedWorldPos(Vector2 screenPos, float ballSize, float heightTop)
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, 10f) // 카메라 거리 고정
        );
        // BoxManager 기준으로 X 좌표 보정
        float leftLimit = BoxManager.i.pos_BoxTopLeft.x + ((ballSize * 0.5f)*BoxManager.i.trf_Box.localScale.x);
        float rightLimit = BoxManager.i.pos_BoxBottomRight.x - ((ballSize * 0.5f)*BoxManager.i.trf_Box.localScale.x);
        
        worldPos.y = heightTop;
        
        if (BallManager.i.isFirstTouch == false)
        {
            if (worldPos.x < leftLimit)
                worldPos.x = leftLimit + 0.01f;
            else if (worldPos.x > rightLimit)
                worldPos.x = rightLimit - 0.01f;

            return worldPos;
        }
        else
        {
            if (worldPos.x >= leftLimit && worldPos.x <= rightLimit)
            {
                BallManager.i.isFirstTouch = false;
            }

            worldPos.x = 0;
            return worldPos;
        }
    }
    
}
