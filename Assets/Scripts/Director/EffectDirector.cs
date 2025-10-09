using System;
using UnityEngine;

public class EffectDirector : MonoBehaviour
{
    public static EffectDirector i;

    private void Awake()
    {
        i = this;
    }

    [Header("클릭 이펙트 프리팹")]
    public GameObject prf_ClickEffect;

    public void Act_ClickEffect(Vector2 clickPosition)
    {
        GameObject effect = Instantiate(prf_ClickEffect, clickPosition, Quaternion.identity);

        Destroy(effect, 0.4f);
    }
}