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

    public void GameFail()
    {
        
    }
    public void GameClear()
    {
        
    }

    public void GameRestart()
    {
        
    }

    public void GameQuit()
    {
        
    }
}
