using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager i;

    public GameObject loadingImageGroup;
    private void Awake()
    {
        i = this;
    }

    private void Start()
    {
        Debug.Log("screen ratio = "  + ScreenTester.i.GetScreenRatio());

        StartCoroutine(DoStart());
    }

    IEnumerator DoStart()
    {
        yield return Timer.i.wait_sec_pointOne;

        float moveTime = 1;
        float timer = 0;

        while (timer < moveTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        
        loadingImageGroup.SetActive(false);
        SoundManager.i.PlayBGM(0, true, 0.4f);
    }
}
