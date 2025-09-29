using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class TMPDynamicPrewarm : MonoBehaviour
{
    [Header("예열 대상 TMP 폰트에 Dynamic이 켜져 있어야 합니다.")]
    public TMP_FontAsset targetFont;

    [Tooltip("예열에 사용할 문자열 텍스트들(TextAsset). Localization CSV/JSON 등")]
    public List<TextAsset> seedTextAssets;

    [Tooltip("직접 넣을 추가 문자열(개행 OK)")]
    [TextArea(3, 10)] public string manualSeed = 
@"확인 취소 시작 종료 설정 옵션 도움말 가이드 예 아니요 다음 이전 완료 성공 실패 알림
로그인 로그아웃 구매 결제 환불 복원 무료 리워드 광고 상점 아이템 점수 콤보 스테이지 레벨 단계 클리어 재도전
소리 음악 음향 볼륨 진동 켜기 끄기 그래픽 품질 낮음 보통 높음 매우높음
데이터 저장 동기화 복구 초기화 정말로 계속하시겠습니까?
0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz ~`!@#$%^&*()-_=+[]{}\\|;:'"",.<>/? … – — ‘ ’ “ ” ₩ ° ×";

    [Tooltip("NBSP(U+00A0) 같은 특수 공백 포함")]
    public bool includeNBSP = true;

    void Start()
    {
        if (targetFont == null)
        {
            Debug.LogWarning("[TMPDynamicPrewarm] targetFont가 비어있음");
            return;
        }

        // 1) 문자열 수집
        var sb = new StringBuilder();
        if (seedTextAssets != null)
        {
            foreach (var ta in seedTextAssets)
            {
                if (ta != null) sb.AppendLine(ta.text);
            }
        }
        if (!string.IsNullOrEmpty(manualSeed))
        {
            sb.AppendLine(manualSeed);
        }
        if (includeNBSP) sb.Append('\u00A0'); // NBSP

        string all = sb.ToString();

        // 2) 중복 제거한 문자 배열 만들기
        var set = new HashSet<int>();
        for (int i = 0; i < all.Length; i++)
        {
            int cp = char.ConvertToUtf32(all, i);
            if (cp > 0xFFFF) i++;
            if (cp == '\r' || cp == '\n' || cp == '\t') continue;
            set.Add(cp);
        }
        var chars = CodePointsToString(set);

        // 3) 다이내믹 아틀라스에 미리 추가 (런타임 최초 프레임에서 수행)
        bool added = targetFont.TryAddCharacters(chars, out string missing);
        Debug.Log($"[TMPDynamicPrewarm] Prewarm {(added ? "OK" : "PARTIAL")} | Requested:{set.Count} Missing:{missing?.Length ?? 0}");

        // 필요하면 여기서 missing에 대해서 Fallback까지 TryAddCharacters 호출 가능
        foreach (var fb in targetFont.fallbackFontAssetTable)
        {
            if (!string.IsNullOrEmpty(missing))
            {
                fb.TryAddCharacters(missing, out string stillMissing);
                missing = stillMissing;
            }
        }

        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning($"[TMPDynamicPrewarm] 여전히 누락된 문자 있음: {Truncate(missing, 128)}");
        }
    }

    private static string CodePointsToString(HashSet<int> cps)
    {
        var sb = new StringBuilder(cps.Count);
        foreach (int cp in cps)
            sb.Append(char.ConvertFromUtf32(cp));
        return sb.ToString();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max) + "...";
    }
}