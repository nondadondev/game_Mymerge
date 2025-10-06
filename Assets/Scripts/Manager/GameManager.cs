using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager i;

    private void Awake()
    {
        i = this;
    }

    public void GameStart()
    {
        
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
