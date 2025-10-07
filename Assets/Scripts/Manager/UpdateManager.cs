using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = System.Random;

public class UpdateManager : MonoBehaviour
{
    public static UpdateManager i;

    private void Awake()
    {
        i = this;
    }
    void Update()
    {
        if (Pointer.current != null)
        {
            // 클릭/터치가 시작됐을 때
            if (Pointer.current.press.wasReleasedThisFrame)
            {
                Vector2 screenPos = Pointer.current.position.ReadValue();
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, 10f) // z는 카메라와의 거리
                );

                if (BoxManager.i.IsInBox(worldPos))
                {
                    Debug.Log("inside the box. pos : " + worldPos.ToString());
                    PowerManager.i.ExplodeAt(worldPos);
                }
                else if(BallManager.i.isNowBallWaiting == false)
                {
                    worldPos.y = BallManager.i.height_Top; // 높이를 0으로 고정
                    Debug.Log("outside the box. pos : " + worldPos.ToString());

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
                BallManager.i.trf_guideLine.position = worldPos + new Vector3(0, -2.61f, 0);
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
        }else if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            GameManager.i.GameFail();
        }else if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            GameManager.i.GameRestart();
        }else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            GameManager.i.GameStart();
        }
        
    }

    public Vector3 GetClampedWorldPos()
    {
        return GetClampedWorldPos(Pointer.current.position.ReadValue(),
            BallManager.i.GetBallSize(1),
            BallManager.i.pos_TopTop.position.y);
    }
    public static Vector3 GetClampedWorldPos(Vector2 screenPos, float ballSize, float heightTop)
    {
        // 스크린 좌표 → 월드 좌표 변환
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, 10f) // 카메라 거리 고정
        );

        // y축 고정 (예: BallManager.i.height_Top)
        worldPos.y = heightTop;

        // BoxManager 기준으로 X 좌표 보정
        float leftLimit = BoxManager.i.pos_TopLeft.x + (ballSize * 0.5f);
        float rightLimit = BoxManager.i.pos_BottomRight.x - (ballSize * 0.5f);

        if (worldPos.x < leftLimit)
            worldPos.x = leftLimit + 0.01f;
        else if (worldPos.x > rightLimit)
            worldPos.x = rightLimit - 0.01f;

        return worldPos;
    }
}
