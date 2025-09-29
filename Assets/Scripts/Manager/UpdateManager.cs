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

                    if (BallManager.i.ballIndex == 0)
                    {
                        BallManager.i.CreateBall(worldPos, 1);
                    }
                    else if (BallManager.i.ballIndex < 5)
                    {
                        BallManager.i.CreateBall(worldPos, BallManager.i.ballIndex);
                    }
                    else if (BallManager.i.ballIndex > 35)
                    {
                        int targetLevel = UnityEngine.Random.Range(1, 6);
                        BallManager.i.CreateBall(worldPos, targetLevel);
                    }
                    else
                    {
                        int targetLevel = UnityEngine.Random.Range(1, 5);
                        BallManager.i.CreateBall(worldPos, targetLevel);
                    }
                }
            }
            
            // === 마우스를 따라다니는 공 이동 처리 ===
            if (BallManager.i.nowBall != null) //<<== 조건 이상함. nowBall은 언제든 사라지고 다시 태어남
            {
                Vector2 screenPos = Pointer.current.position.ReadValue();
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(screenPos.x, screenPos.y, 10f) // 카메라 거리 유지
                );
                worldPos.y = BallManager.i.height_Top; // 높이를 0으로 고정
                if (worldPos.x < BoxManager.i.pos_TopLeft.x + (BallManager.i.nowBallSize * 0.5f))
                {
                    worldPos.x = BoxManager.i.pos_TopLeft.x + (BallManager.i.nowBallSize * 0.5f) + 0.01f;
                }
                else if(worldPos.x > BoxManager.i.pos_BottomRight.x - (BallManager.i.nowBallSize * 0.5f))
                {
                    worldPos.x = BoxManager.i.pos_BottomRight.x - (BallManager.i.nowBallSize * 0.5f) - 0.01f;
                }
                BallManager.i.nowBall.position = worldPos;
                BallManager.i.trf_guideLine.position = worldPos + new Vector3(0, -2.9f, 0);
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
        
    }
}
