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

        ScoreManager.i.nowScore = 0;
        ScoreManager.i.RenewScoreText();
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
        
    }

    public void GameQuit()
    {
        
    }
}
