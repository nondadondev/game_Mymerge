using System;
using UnityEngine;

public enum LinkEnum
{
    PrivacyPolicy,
    TermsOfService,
}

public class LinkOpener : MonoBehaviour
{
    public static LinkOpener i;

    private void Awake()
    {
        i = this;
    }

    [Header("사이트 링크")]
    [SerializeField] private string url_PrivacyPolicy = "https://nullergames.com/privacy-policy/";
    [SerializeField] private string url_TermsOfService = "https://www.nullergames.com/terms-of-service/";
    
    [Header("문의 메일 설정")]
    [SerializeField] private string contactEmail = "nullergames@gmail.com"; // ✅ 실제 메일 주소
    [SerializeField] private string mailSubject = "문의사항입니다";              // 기본 제목
    [SerializeField] private string mailBody = "안녕하세요,\n\n문의 내용을 작성해주세요."; // 기본 본문

    // enum 파라미터 이름을 'linkType'으로 변경
    public void OpenLink(LinkEnum linkType)
    {
        string url = null;

        switch (linkType)
        {
            case LinkEnum.PrivacyPolicy:
                url = url_PrivacyPolicy;
                break;
            case LinkEnum.TermsOfService:
                url = url_TermsOfService;
                break;
            default:
                Debug.LogWarning($"Unhandled LinkEnum: {linkType}");
                break;
        }

        if (!string.IsNullOrEmpty(url))
            OpenLink(url);
    }

    // 문자열 URL로 직접 열기 (오버로드)
    public void OpenLink(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.LogWarning("OpenLink: URL이 비어 있습니다.");
            return;
        }

        string key = input.Trim();

        // 키워드 매칭 (대소문자 무시)
        if (string.Equals(key, "PrivacyPolicy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Policy", StringComparison.OrdinalIgnoreCase))
        {
            OpenResolvedUrl(url_PrivacyPolicy);
            return;
        }

        if (string.Equals(key, "TermsOfService", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Service", StringComparison.OrdinalIgnoreCase))
        {
            OpenResolvedUrl(url_TermsOfService);
            return;
        }

        // 위 키워드가 아니면 입력을 URL로 간주해 직접 오픈
        OpenResolvedUrl(key);
    }
    
    // 실제 URL을 여는 단일 진입점 (재귀 방지)
    private void OpenResolvedUrl(string finalUrl)
    {
        if (string.IsNullOrWhiteSpace(finalUrl))
        {
            Debug.LogWarning("OpenResolvedUrl: URL이 비어 있습니다.");
            return;
        }

        string url = finalUrl.Trim();

        // 스킴이 없는 경우 보정(간단 처리)
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        Application.OpenURL(url);
    }
    
    // ✅ 문의 메일 열기
    public void OpenMail()
    {
        string deviceModel = SystemInfo.deviceModel;
        string deviceName = SystemInfo.deviceName;
        string os = SystemInfo.operatingSystem;
        string platform = Application.platform.ToString();
        string gameVersion = Application.version; // ✅ Build Settings > Player Settings > Version 값
        string systemMemory = $"{SystemInfo.systemMemorySize} MB";
        string graphicsDevice = SystemInfo.graphicsDeviceName;
        string graphicsMemory = $"{SystemInfo.graphicsMemorySize} MB";

        // 실제 문의 내용을 사용자가 채워넣을 부분
        string userMessage = "문의 내용을 여기에 작성해주세요.";

        // 본문 구성
        string body =
            $@"{userMessage}

            ----------------------------
            > Device Info
            - Device: {deviceName} ({deviceModel})
            - OS: {os}
            - Platform: {platform}
            - System Memory: {systemMemory}
            - Graphics: {graphicsDevice} ({graphicsMemory})

            > Game Info
            - Game Version: {gameVersion}

            (이 정보는 문제 해결에 도움이 됩니다.)
            ----------------------------";

        // mailto 링크 생성
        string mailto = $"mailto:{contactEmail}?subject={Escape(mailSubject)}&body={Escape(body)}";
        Application.OpenURL(mailto);
    }

    // ✅ URL 인코딩 (간단히 최신 방식으로)
    private string Escape(string text)
    {
        return UnityEngine.Networking.UnityWebRequest.EscapeURL(text).Replace("+", "%20");
    }
}