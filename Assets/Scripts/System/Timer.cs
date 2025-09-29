using System;
using UnityEngine;

public class Timer : MonoBehaviour
{
    public static Timer i;

    private void Awake()
    {
        i = this;
    }
    // 다음 프레임 끝까지 대기
    public WaitForEndOfFrame wait_nextFrame = new WaitForEndOfFrame();
    public WaitForSeconds wait_sec_zero = new WaitForSeconds(0f);
    public WaitForSeconds wait_sec_pointOne = new WaitForSeconds(0.1f);
}
