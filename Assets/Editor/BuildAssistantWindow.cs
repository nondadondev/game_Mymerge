// File: Assets/Editor/BuildAssistantWindow.cs
// Unity 2021+ / 2022+ / 6.x 호환
// 기능 요약:
// - 안드로이드 AAB/APK 빌드 (키스토어 자동 등록, 비번 자동입력)
// - 버전 관리: a.b.c 각각 ↑/↓로 1씩 조절 (자연스러운 자릿수 증가)
//   * 사용자가 화살표를 한 번도 누르지 않으면 빌드시 patch(c)만 +1 자동 반영
// - bundleVersionCode(정수) 관리: 3자리 표시 + 성공 시 자동 +1
// - Android 심볼(symbols.zip) 생성 옵션 + Proguard/R8 매핑(mapping.txt) 생성 옵션
// - 빌드 완료 후 report에서 symbols.zip / mapping.txt 찾아서 출력 폴더로 자동 복사
//
// 보안 주의: 비밀번호는 EditorPrefs(로컬, 평문)에 저장됩니다.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildAssistantWindow : EditorWindow
{
    // ----------- EditorPrefs Keys -----------
    const string PREF_KEYSTORE_PATH   = "BA_KeystorePath";
    const string PREF_ALIAS_NAME      = "BA_AliasName";
    const string PREF_KEYSTORE_PASS   = "BA_KeystorePass";   // 평문 저장
    const string PREF_KEYALIAS_PASS   = "BA_KeyaliasPass";   // 평문 저장
    const string PREF_BUILD_NUMBER    = "BA_BuildNumber";    // int (표시는 3자리)
    const string PREF_OUTPUT_DIR      = "BA_OutputDir";
    const string PREF_USE_AAB         = "BA_UseAAB";
    const string PREF_APPLY_KEYSTORE  = "BA_ApplyKeystoreOnBuild";

    // 심볼/매핑 옵션
    const string PREF_ANDROID_SYMBOL_MODE = "BA_AndroidSymbolMode"; // 0=None, 1=Public, 2=Debugging (리플렉션용)
    const string PREF_ANDROID_CREATE_SYMBOLS = "BA_AndroidCreateSymbolsZip"; // 구버전 bool 호환
    const string PREF_ANDROID_MINIFY_RELEASE = "BA_AndroidMinifyRelease"; // mapping.txt 생성용

    // ----------- UI Fields -----------
    string keystorePath;
    string aliasName;
    string keystorePass;
    string keyaliasPass;

    int buildNumber;            // Android bundleVersionCode
    string outputDir;
    bool useAAB;
    bool applyKeystoreOnBuild;

    // 버전(semver: a.b.c) 조절 상태
    int verMajor, verMinor, verPatch;
    bool manualBumpTouched; // 화살표를 한 번이라도 누르면 true → 빌드 시 수동 버전 우선

    // 심볼/매핑 UI 상태
    // SymbolMode: 0=None, 1=Public, 2=Debugging (Unity의 AndroidCreateSymbols enum 과 매핑)
    int androidSymbolMode = 0;
    bool androidCreateSymbolsZipBool = false; // 구버전 호환용(해당 프로퍼티만 있는 버전)
    bool androidMinifyRelease = false;        // mapping.txt 생성

    [MenuItem("Tools/Build Assistant (Android)")]
    public static void ShowWindow()
    {
        var w = GetWindow<BuildAssistantWindow>("Build Assistant");
        w.minSize = new Vector2(600, 900);
        w.Show();
    }

    void OnEnable()
    {
        // Prefs 로드
        keystorePath         = EditorPrefs.GetString(PREF_KEYSTORE_PATH, "");
        aliasName            = EditorPrefs.GetString(PREF_ALIAS_NAME, "");
        keystorePass         = EditorPrefs.GetString(PREF_KEYSTORE_PASS, "");
        keyaliasPass         = EditorPrefs.GetString(PREF_KEYALIAS_PASS, "");
        buildNumber          = EditorPrefs.GetInt(PREF_BUILD_NUMBER, Math.Max(1, PlayerSettings.Android.bundleVersionCode));
        outputDir            = EditorPrefs.GetString(PREF_OUTPUT_DIR, "Builds/Android");
        useAAB               = EditorPrefs.GetBool(PREF_USE_AAB, true);
        applyKeystoreOnBuild = EditorPrefs.GetBool(PREF_APPLY_KEYSTORE, true);

        // PlayerSettings의 현재 bundleVersion을 분해
        string cur = PlayerSettings.bundleVersion;
        if (string.IsNullOrWhiteSpace(cur)) cur = "1.0.0";
        ParseSemVer(cur, out verMajor, out verMinor, out verPatch);
        manualBumpTouched = false;

        if (buildNumber <= 0) buildNumber = 1;

        // 심볼/매핑 설정 로드(가능하면 에디터 실제 값을 읽고, 안 되면 Prefs 대체)
        if (!TryGetAndroidCreateSymbols(out int modeEnum))
        {
            // 구버전 bool 속성(androidCreateSymbolsZip) 또는 미지원
            androidCreateSymbolsZipBool = EditorPrefs.GetBool(PREF_ANDROID_CREATE_SYMBOLS, false);
            androidSymbolMode = EditorPrefs.GetInt(PREF_ANDROID_SYMBOL_MODE, 0);
        }
        else
        {
            androidSymbolMode = modeEnum; // 0=None,1=Public,2=Debugging
        }

        // Minify Release
        try
        {
            androidMinifyRelease = PlayerSettings.Android.minifyRelease;
        }
        catch
        {
            androidMinifyRelease = EditorPrefs.GetBool(PREF_ANDROID_MINIFY_RELEASE, false);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🔐 키스토어 자동 등록 / 비밀번호 자동입력", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("비밀번호는 이 컴퓨터의 EditorPrefs에 평문으로 저장됩니다. 공용 PC에서는 저장하지 않는 것을 권장합니다.", MessageType.Warning);
        keystorePath = EditorGUILayout.TextField("Keystore Path", keystorePath);
        aliasName    = EditorGUILayout.TextField("Alias Name", aliasName);
        keystorePass = PasswordField("Keystore Password", keystorePass);
        keyaliasPass = PasswordField("Keyalias Password", keyaliasPass);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("현재 값 저장(EditorPrefs)"))
        {
            SavePrefs();
            EditorUtility.DisplayDialog("저장 완료", "설정을 EditorPrefs에 저장했습니다.", "확인");
        }
        if (GUILayout.Button("저장값 불러오기"))
        {
            LoadPrefs();
            Repaint();
        }
        if (GUILayout.Button("저장값 초기화"))
        {
            ClearPrefs();
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🧩 버전/빌드 넘버 관리", EditorStyles.boldLabel);

        // ---- a.b.c 화살표(요청 사양) ----
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("버전명 a.b.c (각 자리 1씩 증감, 자릿수 자동 확대)", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2);

            DrawVersionArrowRow("Major (a)", ref verMajor);
            DrawVersionArrowRow("Minor (b)", ref verMinor);
            DrawVersionArrowRow("Patch (c)", ref verPatch);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"미리보기 → {verMajor}.{verMinor}.{verPatch}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("현재 PlayerSettings 값으로 되돌리기"))
                {
                    string cur = PlayerSettings.bundleVersion;
                    if (string.IsNullOrWhiteSpace(cur)) cur = "1.0.0";
                    ParseSemVer(cur, out verMajor, out verMinor, out verPatch);
                    manualBumpTouched = false;
                }

                if (GUILayout.Button("Patch +1 (빠른 버튼)"))
                {
                    verPatch = Mathf.Max(0, verPatch + 1);
                    manualBumpTouched = true;
                }
            }

            EditorGUILayout.HelpBox(
                "빌드 시, 이 창에서 화살표(또는 빠른 버튼)를 한 번도 누르지 않았다면 자동으로 Patch만 +1 됩니다.\n" +
                "화살표를 눌렀다면, 현재 미리보기 값이 그대로 적용됩니다.",
                MessageType.Info);
        }

        // ---- bundleVersionCode (정수) ----
        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope("box"))
        {
            int oldCode = buildNumber;
            string bnStr = EditorGUILayout.TextField("bundleVersionCode(표시 3자리)", FormatBuildNumber(buildNumber));
            buildNumber = ParseBuildNumberSafe(bnStr, buildNumber);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("빌드 넘버 +1")) buildNumber = Mathf.Clamp(buildNumber + 1, 1, 999999);
                if (GUILayout.Button("빌드 넘버 -1")) buildNumber = Mathf.Clamp(buildNumber - 1, 1, 999999);
            }
        }

        // ---- 심볼/매핑 설정 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("🐞 심볼 / 매핑 파일", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Android 심볼(symbols.zip) 생성", EditorStyles.miniBoldLabel);

            // 최신 Unity는 EditorUserBuildSettings.androidCreateSymbols (enum) 사용
            // 구버전은 EditorUserBuildSettings.androidCreateSymbolsZip (bool) 사용
            bool hasEnumProp = HasAndroidCreateSymbolsEnum();
            if (hasEnumProp == true)
            {
                androidSymbolMode = EditorGUILayout.Popup(
                    "Create Symbols",
                    androidSymbolMode,
                    new[] { "None", "Public (Java/Kotlin)", "Debugging (Full Native)" }
                );
            }
            else
            {
                androidCreateSymbolsZipBool = EditorGUILayout.ToggleLeft("Create symbols.zip (구버전 호환)", androidCreateSymbolsZipBool);
                // enum UI도 보조적으로 유지(에디터 버전 바뀌어도 취향 저장하려는 목적)
                androidSymbolMode = EditorGUILayout.Popup(
                    "선호 모드(저장용)",
                    androidSymbolMode,
                    new[] { "None", "Public", "Debugging" }
                );
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("R8/Proguard 매핑(mapping.txt) 생성", EditorStyles.miniBoldLabel);
            androidMinifyRelease = EditorGUILayout.ToggleLeft("Minify Release(난독화/최적화) → mapping.txt 생성", androidMinifyRelease);

            EditorGUILayout.HelpBox(
                "• symbols.zip: 네이티브 크래시/ANR 분석용 심볼(Play Console에 업로드)\n" +
                "• mapping.txt: R8/Proguard 난독화 해제용(자바/코틀린 스택트레이스 역변환)\n" +
                "빌드 완료 후 자동으로 산출물 폴더에 복사합니다.",
                MessageType.Info);
        }

        // ---- 빌드 설정 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📦 빌드 설정", EditorStyles.boldLabel);
        applyKeystoreOnBuild = EditorGUILayout.ToggleLeft("빌드시 키스토어 설정 자동 적용", applyKeystoreOnBuild);
        useAAB = EditorGUILayout.ToggleLeft("AAB(앱 번들)로 빌드", useAAB);
        outputDir = EditorGUILayout.TextField("출력 폴더", outputDir);

        // ---- 프로젝트 스냅샷 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("📋 현재 프로젝트 상태", EditorStyles.boldLabel);
        DrawProjectSnapshot();

        // ---- 빌드 버튼 ----
        EditorGUILayout.Space();
        if (GUILayout.Button("🚀 안드로이드 빌드 실행", GUILayout.Height(42)))
        {
            RunAndroidBuild();
        }
    }

    // -------- 버전 화살표 UI --------
    void DrawVersionArrowRow(string label, ref int value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            if (GUILayout.Button("↑", GUILayout.Width(36)))
            {
                long next = (long)value + 1;
                value = (int)Mathf.Max(0, (int)Mathf.Min(next, int.MaxValue));
                manualBumpTouched = true;
            }
            GUILayout.Label(value.ToString(), GUILayout.Width(140));
            if (GUILayout.Button("↓", GUILayout.Width(36)))
            {
                long next = (long)value - 1;
                if (next < 0) next = 0; // 음수 방지
                value = (int)next;
                manualBumpTouched = true;
            }
        }
    }

    // -------- 비밀번호 입력(마스킹) --------
    string PasswordField(string label, string value)
    {
        var style = new GUIStyle(EditorStyles.textField) { fontSize = 12 };
        Rect r = EditorGUILayout.GetControlRect();
        EditorGUI.LabelField(new Rect(r.x, r.y, 150, r.height), label);
        string raw = EditorGUI.TextField(new Rect(r.x + 150, r.y, r.width - 150, r.height), value, style);
        return raw;
    }

    // -------- Prefs I/O --------
    void SavePrefs()
    {
        EditorPrefs.SetString(PREF_KEYSTORE_PATH, keystorePath ?? "");
        EditorPrefs.SetString(PREF_ALIAS_NAME, aliasName ?? "");
        EditorPrefs.SetString(PREF_KEYSTORE_PASS, keystorePass ?? "");
        EditorPrefs.SetString(PREF_KEYALIAS_PASS, keyaliasPass ?? "");
        EditorPrefs.SetInt(PREF_BUILD_NUMBER, buildNumber);
        EditorPrefs.SetString(PREF_OUTPUT_DIR, string.IsNullOrWhiteSpace(outputDir) ? "Builds/Android" : outputDir);
        EditorPrefs.SetBool(PREF_USE_AAB, useAAB);
        EditorPrefs.SetBool(PREF_APPLY_KEYSTORE, applyKeystoreOnBuild);

        EditorPrefs.SetInt(PREF_ANDROID_SYMBOL_MODE, androidSymbolMode);
        EditorPrefs.SetBool(PREF_ANDROID_CREATE_SYMBOLS, androidCreateSymbolsZipBool);
        EditorPrefs.SetBool(PREF_ANDROID_MINIFY_RELEASE, androidMinifyRelease);
    }

    void LoadPrefs()
    {
        keystorePath         = EditorPrefs.GetString(PREF_KEYSTORE_PATH, keystorePath);
        aliasName            = EditorPrefs.GetString(PREF_ALIAS_NAME, aliasName);
        keystorePass         = EditorPrefs.GetString(PREF_KEYSTORE_PASS, keystorePass);
        keyaliasPass         = EditorPrefs.GetString(PREF_KEYALIAS_PASS, keyaliasPass);
        buildNumber          = EditorPrefs.GetInt(PREF_BUILD_NUMBER, buildNumber);
        outputDir            = EditorPrefs.GetString(PREF_OUTPUT_DIR, outputDir);
        useAAB               = EditorPrefs.GetBool(PREF_USE_AAB, useAAB);
        applyKeystoreOnBuild = EditorPrefs.GetBool(PREF_APPLY_KEYSTORE, applyKeystoreOnBuild);

        androidSymbolMode        = EditorPrefs.GetInt(PREF_ANDROID_SYMBOL_MODE, androidSymbolMode);
        androidCreateSymbolsZipBool = EditorPrefs.GetBool(PREF_ANDROID_CREATE_SYMBOLS, androidCreateSymbolsZipBool);
        androidMinifyRelease     = EditorPrefs.GetBool(PREF_ANDROID_MINIFY_RELEASE, androidMinifyRelease);
    }

    void ClearPrefs()
    {
        EditorPrefs.DeleteKey(PREF_KEYSTORE_PATH);
        EditorPrefs.DeleteKey(PREF_ALIAS_NAME);
        EditorPrefs.DeleteKey(PREF_KEYSTORE_PASS);
        EditorPrefs.DeleteKey(PREF_KEYALIAS_PASS);
        EditorPrefs.DeleteKey(PREF_BUILD_NUMBER);
        EditorPrefs.DeleteKey(PREF_OUTPUT_DIR);
        EditorPrefs.DeleteKey(PREF_USE_AAB);
        EditorPrefs.DeleteKey(PREF_APPLY_KEYSTORE);

        EditorPrefs.DeleteKey(PREF_ANDROID_SYMBOL_MODE);
        EditorPrefs.DeleteKey(PREF_ANDROID_CREATE_SYMBOLS);
        EditorPrefs.DeleteKey(PREF_ANDROID_MINIFY_RELEASE);

        EditorUtility.DisplayDialog("초기화 완료", "EditorPrefs에 저장된 값을 모두 삭제했습니다.", "확인");
    }

    // -------- 프로젝트 스냅샷 --------
    void DrawProjectSnapshot()
    {
        string product = Application.productName;
        string company = Application.companyName;
        string currentVersion = PlayerSettings.bundleVersion;
        int currentCode = PlayerSettings.Android.bundleVersionCode;

        EditorGUILayout.LabelField($"Company: {company}");
        EditorGUILayout.LabelField($"Product: {product}");
        EditorGUILayout.LabelField($"현재 bundleVersion: {currentVersion}");
        EditorGUILayout.LabelField($"현재 bundleVersionCode: {currentCode}");
        EditorGUILayout.LabelField($"선택 빌드타겟: {EditorUserBuildSettings.activeBuildTarget}");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        EditorGUILayout.LabelField($"활성화된 씬 수: {scenes.Length}");
        if (scenes.Length == 0)
        {
            EditorGUILayout.HelpBox("Build Settings에 활성화된 씬이 없습니다. (File → Build Settings에서 씬 추가/체크)", MessageType.Error);
        }
    }

    // -------- 빌드 실행 --------
    void RunAndroidBuild()
    {
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("빌드 중단", "활성화된 씬이 없습니다. Build Settings에서 씬을 추가하세요.", "확인");
                return;
            }

            // 0) Android 심볼/매핑 설정 적용
            ApplyAndroidSymbolSettings();
            ApplyAndroidMinifySettings();

            // 1) 최종 bundleVersion 결정
            string finalBundleVersion;
            if (manualBumpTouched == true)
            {
                finalBundleVersion = $"{verMajor}.{verMinor}.{verPatch}";
                if (!TryNormalizeSemVer(finalBundleVersion, out finalBundleVersion))
                {
                    EditorUtility.DisplayDialog("버전 형식 오류", "bundleVersion은 '메이저.마이너.패치' 형식이어야 합니다. 예: 1.2.3", "확인");
                    return;
                }
            }
            else
            {
                finalBundleVersion = IncrementPatchSafe(PlayerSettings.bundleVersion);
                ParseSemVer(finalBundleVersion, out verMajor, out verMinor, out verPatch);
            }
            PlayerSettings.bundleVersion = finalBundleVersion;

            // 2) bundleVersionCode 반영
            buildNumber = Mathf.Clamp(buildNumber, 1, 999999);
            PlayerSettings.Android.bundleVersionCode = buildNumber;

            // 3) 키스토어 자동 적용
            if (applyKeystoreOnBuild)
            {
                if (string.IsNullOrWhiteSpace(keystorePath) ||
                    string.IsNullOrWhiteSpace(aliasName) ||
                    string.IsNullOrWhiteSpace(keystorePass) ||
                    string.IsNullOrWhiteSpace(keyaliasPass))
                {
                    bool cont = EditorUtility.DisplayDialog(
                        "키스토어 정보 부족",
                        "키스토어 자동 적용이 켜져 있으나 입력이 비었습니다.\n이 상태로 진행하면 서명 실패/미서명일 수 있습니다.\n계속하시겠습니까?",
                        "계속", "취소");
                    if (!cont) return;
                }
                ApplyAndroidKeystore();
            }

            // 4) 출력 경로/파일명
            string outDir = string.IsNullOrWhiteSpace(outputDir) ? "Builds/Android" : outputDir.Trim();
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string product = SanitizeFileName(Application.productName);
            string bn3 = FormatBuildNumber(buildNumber);
            string time = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string ext = useAAB ? "aab" : "apk";
            string fileName = $"{product}_{finalBundleVersion}_b{bn3}_{time}.{ext}";
            string locationPathName = Path.Combine(outDir, fileName);

            // 5) 빌드 옵션
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                locationPathName = locationPathName,
                options = BuildOptions.None
            };
            EditorUserBuildSettings.buildAppBundle = useAAB;

            // 6) 빌드 실행
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result == BuildResult.Succeeded)
            {
                // 6-1) 심볼/매핑 자동 수집 → 출력 폴더로 복사
                TryCollectAndCopySymbols(report, outDir, product, finalBundleVersion, bn3, time);

                // 6-2) 성공 시에만 코드 +1 / 상태 저장
                buildNumber = Mathf.Clamp(buildNumber + 1, 1, 999999);
                SavePrefs();

                // 다음 빌드를 위해 수동 조절 상태 초기화
                manualBumpTouched = false;

                EditorUtility.RevealInFinder(locationPathName);
                EditorUtility.DisplayDialog(
                    "빌드 성공",
                    $"파일: {locationPathName}\n" +
                    $"bundleVersion: {finalBundleVersion}\n" +
                    $"bundleVersionCode: {PlayerSettings.Android.bundleVersionCode}",
                    "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("빌드 실패", report.summary.result.ToString(), "확인");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("예외 발생", ex.Message, "확인");
        }
    }

    void ApplyAndroidKeystore()
    {
        PlayerSettings.Android.useCustomKeystore = true;
        if (!string.IsNullOrWhiteSpace(keystorePath))
            PlayerSettings.Android.keystoreName = keystorePath;

        if (!string.IsNullOrWhiteSpace(keystorePass))
            PlayerSettings.Android.keystorePass = keystorePass;

        if (!string.IsNullOrWhiteSpace(aliasName))
            PlayerSettings.Android.keyaliasName = aliasName;

        if (!string.IsNullOrWhiteSpace(keyaliasPass))
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
    }

    // -------- 심볼/매핑 적용 --------
    void ApplyAndroidSymbolSettings()
    {
        // 최신: EditorUserBuildSettings.androidCreateSymbols (enum AndroidCreateSymbols: None=0, Public=1, Debugging=2)
        // 구버전: EditorUserBuildSettings.androidCreateSymbolsZip (bool)
        if (!TrySetAndroidCreateSymbols(androidSymbolMode))
        {
            // enum이 없으면 구버전 bool을 시도
            TrySetAndroidCreateSymbolsZip(androidCreateSymbolsZipBool || androidSymbolMode != 0);
        }
    }

    void ApplyAndroidMinifySettings()
    {
        try
        {
            PlayerSettings.Android.minifyRelease = androidMinifyRelease; // true면 mapping.txt 생성
        }
        catch
        {
            // 일부 구버전/플랫폼 설정에서 접근 불가할 수 있음 → 무시
        }
    }

    void TryCollectAndCopySymbols(BuildReport report, string outDir, string product, string version, string bn3, string time)
    {
        // BuildReport에 포함된 파일 목록을 스캔해서 symbols.zip / mapping.txt를 찾는다.
        // 찾으면 산출물 폴더(outDir)로 복사한다.
        int copied = 0;
        try
        {
            foreach (var f in report.GetFiles())
            {
                string p = f.path.Replace('\\', '/');
                string lower = p.ToLowerInvariant();

                bool isSymbolsZip = lower.EndsWith("symbols.zip") || (lower.Contains("symbols") && lower.EndsWith(".zip"));
                bool isMappingTxt = lower.EndsWith("mapping.txt");

                if (isSymbolsZip || isMappingTxt)
                {
                    string name = Path.GetFileName(p);
                    string destName = $"{product}_{version}_b{bn3}_{time}__{name}";
                    string destPath = Path.Combine(outDir, destName);
                    SafeCopy(p, destPath);
                    copied++;
                    Debug.Log($"[BuildAssistant] Copied symbol/mapping: {p} → {destPath}");
                }
            }

            if (copied == 0)
            {
                Debug.Log("[BuildAssistant] No symbols.zip or mapping.txt found in BuildReport. " +
                          "If you expected them, ensure the options are enabled and check your Unity version.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildAssistant] Symbol collection failed: {e.Message}");
        }
    }

    static void SafeCopy(string src, string dst)
    {
        try
        {
            if (File.Exists(dst)) File.Delete(dst);
            var dir = Path.GetDirectoryName(dst);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            FileUtil.CopyFileOrDirectory(src, dst);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildAssistant] Copy failed: {src} → {dst} ({e.Message})");
        }
    }

    // -------- 리플렉션: Android CreateSymbols --------
    static bool HasAndroidCreateSymbolsEnum()
    {
        var t = typeof(EditorUserBuildSettings);
        var prop = t.GetProperty("androidCreateSymbols", BindingFlags.Public | BindingFlags.Static);
        return prop != null && prop.PropertyType.IsEnum;
    }

    static bool TryGetAndroidCreateSymbols(out int mode)
    {
        mode = 0;
        try
        {
            var t = typeof(EditorUserBuildSettings);
            var prop = t.GetProperty("androidCreateSymbols", BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.PropertyType.IsEnum) return false;
            object enumVal = prop.GetValue(null, null);
            mode = (int)Convert.ChangeType(enumVal, typeof(int));
            return true;
        }
        catch { return false; }
    }

    static bool TrySetAndroidCreateSymbols(int mode)
    {
        try
        {
            var t = typeof(EditorUserBuildSettings);
            var prop = t.GetProperty("androidCreateSymbols", BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.PropertyType.IsEnum) return false;
            var enumType = prop.PropertyType;
            // 유효 범위 강제
            if (mode < 0) mode = 0;
            if (mode > 2) mode = 2;
            object enumVal = Enum.ToObject(enumType, mode);
            prop.SetValue(null, enumVal, null);
            return true;
        }
        catch { return false; }
    }

    static bool TrySetAndroidCreateSymbolsZip(bool on)
    {
        try
        {
            var t = typeof(EditorUserBuildSettings);
            var prop = t.GetProperty("androidCreateSymbolsZip", BindingFlags.Public | BindingFlags.Static);
            if (prop == null || prop.PropertyType != typeof(bool)) return false;
            prop.SetValue(null, on, null);
            return true;
        }
        catch { return false; }
    }

    // -------- 유틸: SemVer --------
    static void ParseSemVer(string s, out int major, out int minor, out int patch)
    {
        major = 1; minor = 0; patch = 0;
        if (string.IsNullOrWhiteSpace(s)) return;
        var t = s.Trim().Split('.');
        int.TryParse(t.Length > 0 ? t[0] : "1", out major);
        int.TryParse(t.Length > 1 ? t[1] : "0", out minor);
        int.TryParse(t.Length > 2 ? t[2] : "0", out patch);
        if (major < 0) major = 0;
        if (minor < 0) minor = 0;
        if (patch < 0) patch = 0;
    }

    static bool TryNormalizeSemVer(string input, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var t = input.Trim().Split('.');
        if (t.Length < 1 || t.Length > 3) return false;

        if (!int.TryParse(t[0], out int major)) return false;
        int minor = 0, patch = 0;
        if (t.Length >= 2 && !int.TryParse(t[1], out minor)) return false;
        if (t.Length == 3 && !int.TryParse(t[2], out patch)) return false;
        if (major < 0 || minor < 0 || patch < 0) return false;

        normalized = $"{major}.{minor}.{patch}";
        return true;
    }

    static string IncrementPatchSafe(string ver)
    {
        if (!TryNormalizeSemVer(ver, out var normalized))
            normalized = "1.0.0";
        var parts = normalized.Split('.');
        int major = SafeParse(parts[0], 1);
        int minor = SafeParse(parts[1], 0);
        int patch = SafeParse(parts[2], 0);
        patch = Mathf.Max(0, patch + 1);
        return $"{major}.{minor}.{patch}";
    }

    static int SafeParse(string s, int fallback)
    {
        return int.TryParse(s, out var v) ? v : fallback;
    }

    // -------- 유틸: 파일/숫자 --------
    static string FormatBuildNumber(int n)
    {
        if (n < 0) n = 0;
        if (n <= 999) return n.ToString("D3"); // 3자리 패딩
        return n.ToString();
    }

    static int ParseBuildNumberSafe(string text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (int.TryParse(text.TrimStart('0'), out var v))
        {
            if (v <= 0) v = 0;
            return v;
        }
        if (int.TryParse(text, out var v2)) return Mathf.Max(0, v2);
        return fallback;
    }

    static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) name = name.Replace(c, '_');
        return name;
    }
}
#endif
