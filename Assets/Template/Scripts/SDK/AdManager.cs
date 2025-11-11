// using System;
// using UnityEngine;
//
// // AppLovin MAX SDK 네임스페이스
// using MaxSdkBase = AppLovinMax.MaxSdkBase;
// using MaxSdk = AppLovinMax.MaxSdk;
// using MaxSdkCallbacks = AppLovinMax.MaxSdkCallbacks;
//
// public class AdManager : MonoBehaviour
// {
//     public static AdManager i;
//
//     [Header("Initialize On Awake")]
//     [Tooltip("게임 시작 시 자동 초기화 여부")]
//     public bool initializeOnAwake = true;
//
//     [Header("Debug")]
//     [Tooltip("MAX SDK 디버그 로깅")]
//     public bool enableVerboseLogs = false;
//
// #if UNITY_IOS
//     // 실제 배포 시 각 포맷별 유닛ID로 교체
//     [Header("iOS Ad Unit Ids")]
//     [SerializeField] private string bannerAdUnitId        = "«iOS-banner-ad-unit-ID»";
//     [SerializeField] private string interstitialAdUnitId   = "«iOS-interstitial-ad-unit-ID»";
//     [SerializeField] private string rewardedAdUnitId       = "«iOS-rewarded-ad-unit-ID»";
//     [SerializeField] private string appOpenAdUnitId        = "«iOS-app-open-ad-unit-ID»";   // 추후 사용
// #else   // UNITY_ANDROID
//     [Header("Android Ad Unit Ids")]
//     [SerializeField] private string bannerAdUnitId        = "«Android-banner-ad-unit-ID»";
//     [SerializeField] private string interstitialAdUnitId   = "«Android-interstitial-ad-unit-ID»";
//     [SerializeField] private string rewardedAdUnitId       = "«Android-rewarded-ad-unit-ID»";
//     [SerializeField] private string appOpenAdUnitId        = "«Android-app-open-ad-unit-ID»"; // 추후 사용
// #endif
//
//     // 전면/리워드 재시도 지수백오프 관리용
//     private int _interstitialRetryAttempt;
//     private int _rewardedRetryAttempt;
//
//     // 배너 표시 상태 캐시
//     private bool _bannerCreated;
//     private bool _bannerShowing;
//
//     // 콜백 전달용(필요 시 외부에서 구독 가능)
//     public event Action OnInterstitialClosed;
//     public event Action<bool> OnRewardedClosed; // rewardedGranted=true면 보상 획득
//
//     private void Awake()
//     {
//         i = this;
//         if (enableVerboseLogs) MaxSdk.SetVerboseLogging(true);
//         if (initializeOnAwake)
//         {
//             InitializeMaxIfNeeded();
//         }
//     }
//
//     private void OnDestroy()
//     {
//         if (i == this) i = null;
//     }
//
//     // --------- 초기화 ---------
//     public void InitializeMaxIfNeeded()
//     {
//         if (MaxSdk.IsInitialized())
//         {
//             // 이미 초기화된 경우 광고 리스너만 보장
//             AttachInterstitialCallbacks();
//             AttachRewardedCallbacks();
//             AttachBannerCallbacks();
//             return;
//         }
//
//         // iOS: ATT 권한 요청(선택) — 필요 없으면 제거 가능
// #if UNITY_IOS
//         MaxSdkCallbacks.OnSdkConsentDialogDismissedEvent += _ => { };
//         MaxSdk.SetSdkKey(GetSdkKeyFromProject()); // SDK Key는 프로젝트 설정에 이미 들어있다면 생략해도 됨
//         MaxSdk.RequestTrackingAuthorizationWithCompletion(status =>
//         {
//             // ATT 응답 이후 초기화
//             MaxSdk.InitializeSdk();
//         });
// #else
//         MaxSdk.SetSdkKey(GetSdkKeyFromProject()); // Android도 동일하게 호출해도 무해
//         MaxSdk.InitializeSdk();
// #endif
//         MaxSdkCallbacks.OnSdkInitializedEvent += OnMaxInitialized;
//     }
//
//     // 실제 SDK Key는 보통 Project Settings > AppLovin Integration Manager에서 자동 주입됨.
//     private string GetSdkKeyFromProject()
//     {
//         // 비워도 동작하는 환경 많지만, 안전하게 빈 문자열 반환
//         return string.Empty;
//     }
//
//     private void OnMaxInitialized(MaxSdkBase.SdkConfiguration config)
//     {
//         // 전면/리워드/배너 콜백 체인 연결
//         AttachInterstitialCallbacks();
//         AttachRewardedCallbacks();
//         AttachBannerCallbacks();
//
//         // 초기 로드
//         LoadInterstitial();
//         LoadRewarded();
//
//         // 배너는 필요 시 CreateBanner() 호출로 생성/표시 (자동생성 원하면 여기서 호출)
//         // CreateBanner();
//         // ShowBanner();
//     }
//
//     // =========================================================
//     //                        배너 광고
//     // =========================================================
//     public void CreateBanner(MaxSdkBase.BannerPosition position = MaxSdkBase.BannerPosition.BottomCenter)
//     {
//         if (_bannerCreated) return;
//
//         MaxSdk.CreateBanner(bannerAdUnitId, position);
//         // 배경 투명 등 옵션이 필요하면 여기서 설정 (문서 전달받은 뒤 채움)
//         // MaxSdk.SetBannerBackgroundColor(bannerAdUnitId, new Color(0,0,0,0));
//         _bannerCreated = true;
//     }
//
//     public void ShowBanner()
//     {
//         if (!_bannerCreated) CreateBanner(MaxSdkBase.BannerPosition.BottomCenter);
//         if (_bannerShowing == true) return;
//
//         MaxSdk.ShowBanner(bannerAdUnitId);
//         _bannerShowing = true;
//     }
//
//     public void HideBanner()
//     {
//         if (_bannerCreated == false) return;
//         if (_bannerShowing == false) return;
//
//         MaxSdk.HideBanner(bannerAdUnitId);
//         _bannerShowing = false;
//     }
//
//     public void DestroyBanner()
//     {
//         if (_bannerCreated == false) return;
//
//         MaxSdk.DestroyBanner(bannerAdUnitId);
//         _bannerCreated = false;
//         _bannerShowing = false;
//     }
//
//     private void AttachBannerCallbacks()
//     {
//         MaxSdkCallbacks.Banner.OnAdLoadedEvent += (unitId, info) =>
//         {
//             // 로드됨. 배너는 ShowBanner()가 실제 표시 제어
//         };
//         MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (unitId, error) =>
//         {
//             // 필요 시 재시도 로직 추가 가능(문서 확인 후)
//         };
//         MaxSdkCallbacks.Banner.OnAdClickedEvent += (unitId, info) => { };
//         MaxSdkCallbacks.Banner.OnAdExpandedEvent += (unitId, info) => { };
//         MaxSdkCallbacks.Banner.OnAdCollapsedEvent += (unitId, info) => { };
//         MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += (unitId, adInfo) =>
//         {
//             // 수익 콜백 처리(애널리틱스 전송 등)
//         };
//     }
//
//     // =========================================================
//     //                      전면(Interstitial)
//     // =========================================================
//     public void LoadInterstitial()
//     {
//         MaxSdk.LoadInterstitial(interstitialAdUnitId);
//     }
//
//     public bool IsInterstitialReady()
//     {
//         return MaxSdk.IsInterstitialReady(interstitialAdUnitId);
//     }
//
//     public void ShowInterstitial()
//     {
//         if (IsInterstitialReady() == true)
//         {
//             MaxSdk.ShowInterstitial(interstitialAdUnitId);
//             return;
//         }
//         // 준비 안됨: 즉시 로드 시도
//         LoadInterstitial();
//     }
//
//     private void AttachInterstitialCallbacks()
//     {
//         MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (unitId, info) =>
//         {
//             _interstitialRetryAttempt = 0;
//         };
//
//         MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (unitId, error) =>
//         {
//             _interstitialRetryAttempt++;
//             double retryDelay = Math.Pow(2, Math.Min(6, _interstitialRetryAttempt)); // 최대 64초
//             Invoke(nameof(LoadInterstitial), (float)retryDelay);
//         };
//
//         MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += (unitId, info) => { };
//         MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += (unitId, error, info) =>
//         {
//             // 실패 시 재로드
//             LoadInterstitial();
//         };
//
//         MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += (unitId, info) =>
//         {
//             // 닫힘: 다음 광고 미리 로드
//             LoadInterstitial();
//             OnInterstitialClosed?.Invoke();
//         };
//
//         MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += (unitId, adInfo) =>
//         {
//             // 수익 데이터 처리
//         };
//     }
//
//     // =========================================================
//     //                      리워드(Rewarded)
//     // =========================================================
//     private bool _rewardGranted;
//
//     public void LoadRewarded()
//     {
//         MaxSdk.LoadRewardedAd(rewardedAdUnitId);
//     }
//
//     public bool IsRewardedReady()
//     {
//         return MaxSdk.IsRewardedAdReady(rewardedAdUnitId);
//     }
//
//     public void ShowRewarded()
//     {
//         if (IsRewardedReady() == true)
//         {
//             _rewardGranted = false;
//             MaxSdk.ShowRewardedAd(rewardedAdUnitId);
//             return;
//         }
//         // 준비 안됨: 즉시 로드 시도
//         LoadRewarded();
//         // 필요하면: 준비 안된 상태 콜백/토스트 등 처리
//     }
//
//     private void AttachRewardedCallbacks()
//     {
//         MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (unitId, info) =>
//         {
//             _rewardedRetryAttempt = 0;
//         };
//
//         MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (unitId, error) =>
//         {
//             _rewardedRetryAttempt++;
//             double retryDelay = Math.Pow(2, Math.Min(6, _rewardedRetryAttempt));
//             Invoke(nameof(LoadRewarded), (float)retryDelay);
//         };
//
//         MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += (unitId, info) => { };
//         MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += (unitId, error, info) =>
//         {
//             // 표시 실패 시 재로드
//             LoadRewarded();
//             OnRewardedClosed?.Invoke(false);
//         };
//
//         MaxSdkCallbacks.Rewarded.OnAdClickedEvent += (unitId, info) => { };
//
//         MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += (unitId, info) =>
//         {
//             // 닫힘. 보상 여부 전달 후 다음 로드
//             OnRewardedClosed?.Invoke(_rewardGranted);
//             LoadRewarded();
//         };
//
//         MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (unitId, reward, info) =>
//         {
//             // 실제 보상 시점
//             _rewardGranted = true;
//         };
//
//         MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += (unitId, adInfo) =>
//         {
//             // 수익 처리
//         };
//     }
//
//     // =========================================================
//     //                    앱 오픈(App Open) 훅
//     // =========================================================
//     // App Open Ad는 MAX에서 전용 포맷(베타/변동) 또는 외부(AdMob 등)로 붙이는 케이스가 있어
//     // 다음 턴에서 네가 선택한 문서 기준으로 아래 메서드 구현 이어서 합칠게.
//     public void PreloadAppOpen() { /* TODO: 문서 반영 후 구현 */ }
//     public bool IsAppOpenReady() { return false; }
//     public void ShowAppOpenIfReady() { /* TODO: 문서 반영 후 구현 */ }
// }
