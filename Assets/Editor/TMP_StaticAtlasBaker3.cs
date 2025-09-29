// File: Assets/Editor/TMP_StaticAtlasBaker3.cs
// 목적: 세 개의 문자 파일(TextAsset)을 합집합으로 묶어
//       대상 TMP_FontAsset에 글리프를 채우고 Static으로 고정.
// 자동화: Assets/Fonts/txt(기본) 등 지정 폴더에서
//       harvested_cs_chars(.txt), harvested_chars(.txt), kr_tmp_ui_common(.txt)을 자동 할당.
//
// 메뉴: Tools/TMP/Static Atlas Baker (3-in-1)

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TMPro;

public class TMP_StaticAtlasBaker3 : EditorWindow
{
    [Header("Target Font")]
    public TMP_FontAsset targetFont;

    [Header("Auto Source Lookup")]
    public string lookupFolder = "Assets/Fonts/txt";
    public bool autoAssignOnOpen = true;

    [Header("Three Sources (auto-assign tries below names)")]
    public TextAsset harvestedCsChars;  // harvested_cs_chars(.txt)
    public TextAsset harvestedChars;    // harvested_chars(.txt)
    public TextAsset krUiCommon;        // kr_tmp_ui_common(.txt)

    [Header("Options")]
    public bool addNBSP = true;             // U+00A0
    public bool ignoreControlChars = true;  // \r \n \t 제외
    public bool clearBeforeBake = true;     // 베이크 전에 기존 데이터 정리

    private Vector2 _scroll;

    private static readonly string[] WantNamesCs   = new[] { "harvested_cs_chars", "harvested_cs_chars.txt" };
    private static readonly string[] WantNamesUi   = new[] { "harvested_chars", "harvested_chars.txt" };
    private static readonly string[] WantNamesComm = new[] { "kr_tmp_ui_common", "kr_tmp_ui_common.txt" };

    [MenuItem("Tools/TMP/Static Atlas Baker (3-in-1)")]
    public static void Open()
    {
        var w = GetWindow<TMP_StaticAtlasBaker3>("TMP Static Atlas Baker");
        w.minSize = new Vector2(560, 520);
    }

    private void OnEnable()
    {
        if (autoAssignOnOpen)
        {
            TryAutoAssign();
        }
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Target Font Asset", EditorStyles.boldLabel);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font Asset", targetFont, typeof(TMP_FontAsset), false);
        using (new EditorGUI.DisabledScope(targetFont == null))
        {
            EditorGUILayout.LabelField($"Atlas Mode: {targetFont?.atlasPopulationMode}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Glyph Count: {targetFont?.glyphTable?.Count}", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Character Count: {targetFont?.characterTable?.Count}", EditorStyles.miniBoldLabel);
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Auto Source Lookup", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        lookupFolder = EditorGUILayout.TextField("Lookup Folder", lookupFolder);
        if (GUILayout.Button("Auto-Assign", GUILayout.Width(110)))
        {
            TryAutoAssign(showDialog:true);
        }
        EditorGUILayout.EndHorizontal();
        autoAssignOnOpen = EditorGUILayout.Toggle(new GUIContent("Auto-assign on Open"), autoAssignOnOpen);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Sources (auto-filled if found)", EditorStyles.boldLabel);
        harvestedCsChars = (TextAsset)EditorGUILayout.ObjectField("harvested_cs_chars.txt", harvestedCsChars, typeof(TextAsset), false);
        harvestedChars   = (TextAsset)EditorGUILayout.ObjectField("harvested_chars.txt", harvestedChars, typeof(TextAsset), false);
        krUiCommon       = (TextAsset)EditorGUILayout.ObjectField("kr_tmp_ui_common.txt", krUiCommon, typeof(TextAsset), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        addNBSP = EditorGUILayout.Toggle(new GUIContent("Add NBSP (U+00A0)"), addNBSP);
        ignoreControlChars = EditorGUILayout.Toggle(new GUIContent("Ignore Control Chars (\\r,\\n,\\t)"), ignoreControlChars);
        clearBeforeBake = EditorGUILayout.Toggle(new GUIContent("Clear Before Bake (권장)"), clearBeforeBake);

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Preview Unique Count", GUILayout.Height(28)))
        {
            var chars = BuildUnionString(out int count);
            EditorUtility.DisplayDialog("Preview", $"고유 코드포인트: {count}\n미리보기(앞 200자):\n{Truncate(chars, 200)}", "확인");
        }

        using (new EditorGUI.DisabledScope(targetFont == null))
        {
            if (GUILayout.Button("Bake → Make Static (원클릭)", GUILayout.Height(36)))
            {
                try
                {
                    BakeAndMakeStatic();
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    EditorUtility.DisplayDialog("Static Atlas Baker", "오류 발생:\n" + ex.Message, "확인");
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
@"워크플로우:
1) Lookup Folder에서 소스 3개를 자동 할당하거나, 수동으로 드래그해도 됩니다.
2) [Bake → Make Static] 클릭
   - 폰트를 일시적으로 Dynamic으로 전환
   - 선택 시 기존 데이터 클리어
   - 세 파일의 문자 '합집합'을 TryAddCharacters로 채움
   - Atlas 모드를 Static으로 고정
* 폰트 에셋 인스펙터의 '아틀라스 해상도/패딩/렌더 모드'를 원하는 값으로 먼저 세팅하세요.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // === Auto assign ===

    private void TryAutoAssign(bool showDialog = false)
    {
        harvestedCsChars = FindTextAssetByNames(WantNamesCs, lookupFolder);
        harvestedChars   = FindTextAssetByNames(WantNamesUi, lookupFolder);
        krUiCommon       = FindTextAssetByNames(WantNamesComm, lookupFolder);

        // 폴더 내에서 못 찾으면 프로젝트 전체에서 보조 검색
        if (harvestedCsChars == null) harvestedCsChars = FindTextAssetByNames(WantNamesCs, null);
        if (harvestedChars   == null) harvestedChars   = FindTextAssetByNames(WantNamesUi, null);
        if (krUiCommon       == null) krUiCommon       = FindTextAssetByNames(WantNamesComm, null);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Auto-Assign",
                $"harvested_cs_chars: {(harvestedCsChars ? AssetDatabase.GetAssetPath(harvestedCsChars) : "Not found")}\n" +
                $"harvested_chars   : {(harvestedChars   ? AssetDatabase.GetAssetPath(harvestedChars)   : "Not found")}\n" +
                $"kr_tmp_ui_common  : {(krUiCommon       ? AssetDatabase.GetAssetPath(krUiCommon)       : "Not found")}",
                "확인");
        }
        Repaint();
    }

    private TextAsset FindTextAssetByNames(string[] wantNames, string folderOrNull)
    {
        // 우선 지정 폴더에서 정확/유사이름 검색 → 없으면 전체 검색
        string[] searchIn = (string.IsNullOrEmpty(folderOrNull) ? new[] { "Assets" } : new[] { folderOrNull });

        // t:TextAsset 으로 후보 수집 후 이름/경로로 후보 점수화
        var guids = AssetDatabase.FindAssets("t:TextAsset", searchIn);
        string bestPath = null;
        int bestScore = int.MinValue;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(path);

            int score = ScoreNameMatch(fileName, fileNameNoExt, wantNames);
            if (score > bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }

        if (bestPath == null) return null;
        return AssetDatabase.LoadAssetAtPath<TextAsset>(bestPath);
    }

    private int ScoreNameMatch(string fileName, string fileNameNoExt, string[] wants)
    {
        // 이름 일치 정도에 따라 점수 부여
        // 정확히 동일(확장자 포함/미포함) > 대소문자 무시 동일 > 포함(match)
        int score = int.MinValue;
        foreach (var want in wants)
        {
            if (string.Equals(fileName, want, StringComparison.Ordinal))
                score = Math.Max(score, 100);
            if (string.Equals(fileNameNoExt, want, StringComparison.Ordinal))
                score = Math.Max(score, 100);

            if (string.Equals(fileName, want, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 90);
            if (string.Equals(fileNameNoExt, want, StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 90);

            if (fileName.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileNameNoExt.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                score = Math.Max(score, 60);
        }

        // 동일 점수면 경로가 짧은 쪽 선호
        if (score == int.MinValue) score = 0;
        return score;
    }

    // === Bake ===

    private void BakeAndMakeStatic()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Static Atlas Baker", "대상 TMP_FontAsset을 지정하세요.", "확인");
            return;
        }

        string chars = BuildUnionString(out int uniqueCount);
        if (uniqueCount == 0)
        {
            EditorUtility.DisplayDialog("Static Atlas Baker", "추가할 문자가 없습니다.", "확인");
            return;
        }

        var prevMode = targetFont.atlasPopulationMode;
        targetFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        EditorUtility.SetDirty(targetFont);

        if (clearBeforeBake)
        {
            targetFont.ClearFontAssetData(true);
            EditorUtility.SetDirty(targetFont);
        }

        bool added = targetFont.TryAddCharacters(chars, out string missing);
        EditorUtility.SetDirty(targetFont);
        AssetDatabase.SaveAssets();

        if (string.IsNullOrEmpty(missing) == false && targetFont.fallbackFontAssetTable != null)
        {
            foreach (var fb in targetFont.fallbackFontAssetTable)
            {
                if (string.IsNullOrEmpty(missing)) break;
                if (fb == null) continue;

                var fbPrev = fb.atlasPopulationMode;
                fb.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fb.TryAddCharacters(missing, out string stillMissing);
                EditorUtility.SetDirty(fb);
                fb.atlasPopulationMode = fbPrev;
                missing = stillMissing;
            }
            AssetDatabase.SaveAssets();
        }

        targetFont.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(targetFont);
        AssetDatabase.SaveAssets();

        var addedText = added ? "OK" : "PARTIAL";
        EditorUtility.DisplayDialog(
            "Static Atlas Baker",
            $"Requested: {uniqueCount}\nTarget Added: {addedText}\nMissing(after fallbacks): {(missing?.Length ?? 0)}\n\n완료: Static 아틀라스로 고정되었습니다.",
            "확인");

    }

    private string BuildUnionString(out int uniqueCount)
    {
        var set = new HashSet<int>();

        void AddTextAsset(TextAsset ta)
        {
            if (ta == null) return;
            string s = ta.text;
            if (string.IsNullOrEmpty(s)) return;

            for (int i = 0; i < s.Length; i++)
            {
                int cp = char.ConvertToUtf32(s, i);
                if (cp > 0xFFFF) i++;
                if (ignoreControlChars && (cp == '\r' || cp == '\n' || cp == '\t')) continue;
                set.Add(cp);
            }
        }

        AddTextAsset(harvestedCsChars);
        AddTextAsset(harvestedChars);
        AddTextAsset(krUiCommon);

        if (addNBSP) set.Add(0x00A0);

        var sb = new StringBuilder(set.Count);
        foreach (int cp in set.OrderBy(x => x))
        {
            sb.Append(char.ConvertFromUtf32(cp));
        }

        uniqueCount = set.Count;
        return sb.ToString();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max) + "...";
    }
}
