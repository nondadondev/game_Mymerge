using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class BallState : MonoBehaviour
{
    [Header("Ball State")]
    public int ballLevel = 1;
    public int ballIndex = 0;

    [Header("Flags")]
    public bool isMergeSet = false;   // 병합 예약
    public bool isGrowingUp = false;  // 성장 연출 중
    public bool isSpawnUp = false;
    public bool isSetOnTop = false;
    public int countColliderSound = 0;

    [Header("Ball State")] 
    public SpriteRenderer spr_effect;
    
    [FormerlySerializedAs("Transform_00")] [Header("Refs")]
    public Transform transform_00;
    public SpriteRenderer spr;
    public CircleCollider2D circleCollider2D;
    public Rigidbody2D rigidbody2D;

    private void OnEnable()
    {
        BallManager.i.AddListBall(this);
    }

    private void OnDisable()
    {
        if (BallManager.i != null) BallManager.i.RemoveListBall(this);
    }

    public void RenewSize()
    {
        RenewSize(ballLevel, 1f);
    }

    public void RenewSize(int level, float ratio)
    {
        float size = BallManager.i.GetBallSize(level) * ratio;
        RenewColor(level);
        transform_00.localScale = new Vector3(size, size, 1f);
    }

    public void RenewColor() { RenewColor(ballLevel); }
    public void RenewColor(int level) { spr.sprite = BallSpriteStorage.i.GetFruitSprite(level); }

    public void DoRecoverSize()
    {
        StartCoroutine(RecoverSize());
    }

    private IEnumerator RecoverSize()
    {
        // 성장 팝 애니메이션
        Vector3 targetScale = Vector3.one * BallManager.i.GetBallSize(ballLevel);
        Vector3 startScale = targetScale * 0.5f;

        float timer = 0f;
        float moveTime = 0.1f;
        
        //새공 크기 확대
        SoundManager.i.PlayBallSound(ballLevel);
        while (timer < moveTime)
        {
            timer += Time.deltaTime;
            float t = timer / moveTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
        isGrowingUp = false;
    }
    
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 태그 빠르게 필터
        if (!collision.collider.CompareTag("Ball") && countColliderSound < 2)
        {
            countColliderSound++;
            float fastest = Mathf.Max(rigidbody2D.linearVelocity.magnitude, rigidbody2D.linearVelocity.magnitude);

            if (fastest > BallManager.i.speed_f)
            {
                SoundManager.i.PlaySFX(SoundType.POP, 0.45f);
                HapticManager.i.PlayOneShot(10, 100);
            }
            else if (fastest > BallManager.i.speed_m)
            {
                SoundManager.i.PlaySFX(SoundType.POP, 0.2f);
                HapticManager.i.PlayOneShot(10, 200);
            }
            return;
        }

        if (isGrowingUp || isSetOnTop || isSpawnUp) return;
        
        if (collision.collider.gameObject.CompareTag("Ball") == false) return;
        BallState other = collision.collider.GetComponent<BallState>();
        if (other == null) return;
        if (other.isSpawnUp == true || other.isSetOnTop == true) return;
        
        BallState myState = this;
        BallState otherState = collision.collider.GetComponent<BallState>();

        if (otherState == null)
        {
            Debug.LogWarning("상대 BallState 누락");
            return;
        }

        BallManager.i.DoMergeBall(myState, otherState);
    }

    public bool IsMovingFast(float threshold)
    {
        // Rigidbody2D는 일반적으로 velocity 사용
        return rigidbody2D.linearVelocity.magnitude > threshold;
    }
}
