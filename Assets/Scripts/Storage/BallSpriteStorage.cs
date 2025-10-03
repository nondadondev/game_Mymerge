using System;
using UnityEngine;

public class BallSpriteStorage : MonoBehaviour
{
    public static BallSpriteStorage i;

    private void Awake() { i = this; }

    public Sprite fruit_01;
    public Sprite fruit_02;
    public Sprite fruit_03;
    public Sprite fruit_04;
    public Sprite fruit_05;
    public Sprite fruit_06;
    public Sprite fruit_07;
    public Sprite fruit_08;
    public Sprite fruit_09;
    public Sprite fruit_10;
    public Sprite fruit_11;

    public Sprite GetFruitSprite(int level)
    {
        switch (level)
        {
            default: return fruit_01;
            case 1: return fruit_01;
            case 2: return fruit_02;
            case 3: return fruit_03;
            case 4: return fruit_04;
            case 5: return fruit_05;
            case 6: return fruit_06;
            case 7: return fruit_07;
            case 8: return fruit_08;
            case 9: return fruit_09;
            case 10: return fruit_10;
            case 11: return fruit_11;
        }
    }
}