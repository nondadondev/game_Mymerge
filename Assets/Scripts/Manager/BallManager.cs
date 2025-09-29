using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public class BallManager : MonoBehaviour
{
    public static BallManager i;

    public Transform trf_Box;
    public Transform trf_WallRoot;

    public float boxSize = 1f;
    
    [Header("공")]
    public GameObject prf_Ball;
    public Transform nowBall;
    public bool isNowBallWaiting;

    public float nowBallSize = 0.2f;
    
    [FormerlySerializedAs("pos_Top")] [Header("공 보조")]
    public Transform pos_TopTop;
    public float height_Top;
    [FormerlySerializedAs("trf_guide")] public Transform trf_guideLine;

    [Header("Indexing")]
    public int sampleLevel = 0;
    public int ballIndex = 0;

    [Header("소리 기준 속도")]
    public float speed_f = 1.5f; // 빠름
    public float speed_m = 0.7f; // 보통

    [Header("Tracking")]
    public List<BallState> list_BallState = new List<BallState>();
    private HashSet<int> set_Indices = new HashSet<int>(); // O(1) 존재 체크

    private void Awake()
    {
        i = this;
        height_Top = pos_TopTop.position.y;
        trf_WallRoot.localScale = Vector3.one * boxSize;
        nowBall.localScale = nowBall.localScale * boxSize;
    }

    public void CreateBall(Vector3 pos, int level)
    {
        StartCoroutine(DoCreateBall(pos, level));
    }

    private IEnumerator DoCreateBall(Vector3 pos, int level)
    {
        Debug.Log("nowball is = " + nowBall.gameObject.name);
        nowBall.gameObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        isNowBallWaiting = true;
        nowBall = null;
        
        yield return Timer.i.wait_sec_pointOne;
        yield return Timer.i.wait_sec_pointOne;
        yield return Timer.i.wait_sec_pointOne;
        
        GameObject obj = Instantiate(prf_Ball, pos, Quaternion.identity);
        obj.transform.parent = trf_Box;
        BallState state = obj.GetComponent<BallState>();
        state.isSpawnUp = true;
        state.rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        nowBall = obj.transform;
        nowBallSize = GetBallSize(level);
        isNowBallWaiting = false;
        state.ballLevel = level;
        state.isGrowingUp = true;
        state.RenewSize(state.ballLevel, 0.5f); // 0.5에서 팽창 연출 시작
        
        state.ballIndex = ballIndex++;
        state.gameObject.name = "ball_" + state.ballIndex;
        // 성장 팝 애니메이션
        Vector3 targetScale = Vector3.one * GetBallSize(level);
        Vector3 startScale = targetScale * 0.5f;

        float timer = 0;
        float moveTime = 0.2f;
        
        timer = 0f;
        //새 공 크기 확대
        while (timer < moveTime)
        {
            timer += Time.deltaTime;
            float t = timer / moveTime;
            state.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        state.transform.localScale = targetScale;
        state.isGrowingUp = false;
        state.isSpawnUp = false;
    }

    public float GetBallSize(int level)
    {
        // 레벨1: 1.00
        // 레벨2: 1.50
        // 레벨3: 2.10
        // 레벨4: 2.30
        // 레벨5: 2.90
        // 레벨6: 3.50
        // 레벨7: 3.70
        // 레벨8: 5.00
        // 레벨9: 5.90
        // 레벨10: 6.00
        // 레벨11: 7.80
        float baseLevel1 = 0.2f;
        switch (level)
        {
            default: return 1f * boxSize;
            case 1: return baseLevel1 * boxSize * 1;
            case 2: return baseLevel1 * boxSize * 1.5f;
            case 3: return baseLevel1 * boxSize * 2.1f;
            case 4: return baseLevel1 * boxSize * 2.3f;
            case 5: return baseLevel1 * boxSize * 2.9f;
            case 6: return baseLevel1 * boxSize * 3.5f;
            case 7: return baseLevel1 * boxSize * 3.7f;
            case 8: return baseLevel1 * boxSize * 5.0f;
            case 9: return baseLevel1 * boxSize * 5.9f;
            case 10: return baseLevel1 * boxSize * 6.0f;
            case 11: return baseLevel1 * boxSize * 7.8f;
        }
    }

    public Color GetBallColor(int level)
    {
        // Unity 내장에 없는 색은 직접 정의
        // orange(1,0.5,0), purple(0.5,0,0.5), chartreuse(0.5,1,0)
        switch (level)
        {
            default: return Color.white;
            case 1: return Color.red;
            case 2: return new Color(1f, 0.5f, 0f);        // orange
            case 3: return Color.yellow;
            case 4: return Color.cyan;
            case 5: return Color.blue;
            case 6: return Color.magenta;
            case 7: return new Color(0.5f, 0f, 0.5f);      // purple
            case 8: return new Color(0.5f, 1f, 0f);        // chartreuse
            case 9: return Color.black;        // chartreuse
            case 10: return Color.gray;        // chartreuse
            case 11: return Color.white;        // chartreuse
        }
    }

    public void AddListBall(BallState state)
    {
        // HashSet으로 중복 O(1) 체크
        if (set_Indices.Add(state.ballIndex))
        {
            list_BallState.Add(state);
        }
    }

    public void RemoveListBall(BallState state)
    {
        if (set_Indices.Remove(state.ballIndex))
        {
            list_BallState.Remove(state);
        }
    }

    public void DoMergeBall(BallState stateA, BallState stateB)
    {
        // 중복/동시 처리 방지
        if (stateA.isMergeSet || stateB.isMergeSet) return;

        // 레벨 다르면 사운드만 (속도 조건에 따라)
        if (stateA.ballLevel != stateB.ballLevel)
        {
            // 둘 중 더 빠른 쪽으로 판단
            float fastest = Mathf.Max(stateA.rigidbody2D.linearVelocity.magnitude, stateB.rigidbody2D.linearVelocity.magnitude);

            if (fastest > speed_f)
            {
                SoundManager.i.PlaySFX(SoundType.MARIMBA, 1f);
            }
            else if (fastest > speed_m)
            {
                SoundManager.i.PlaySFX(SoundType.MARIMBA, 0.45f);
            }
            else
            {
                // 개발 중에만 보고 싶다면 주석 처리 권장
                // Debug.Log($"Fail. speed = {fastest:0.00} m/s");
            }
            return;
        }

        // 병합 예약
        stateA.isMergeSet = true;
        stateB.isMergeSet = true;

        StartCoroutine(MergeBall(stateA, stateB));
    }

    private IEnumerator MergeBall(BallState stateA, BallState stateB)
    {
        int targetLevel = stateA.ballLevel + 1;
        float timer = 0f;
        float moveTime = 0.1f;

        if (isNowBallWaiting == false)
        {
            BallState nowBallState = nowBall.GetComponent<BallState>();
            if (stateA == nowBallState || stateB == nowBallState)
            {
                CreateBall(nowBall.transform.position, BallManager.i.ballIndex + 1);
            }
        }
        
        Transform ta = stateA.transform;
        Transform tb = stateB.transform;

        // 월드 좌표로 통일
        Vector3 startPosA = ta.position;
        Vector3 startPosB = tb.position;
        Vector3 targetPoint = (startPosA + startPosB) * 0.5f;

        // 머지 중 물리 간섭 최소화
        var rbA = stateA.rigidbody2D;
        var rbB = stateB.rigidbody2D;
        var colA = stateA.circleCollider2D;
        var colB = stateB.circleCollider2D;

        bool wasKinematicA = rbA.isKinematic;
        bool wasKinematicB = rbB.isKinematic;
        float gravA = rbA.gravityScale;
        float gravB = rbB.gravityScale;

        rbA.isKinematic = true; rbB.isKinematic = true;
        rbA.linearVelocity = Vector2.zero; rbB.linearVelocity = Vector2.zero;
        rbA.angularVelocity = 0f; rbB.angularVelocity = 0f;
        rbA.gravityScale = 0f; rbB.gravityScale = 0f;
        colA.enabled = false; colB.enabled = false;

        //두개의 공이 가운데로 이동
        while (timer < moveTime)
        {
            timer += Time.deltaTime;
            float t = timer / moveTime;

            ta.position = Vector3.Lerp(startPosA, targetPoint, t);
            tb.position = Vector3.Lerp(startPosB, targetPoint, t);

            yield return null;
        }

        // 원본 제거
        RemoveListBall(stateA);
        RemoveListBall(stateB);
        Destroy(ta.gameObject);
        Destroy(tb.gameObject);

        // 새 공 생성 (처음부터 targetPoint에)
        GameObject obj = Instantiate(prf_Ball, targetPoint, Quaternion.identity);
        obj.transform.parent = trf_Box;
        BallState stateC = obj.GetComponent<BallState>();
        stateC.ballLevel = targetLevel;
        stateC.isGrowingUp = true;
        stateC.RenewSize(stateC.ballLevel, 0.5f); // 0.5에서 팽창 연출 시작
        stateC.gameObject.name = "ballM_" + stateC.ballIndex;

        stateC.DoRecoverSize();
    }
}
