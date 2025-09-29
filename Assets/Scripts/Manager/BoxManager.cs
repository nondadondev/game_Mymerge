using System;
using UnityEngine;

public class BoxManager : MonoBehaviour
{
    public static BoxManager i;

    private void Awake()
    {
        i = this;
    }

    public Transform anchor_TopLeft;
    public Transform anchor_BottomRight;

    public Vector3 pos_TopLeft;
    public Vector3 pos_BottomRight;

    public void Start()
    {
        pos_TopLeft = anchor_TopLeft.transform.position;
        pos_BottomRight = anchor_BottomRight.transform.position;
    }

    public bool IsInBox(Vector3 pos)
    {
        if (pos.y <= pos_TopLeft.y && pos.y >= pos_BottomRight.y &&
            pos.x >= pos_TopLeft.x && pos.x <= pos_BottomRight.x)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
