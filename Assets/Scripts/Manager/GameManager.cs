using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager i;

    private void Awake()
    {
        i = this;
    }

    private void Start()
    {
        GameStart();
    }

    public void GameStart()
    {
        GameReset();
        BallManager.i.RenewIconNextFruit();
        BallManager.i.DoCreateBall(UpdateManager.i.GetClampedWorldPos(), 1);
    }

    public void GameReset()
    {
        for (int i = BallManager.i.list_BallState.Count - 1; i >= 0; i--)
        {
            Destroy(BallManager.i.list_BallState[i].gameObject);
        }
        BallManager.i.list_BallState.Clear();
        BallManager.i.ballIndex = 0;
        BallManager.i.nextBallLevel = 1;

        ScoreManager.i.nowScore = 0;
        ScoreManager.i.RenewScoreText();

        PowerManager.i.powerCount = 3;
        PowerManager.i.RenewPowerLight();
        PowerManager.i.powerChargingCount = 0;
        PowerManager.i.text_ChargingCount.text = "00/30";
    }

    public void GameEnd()
    {
        for (int i = 0; i < BallManager.i.list_BallState.Count; i++)
        {
            Rigidbody2D rb = BallManager.i.list_BallState[i].GetComponent<Rigidbody2D>();
            
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        
        ScoreManager.i.CompareNowScore();
    }
    public void GameFail()
    {
        GameEnd();
        PopupDirector.i.Show(PopupType.GAME_RESULT);
    }
    public void GameClear()
    {
        GameEnd();
    }

    public void GameRestart()
    {
        //GameEnd를 거치지 않고 바로 GameStart로 넘어갑니다
        PopupDirector.i.HideAll();
        GameStart();
    }

    public void GameQuit()
    {
        
    }
}
