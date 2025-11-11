using System;
using System.Runtime.InteropServices;
using UnityEngine;

public enum HapticImpact
{
    Light,
    Medium,
    Heavy,
    Soft,
    Rigid
}

public enum HapticNotification
{
    Success,
    Warning,
    Error
}

/// <summary>
/// 모바일 햅틱(진동) 통합 매니저
/// - Android: VibrationEffect 기반(세기/패턴 지원)
/// - iOS: 네이티브(CoreHaptics / UIFeedbackGenerator) 브릿지 있으면 활용, 없으면 Handheld.Vibrate() 폴백
/// - Editor/PC: 로그 시뮬레이션
/// </summary>
public class HapticManager : MonoBehaviour
{
    public static HapticManager i;

    [Header("Global Settings")]
    [Tooltip("햅틱 전체 사용 여부")]
    public bool enabledGlobally = true;

    [Tooltip("연속 호출 최소 간격(초) (스팸 방지)")]
    [Range(0f, 0.5f)] public float minIntervalSeconds = 0.02f;

    [Tooltip("에디터/PC에서 로그로 시뮬레이션")]
    public bool simulateInEditor = true;

    [Header("iOS Settings")]
    [Tooltip("iOS에서 네이티브(CoreHaptics/Impact) 사용 시도")]
    public bool iosUseNativeIfAvailable = true;

    [Tooltip("네이티브가 없을 때 iOS에서 Handheld.Vibrate() 사용")]
    public bool iosFallbackHandheldVibrate = true;

    private float _lastHapticTime;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _vibrator;
    private AndroidJavaClass _vibrationEffectClass;
    private bool _hasAmplitudeControl;
    private int _sdkInt;
#endif

#if UNITY_IOS && !UNITY_EDITOR
    // ========= iOS 네이티브 브릿지 시그니처 =========
    // Xcode에서 .mm/.h 플러그인으로 아래 심볼들을 구현하면, 런타임에 연결되어 사용됩니다.
    // 구현이 없다면 호출 시 EntryPointNotFoundException이 발생하므로 try-catch로 폴백합니다.

    [DllImport("__Internal")]
    private static extern bool Haptic_IsAvailable(); // CoreHaptics/Impact 사용 가능 여부

    [DllImport("__Internal")]
    private static extern void Haptic_Selection();   // UIFeedbackGenerator: selectionChanged

    // Impact: 0=Light, 1=Medium, 2=Heavy, 3=Soft(iOS13+), 4=Rigid(iOS13+)
    [DllImport("__Internal")]
    private static extern void Haptic_Impact(int style);

    // Notification: 0=Success, 1=Warning, 2=Error
    [DllImport("__Internal")]
    private static extern void Haptic_Notification(int type);

    // CoreHaptics 원샷: seconds(초), intensity(0~1)
    [DllImport("__Internal")]
    private static extern void Haptic_OneShot(double seconds, double intensity01);

    // CoreHaptics 패턴: timingsSec & intensities01 동일 길이 필요
    [DllImport("__Internal")]
    private static extern void Haptic_Pattern([In] double[] timingsSec, [In] double[] intensities01, int length);

    private bool _iosNativeAvailable = false;
#endif

    private void Awake()
    {
        if (i != null && i != this)
        {
            Destroy(gameObject);
            return;
        }
        i = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroid();
#endif
#if UNITY_IOS && !UNITY_EDITOR
        InitiOS();
#endif
    }

    private bool CanHaptic()
    {
        if (enabledGlobally == false)
            return false;

        float t = Time.unscaledTime;
        if (t - _lastHapticTime < minIntervalSeconds)
            return false;

        _lastHapticTime = t;
        return true;
    }

    // ============== Public API ==============

    /// <summary> 간단 선택(토글) 시 추천 </summary>
    public void Selection()
    {
        if (CanHaptic() == false) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidOneShot(15, 40);
#elif UNITY_IOS && !UNITY_EDITOR
        if (iosUseNativeIfAvailable && _iosNativeAvailable)
        {
            TryIOS(() => Haptic_Selection(), onFailBasicTap: iosFallbackHandheldVibrate);
        }
        else
        {
            IOSBasicTapIfAllowed();
        }
#else
        if (simulateInEditor) Debug.Log("[Haptic] Selection");
#endif
    }

    /// <summary> UI 임팩트 프리셋 </summary>
    public void Impact(HapticImpact type)
    {
        if (CanHaptic() == false) return;

        switch (type)
        {
            case HapticImpact.Light:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidOneShot(20, 60);
#elif UNITY_IOS && !UNITY_EDITOR
                IOSImpact(0, defaultFallbackMs: 18, defaultFallbackAmp: 80);
#else
                if (simulateInEditor) Debug.Log("[Haptic] Impact Light");
#endif
                break;

            case HapticImpact.Medium:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidOneShot(30, 120);
#elif UNITY_IOS && !UNITY_EDITOR
                IOSImpact(1, defaultFallbackMs: 24, defaultFallbackAmp: 140);
#else
                if (simulateInEditor) Debug.Log("[Haptic] Impact Medium");
#endif
                break;

            case HapticImpact.Heavy:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidOneShot(40, 200);
#elif UNITY_IOS && !UNITY_EDITOR
                IOSImpact(2, defaultFallbackMs: 32, defaultFallbackAmp: 220);
#else
                if (simulateInEditor) Debug.Log("[Haptic] Impact Heavy");
#endif
                break;

            case HapticImpact.Soft:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidOneShot(15, 40);
#elif UNITY_IOS && !UNITY_EDITOR
                IOSImpact(3, defaultFallbackMs: 16, defaultFallbackAmp: 60);
#else
                if (simulateInEditor) Debug.Log("[Haptic] Impact Soft");
#endif
                break;

            case HapticImpact.Rigid:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidOneShot(20, 255);
#elif UNITY_IOS && !UNITY_EDITOR
                IOSImpact(4, defaultFallbackMs: 20, defaultFallbackAmp: 255);
#else
                if (simulateInEditor) Debug.Log("[Haptic] Impact Rigid");
#endif
                break;
        }
    }

    /// <summary> 성공/경고/에러 알림 프리셋 </summary>
    public void Notification(HapticNotification type)
    {
        if (CanHaptic() == false) return;

        switch (type)
        {
            case HapticNotification.Success:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidPattern(new long[] {0, 18, 30, 18}, new int[] {160, 0, 200});
#elif UNITY_IOS && !UNITY_EDITOR
                IOSNotification(0, timingsMs: new long[] {0, 16, 24, 16}, amps: new int[] {160, 0, 200});
#else
                if (simulateInEditor) Debug.Log("[Haptic] Notification Success");
#endif
                break;

            case HapticNotification.Warning:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidPattern(new long[] {0, 22, 18, 45}, new int[] {200, 0, 220});
#elif UNITY_IOS && !UNITY_EDITOR
                IOSNotification(1, timingsMs: new long[] {0, 22, 18, 45}, amps: new int[] {200, 0, 220});
#else
                if (simulateInEditor) Debug.Log("[Haptic] Notification Warning");
#endif
                break;

            case HapticNotification.Error:
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidPattern(new long[] {0, 45, 25, 20, 20, 20}, new int[] {220, 0, 230, 0, 255});
#elif UNITY_IOS && !UNITY_EDITOR
                IOSNotification(2, timingsMs: new long[] {0, 40, 20, 20, 20, 20}, amps: new int[] {220, 0, 230, 0, 255});
#else
                if (simulateInEditor) Debug.Log("[Haptic] Notification Error");
#endif
                break;
        }
    }

    /// <summary> 커스텀 원샷 (ms, 세기1~255) </summary>
    public void PlayOneShot(int durationMs, int amplitude01to255)
    {
        if (CanHaptic() == false) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidOneShot(Mathf.Max(1, durationMs), Mathf.Clamp(amplitude01to255, 1, 255));
#elif UNITY_IOS && !UNITY_EDITOR
        if (iosUseNativeIfAvailable && _iosNativeAvailable)
        {
            double sec = Math.Max(0.001, durationMs / 1000.0);
            double intensity = Mathf.InverseLerp(1f, 255f, amplitude01to255);
            TryIOS(() => Haptic_OneShot(sec, intensity), onFailBasicTap: iosFallbackHandheldVibrate);
        }
        else
        {
            IOSBasicTapIfAllowed();
        }
#else
        if (simulateInEditor) Debug.Log($"[Haptic] CustomOneShot {durationMs}ms, amp {amplitude01to255}");
#endif
    }

    /// <summary>
    /// 커스텀 패턴.
    /// Android: timings(ms) + amplitudes(1~255)
    /// iOS 네이티브: timingsSec + intensities01(0~1)로 변환하여 CoreHaptics 패턴 호출 (없으면 폴백)
    /// </summary>
    public void CustomPattern(long[] timings, int[] amplitudes)
    {
        if (CanHaptic() == false) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidPattern(timings, amplitudes);
#elif UNITY_IOS && !UNITY_EDITOR
        if (iosUseNativeIfAvailable && _iosNativeAvailable && timings != null && amplitudes != null && timings.Length == amplitudes.Length)
        {
            int len = timings.Length;
            double[] sec = new double[len];
            double[] inten = new double[len];
            for (int n = 0; n < len; n++)
            {
                sec[n] = Math.Max(0.0, timings[n] / 1000.0);
                inten[n] = Mathf.Clamp01((amplitudes[n] - 1) / 254.0f);
            }
            TryIOS(() => Haptic_Pattern(sec, inten, len), onFailBasicTap: iosFallbackHandheldVibrate);
        }
        else
        {
            IOSBasicTapIfAllowed();
        }
#else
        if (simulateInEditor) Debug.Log($"[Haptic] CustomPattern len {timings?.Length}");
#endif
    }

    // ============== ANDROID 구현 ==============

#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitAndroid()
    {
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                _sdkInt = version.GetStatic<int>("SDK_INT");
            }

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            if (_vibrator != null)
            {
                _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
            }

            if (_sdkInt >= 26)
            {
                _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Haptic][Android] init error: {e}");
        }
    }

    private void AndroidOneShot(int durationMs, int amplitude01to255)
    {
        if (_vibrator == null) return;

        try
        {
            if (_sdkInt >= 26 && _vibrationEffectClass != null)
            {
                int amp = Mathf.Clamp(amplitude01to255, 1, 255);
                using (var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                           "createOneShot", (long)durationMs, _hasAmplitudeControl ? amp : -1))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                _vibrator.Call("vibrate", (long)durationMs);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Haptic][Android] OneShot error: {e}");
        }
    }

    private void AndroidPattern(long[] timings, int[] amplitudes)
    {
        if (_vibrator == null || timings == null || amplitudes == null) return;

        try
        {
            if (_sdkInt >= 26 && _vibrationEffectClass != null && _hasAmplitudeControl)
            {
                using (var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>(
                           "createWaveform", timings, amplitudes, -1))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                _vibrator.Call("vibrate", timings, -1);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Haptic][Android] Pattern error: {e}");
        }
    }

    private void OnApplicationQuit()
    {
        try { _vibrator?.Call("cancel"); } catch { }
    }
#endif

    // ============== iOS 구현/폴백 ==============

#if UNITY_IOS && !UNITY_EDITOR
    private void InitiOS()
    {
        _iosNativeAvailable = false;

        if (iosUseNativeIfAvailable)
        {
            // 네이티브 연결 여부 확인 (심볼 미존재 시 예외 → 폴백)
            TryIOS(() =>
            {
                _iosNativeAvailable = Haptic_IsAvailable();
            }, onFailBasicTap: false);
        }
    }

    private void IOSImpact(int style, int defaultFallbackMs, int defaultFallbackAmp)
    {
        if (iosUseNativeIfAvailable && _iosNativeAvailable)
        {
            TryIOS(() => Haptic_Impact(style), onFailBasicTap: iosFallbackHandheldVibrate);
        }
        else
        {
            // 폴백: 한 번 탭
            IOSBasicTapIfAllowed();
        }
    }

    private void IOSNotification(int type, long[] timingsMs, int[] amps)
    {
        if (iosUseNativeIfAvailable && _iosNativeAvailable)
        {
            TryIOS(() => Haptic_Notification(type), onFailBasicTap: iosFallbackHandheldVibrate);
        }
        else
        {
            // 폴백
            IOSBasicTapIfAllowed();
        }
    }

    private void IOSBasicTapIfAllowed()
    {
        if (iosFallbackHandheldVibrate)
            Handheld.Vibrate();
    }

    /// <summary>
    /// iOS 네이티브 호출 래퍼: 네이티브 없거나 예외 시 폴백 허용
    /// </summary>
    private void TryIOS(Action nativeCall, bool onFailBasicTap)
    {
        try
        {
            nativeCall?.Invoke();
        }
        catch (EntryPointNotFoundException)
        {
            // 네이티브 심볼 미존재 → 폴백
            if (onFailBasicTap && iosFallbackHandheldVibrate)
                Handheld.Vibrate();
            _iosNativeAvailable = false;
        }
        catch (DllNotFoundException)
        {
            if (onFailBasicTap && iosFallbackHandheldVibrate)
                Handheld.Vibrate();
            _iosNativeAvailable = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Haptic][iOS] native exception: {e}");
            if (onFailBasicTap && iosFallbackHandheldVibrate)
                Handheld.Vibrate();
        }
    }
#else
    private void InitiOS() { /* Editor/Android/PC: noop */ }

    private void IOSBasicTapIfAllowed()
    {
        if (simulateInEditor)
            Debug.Log("[Haptic] iOSBasicTap (simulated)");
    }
#endif
}

// HapticBridge.h
// #import <Foundation/Foundation.h>
// #import <UIKit/UIKit.h>         // UIFeedbackGenerator
// #import <CoreHaptics/CoreHaptics.h> // iOS 13+
//
// #ifdef __cplusplus
// extern "C" {
// #endif
//
//     bool Haptic_IsAvailable(void);
//     void Haptic_Selection(void);
//     void Haptic_Impact(int style);          // 0..4
//     void Haptic_Notification(int type);     // 0..2
//     void Haptic_OneShot(double seconds, double intensity01);
//     void Haptic_Pattern(const double* timingsSec, const double* intensities01, int length);
//
// #ifdef __cplusplus
// }
// #endif

// HapticBridge.mm
// #import "HapticBridge.h"
//
// static bool hasCoreHaptics() {
//     if (@available(iOS 13.0, *)) { return YES; }
//     return NO;
// }
//
// bool Haptic_IsAvailable(void) {
//     // CoreHaptics 또는 UIFeedbackGenerator 사용 가능 여부 반환
//     return true; // 최소한 Impact/Selection은 가능
// }
//
// void Haptic_Selection(void) {
//     if (@available(iOS 10.0, *)) {
//         UISelectionFeedbackGenerator* g = [UISelectionFeedbackGenerator new];
//         [g prepare];
//         [g selectionChanged];
//     }
// }
//
// void Haptic_Impact(int style) {
//     if (@available(iOS 10.0, *)) {
//         UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
//         if (style == 1) s = UIImpactFeedbackStyleMedium;
//         else if (style == 2) s = UIImpactFeedbackStyleHeavy;
// #ifdef __IPHONE_13_0
//         if (@available(iOS 13.0, *)) {
//             if (style == 3) s = UIImpactFeedbackStyleSoft;
//             else if (style == 4) s = UIImpactFeedbackStyleRigid;
//         }
// #endif
//         UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
//         [g prepare];
//         [g impactOccurred];
//     }
// }
//
// void Haptic_Notification(int type) {
//     if (@available(iOS 10.0, *)) {
//         UINotificationFeedbackType t = UINotificationFeedbackTypeSuccess;
//         if (type == 1) t = UINotificationFeedbackTypeWarning;
//         else if (type == 2) t = UINotificationFeedbackTypeError;
//         UINotificationFeedbackGenerator* g = [UINotificationFeedbackGenerator new];
//         [g prepare];
//         [g notificationOccurred:t];
//     }
// }
//
// void Haptic_OneShot(double seconds, double intensity01) {
//     if (@available(iOS 13.0, *)) {
//         // CoreHaptics로 duration & intensity를 생성
//         // (간단 예: 지속 시간 동안 지속 자극)
//         NSError* err = nil;
//         CHHapticEngine* engine = [[CHHapticEngine alloc] initAndReturnError:&err];
//         [engine startAndReturnError:&err];
//         CHHapticEvent* e = [[CHHapticEvent alloc]
//                              initWithEventType:CHHapticEventTypeHapticContinuous
//                              parameters:@[[[CHHapticEventParameter alloc]
//                                            initWithParameterID:CHHapticEventParameterIDHapticIntensity
//                                            value:(float)intensity01]]
//                              relativeTime:0 duration:seconds];
//         CHHapticPattern* pattern = [[CHHapticPattern alloc] initWithEvents:@[e] parameters:@[] error:&err];
//         id<CHHapticPatternPlayer> player = [engine createPlayerWithPattern:pattern error:&err];
//         [player startAtTime:0 error:&err];
//         dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(seconds * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
//             [engine stopWithCompletionHandler:nil];
//         });
//     } else {
//         // iOS13 미만: 대략적 대체(Impact)
//         Haptic_Impact(1);
//     }
// }
//
// void Haptic_Pattern(const double* timingsSec, const double* intensities01, int length) {
//     if (@available(iOS 13.0, *)) {
//         NSError* err = nil;
//         CHHapticEngine* engine = [[CHHapticEngine alloc] initAndReturnError:&err];
//         [engine startAndReturnError:&err];
//
//         NSMutableArray<CHHapticEvent*>* events = [NSMutableArray new];
//         double t = 0.0;
//         for (int i=0; i<length; ++i) {
//             double dur = MAX(0.0, timingsSec[i]);
//             float intensity = (float)fmax(0.0, fmin(1.0, intensities01[i]));
//             if (dur <= 0.0) { continue; }
//             CHHapticEvent* e = [[CHHapticEvent alloc]
//                                  initWithEventType:CHHapticEventTypeHapticContinuous
//                                  parameters:@[[[CHHapticEventParameter alloc]
//                                                initWithParameterID:CHHapticEventParameterIDHapticIntensity
//                                                value:intensity]]
//                                  relativeTime:t duration:dur];
//             [events addObject:e];
//             t += dur;
//         }
//         CHHapticPattern* pattern = [[CHHapticPattern alloc] initWithEvents:events parameters:@[] error:&err];
//         id<CHHapticPatternPlayer> player = [engine createPlayerWithPattern:pattern error:&err];
//         [player startAtTime:0 error:&err];
//         dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(t * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
//             [engine stopWithCompletionHandler:nil];
//         });
//     } else {
//         // iOS13 미만: 간단 대체
//         Haptic_Notification(1);
//     }
// }


