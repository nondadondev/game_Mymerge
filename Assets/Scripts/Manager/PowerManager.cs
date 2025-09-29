using System;
using UnityEngine;

public class PowerManager : MonoBehaviour
{
    public static PowerManager i;

    [Header("Explosion Settings")]
    [SerializeField] private float radius = 3f;                  // 폭발 반경
    [SerializeField] private float force = 20f;                  // 기본 임펄스 힘
    [SerializeField] private bool useDistanceFalloff = true;     // 거리 감쇠 사용 여부
    [SerializeField] private AnimationCurve falloff =            // 0(가까움)~1(멀리) 구간의 감쇠 커브
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Filter")]
    [SerializeField] private LayerMask targetMask;               // 타겟 레이어(예: Ball 레이어)
    [SerializeField] private string requiredTag = "Ball";        // 필요한 태그(공)

    [Header("Physics")]
    [SerializeField] private ForceMode2D forceMode = ForceMode2D.Impulse; // 임펄스 적용
    [SerializeField] private float maxSpeedClamp = 0f;           // 0이면 제한 없음, 그 외에는 속도 상한
    [SerializeField] private float randomAngleJitter = 0f;       // 방향에 살짝 랜덤성(도 단위)

    [Header("Quality/Debug")]
    [SerializeField] private int maxHits = 128;                  // 한 번에 처리할 최대 콜라이더 수
    [SerializeField] private bool drawGizmos = true;             // 반경 시각화

    private Camera mainCam;
    private Collider2D[] hitsBuffer;

    private void Awake()
    {
        i = this;
        
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("Main Camera를 찾을 수 없습니다. Camera.main이 필요합니다.");
        }
        hitsBuffer = new Collider2D[maxHits];
    }


    private Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (mainCam == null)
        {
            return Vector3.zero;
        }
        // Z는 카메라와의 거리. 2D 오쏘그래픽이면 무시되지만 10 정도 주면 안전
        Vector3 sp = new Vector3(screenPos.x, screenPos.y, 10f);
        return mainCam.ScreenToWorldPoint(sp);
    }

    /// <summary>
    /// worldPos를 중심으로 반경 내의 Rigidbody2D들에게 방사형 힘 적용
    /// </summary>
    public void ExplodeAt(Vector3 worldPos)
    {
        int count = Physics2D.OverlapCircleNonAlloc(worldPos, radius, hitsBuffer, targetMask);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitsBuffer[i];
            if (col == null)
            {
                continue;
            }

            // 태그 필터(설정해둔 requiredTag가 비어있다면 패스)
            if (string.IsNullOrEmpty(requiredTag) == false)
            {
                if (col.CompareTag(requiredTag) == false)
                {
                    continue;
                }
            }

            Rigidbody2D rb = col.attachedRigidbody;
            if (rb == null)
            {
                continue;
            }

            // 방향: 폭발 중심 -> 공
            Vector2 toTarget = (Vector2)rb.worldCenterOfMass - (Vector2)worldPos;
            float dist = toTarget.magnitude;

            if (dist == 0f)
            {
                // 동일 좌표에 겹쳐 있을 수 있음: 임의 방향 부여
                toTarget = UnityEngine.Random.insideUnitCircle.normalized;
                dist = 0.0001f;
            }

            Vector2 dir = toTarget.normalized;

            // 방향 지터(선택): 너무 완벽한 방사형이 딱딱해 보일 때
            if (randomAngleJitter > 0f)
            {
                float jitter = UnityEngine.Random.Range(-randomAngleJitter, randomAngleJitter);
                float rad = jitter * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                dir = new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
            }

            // 거리 감쇠
            float t = Mathf.Clamp01(dist / radius); // 0(가까움)~1(멀리)
            float fall = 1f;
            if (useDistanceFalloff == true)
            {
                fall = falloff.Evaluate(t);
            }

            Vector2 impulse = dir * force * fall;

            rb.AddForce(impulse, forceMode);

            // 속도 상한(선택)
            if (maxSpeedClamp > 0f)
            {
                float spd = rb.linearVelocity.magnitude;
                if (spd > maxSpeedClamp)
                {
                    rb.linearVelocity = rb.linearVelocity.normalized * maxSpeedClamp;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmos == false)
        {
            return;
        }
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawSphere(transform.position, 0.04f);

        // 에디터에서 마우스 위치를 알 수 없으니, 컴포넌트 위치 기준의 반경만 시각화
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
