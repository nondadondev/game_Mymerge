using System;
using UnityEngine;

public class ScreenTester : MonoBehaviour
{
    public static ScreenTester i;

    private void Awake()
    {
        i = this;
    }

    public Transform anchor_TopLeft;
    public Transform anchor_BottomRight;

    public float GetWidth()
    {
        return anchor_BottomRight.position.x - anchor_TopLeft.position.x;
    }

    public float GetHeight()
    {
        return anchor_TopLeft.position.y - anchor_BottomRight.position.y;
    }
    
    public float GetScreenRatio()
    {
        return GetHeight() / GetWidth();
    }
}
