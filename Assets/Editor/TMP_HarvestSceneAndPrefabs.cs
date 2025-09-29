// File: Assets/Editor/TMP_HarvestSceneAndPrefabs.cs
// 목적: 열린 씬 + 프로젝트 프리팹의 TMP_Text.text를 수집해
//       고유 문자만 모아 harvested_chars.txt 로 "오버라이트" 저장.
// 메뉴: Tools/TMP/Harvest Scene+Prefabs → harvested_chars.txt 생성/덮어쓰기

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TMP_HarvestSceneAndPrefabs
{
    // 출력 파일 경로
    private const string OutputPath = "Assets/Fonts/txt/harvested_chars.txt";

    [MenuItem("Tools/TMP/Harvest Scene+Prefabs (Overwrite)")]
    public static void Harvest()
    {
        try
        {
            var sb = new StringBuilder();

            // 1) 열린 모든 씬에서 TMP_Text 수집
            int openSceneCount = SceneManager.sceneCount;
            for (int i = 0; i < openSceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded == false) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    var texts = root.GetComponentsInChildren<TMP_Text>(true);
                    foreach (var t in texts)
                    {
                        if (string.IsNullOrEmpty(t.text) == false)
                        {
                            sb.AppendLine(t.text);
                        }
                    }
                }
            }

            // 2) 프로젝트의 모든 프리팹에서 TMP_Text 수집
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            int total = prefabGuids.Length;
            for (int idx = 0; idx < total; idx++)
            {
                string guid = prefabGuids[idx];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (EditorUtility.DisplayCancelableProgressBar("Harvest Prefabs", path, (float)(idx + 1) / total))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("TMP Harvest", "사용자 취소", "확인");
                    return;
                }

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    if (string.IsNullOrEmpty(t.text) == false)
                    {
                        sb.AppendLine(t.text);
                    }
                }
            }
            EditorUtility.ClearProgressBar();

            // 3) 고유 코드포인트로 압축 (개행/탭 제거, NBSP 추가)
            var unique = new HashSet<int>();
            string all = sb.ToString();

            for (int i = 0; i < all.Length; i++)
            {
                int cp = char.ConvertToUtf32(all, i);
                if (cp > 0xFFFF) i++;
                if (cp == '\r' || cp == '\n' || cp == '\t') continue;
                unique.Add(cp);
            }
            // NBSP(U+00A0) 추가 권장
            unique.Add(0x00A0);

            // 4) 문자열로 변환
            var outSb = new StringBuilder(unique.Count);
            foreach (int cp in unique.OrderBy(x => x))
            {
                outSb.Append(char.ConvertFromUtf32(cp));
            }

            // 5) 파일 쓰기(UTF-8, BOM 없음) — 오버라이트
            string dir = Path.GetDirectoryName(OutputPath);
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);
            File.WriteAllText(OutputPath, outSb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("TMP Harvest", $"고유 코드포인트: {unique.Count}\n저장: {OutputPath}", "확인");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("TMP Harvest", "오류 발생:\n" + ex.Message, "확인");
        }
    }
}
