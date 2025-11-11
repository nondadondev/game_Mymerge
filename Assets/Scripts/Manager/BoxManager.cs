using System;
using UnityEngine;
using UnityEngine.Serialization;

public class BoxManager : MonoBehaviour
{
    public static BoxManager i;

    private void Awake()
    {
        i = this;
    }

    public Transform anchor_BoxTopTop;
    public Transform anchor_TopLeft;
    public Transform anchor_BottomRight;

    public Transform trf_Box;

    public Vector3 pos_BoxTopLeft;
    public Vector3 pos_BoxBottomRight;

    public void Start()
    {
        trf_Box.localScale = new Vector3(ScreenTester.i.GetWidth() / 5, ScreenTester.i.GetWidth() / 5, 1);
        pos_BoxTopLeft = anchor_TopLeft.transform.position;
        pos_BoxBottomRight = anchor_BottomRight.transform.position;
        
        BallManager.i.height_Top = anchor_BoxTopTop.position.y;
        BallManager.i.boxSize = trf_Box.localScale.x;
        BallManager.i.nowBall.localScale *= BallManager.i.boxSize;
    }

    public bool IsInBox(Vector3 pos)
    {
        if (pos.y <= pos_BoxTopLeft.y && pos.y >= pos_BoxBottomRight.y &&
            pos.x >= pos_BoxTopLeft.x && pos.x <= pos_BoxBottomRight.x)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
