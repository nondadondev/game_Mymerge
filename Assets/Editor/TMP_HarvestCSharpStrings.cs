// File: Assets/Editor/TMP_HarvestCSharpStrings.cs
// 목적: .cs 파일의 문자열 리터럴을 추출해 harvested_cs_chars.txt 생성.
//       1) Incremental(캐시 기반 변경분만)  2) Full Rescan(전체 새로 읽기)
// 메뉴:
//   - Tools/TMP/Harvest C# Strings (Incremental)
//   - Tools/TMP/Harvest C# Strings (Full Rescan)

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TMP_HarvestCSharpStrings
{
    private const string OutputPath = "Assets/Fonts/txt/harvested_cs_chars.txt";
    private const string CachePath  = "ProjectSettings/tmpscan_cs_cache.json";

    // 스캔 포함/제외 폴더(필요시 수정)
    private static readonly string[] IncludeFolders = new[] { "Assets/Scripts", "Assets" };
    private static readonly string[] ExcludeFolders = new[] { "Assets/Plugins", "Assets/ThirdParty", "Assets/Generated" };

    [Serializable]
    private class CacheData
    {
        public Dictionary<string, string> fileHash = new Dictionary<string, string>(); // path→md5
        public string lastOutput = "";
    }

    [MenuItem("Tools/TMP/Harvest C# Strings (Incremental)")]
    public static void RunIncremental()
    {
        Run(incremental: true);
    }

    [MenuItem("Tools/TMP/Harvest C# Strings (Full Rescan)")]
    public static void RunFull()
    {
        Run(incremental: false);
    }

    private static void Run(bool incremental)
    {
        try
        {
            var allCs = CollectCsPaths(IncludeFolders, ExcludeFolders);
            if (allCs.Count == 0)
            {
                EditorUtility.DisplayDialog("C# Harvester", "스캔할 .cs 파일이 없습니다.", "확인");
                return;
            }

            var cache = LoadCache(CachePath);
            var aggregated = new StringBuilder(1024);
            var unique = new HashSet<int>();

            int total = allCs.Count;
            int processed = 0;

            for (int i = 0; i < allCs.Count; i++)
            {
                processed = i + 1;
                string path = allCs[i];

                if (EditorUtility.DisplayCancelableProgressBar(
                    incremental ? "Harvest C# (Incremental)" : "Harvest C# (Full Rescan)",
                    path,
                    (float)processed / total))
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("C# Harvester", "사용자 취소", "확인");
                    return;
                }

                string md5 = ComputeMD5(path);
                string old;
                cache.fileHash.TryGetValue(path, out old);

                bool needRead = true;
                if (incremental == true)
                {
                    if (string.Equals(old, md5, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        needRead = false;
                    }
                }

                if (needRead == true)
                {
                    string code = File.ReadAllText(path, new UTF8Encoding(true));
                    string noComments = StripComments(code);
                    foreach (var s in ExtractStringLiterals(noComments))
                    {
                        if (string.IsNullOrWhiteSpace(s) == false)
                        {
                            aggregated.AppendLine(s);
                        }
                    }
                    cache.fileHash[path] = md5;
                }
            }

            EditorUtility.ClearProgressBar();

            string all = aggregated.ToString();
            for (int i = 0; i < all.Length; i++)
            {
                int cp = char.ConvertToUtf32(all, i);
                if (cp > 0xFFFF) i++;
                if (cp == '\r' || cp == '\n' || cp == '\t') continue;
                unique.Add(cp);
            }
            // NBSP 추가 권장
            unique.Add(0x00A0);

            var outSb = new StringBuilder(unique.Count);
            foreach (int cp in unique.OrderBy(x => x))
            {
                outSb.Append(char.ConvertFromUtf32(cp));
            }

            string dir = Path.GetDirectoryName(OutputPath);
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);
            File.WriteAllText(OutputPath, outSb.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();

            cache.lastOutput = outSb.ToString();
            SaveCache(CachePath, cache);

            EditorUtility.DisplayDialog(
                "C# Harvester",
                $"고유 코드포인트: {unique.Count}\n저장: {OutputPath}\n처리 파일: {processed}/{total}",
                "확인");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("C# Harvester", "오류 발생:\n" + ex.Message, "확인");
        }
    }

    private static List<string> CollectCsPaths(string[] includes, string[] excludes)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includes != null && includes.Length > 0)
        {
            foreach (var inc in includes)
            {
                if (string.IsNullOrWhiteSpace(inc) == true) continue;
                var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { inc });
                foreach (var guid in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (IsExcluded(p, excludes) == false)
                        {
                            set.Add(p);
                        }
                    }
                }
            }
        }

        if (set.Count == 0)
        {
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (IsExcluded(p, excludes) == false)
                    {
                        set.Add(p);
                    }
                }
            }
        }

        return set.ToList();
    }

    private static bool IsExcluded(string assetPath, string[] excludes)
    {
        if (excludes == null) return false;
        foreach (var ex in excludes)
        {
            if (string.IsNullOrWhiteSpace(ex) == true) continue;
            if (assetPath.StartsWith(ex, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }
        return false;
    }

    private static string ComputeMD5(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(fullPath))
        {
            var hash = md5.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static CacheData LoadCache(string path)
    {
        try
        {
            if (File.Exists(path) == true)
            {
                var json = File.ReadAllText(path, new UTF8Encoding(false));
                var data = JsonUtility.FromJson<CacheData>(json);
                if (data != null) return data;
            }
        }
        catch { }
        return new CacheData();
    }

    private static void SaveCache(string path, CacheData data)
    {
        try
        {
            var json = JsonUtility.ToJson(data, prettyPrint: true);
            string dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[C# Harvester] 캐시 저장 실패: {ex.Message}");
        }
    }

    // ===== 주석 제거 + 문자열 추출(간이 스캐너) =====

    private static string StripComments(string code)
    {
        var sb = new StringBuilder(code.Length);
        bool inBlock = false;
        bool inLine = false;

        for (int i = 0; i < code.Length; i++)
        {
            if (inBlock == false && inLine == false && i + 1 < code.Length && code[i] == '/' && code[i + 1] == '*')
            {
                inBlock = true; i++; continue;
            }
            if (inBlock == true && i + 1 < code.Length && code[i] == '*' && code[i + 1] == '/')
            {
                inBlock = false; i++; continue;
            }
            if (inBlock == false && inLine == false && i + 1 < code.Length && code[i] == '/' && code[i + 1] == '/')
            {
                inLine = true; i++; continue;
            }
            if (inLine == true && (code[i] == '\n' || code[i] == '\r'))
            {
                inLine = false;
            }

            if (inBlock == false && inLine == false) sb.Append(code[i]);
        }
        return sb.ToString();
    }

    private static IEnumerable<string> ExtractStringLiterals(string code)
    {
        var results = new List<string>();
        int i = 0;

        while (i < code.Length)
        {
            bool isDollar = false;
            bool isVerbatim = false;
            int start = i;

            bool advanced = true;
            while (advanced == true && i < code.Length)
            {
                advanced = false;
                if (code[i] == '$') { isDollar = true; i++; advanced = true; }
                if (i < code.Length && code[i] == '@') { isVerbatim = true; i++; advanced = true; }
            }

            if (i < code.Length && code[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                bool closed = false;

                while (i < code.Length)
                {
                    char ch = code[i++];
                    if (isVerbatim == true)
                    {
                        if (ch == '"')
                        {
                            if (i < code.Length && code[i] == '"')
                            {
                                sb.Append('"'); i++; continue;
                            }
                            else
                            {
                                closed = true; break;
                            }
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                    }
                    else
                    {
                        if (ch == '\\')
                        {
                            if (i < code.Length)
                            {
                                char nx = code[i++];
                                switch (nx)
                                {
                                    case 'n': sb.Append('\n'); break;
                                    case 'r': sb.Append('\r'); break;
                                    case 't': sb.Append('\t'); break;
                                    case '\\': sb.Append('\\'); break;
                                    case '"': sb.Append('"'); break;
                                    default: sb.Append(nx); break;
                                }
                            }
                        }
                        else if (ch == '"')
                        {
                            closed = true; break;
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                    }
                }

                if (closed == true && sb.Length > 0)
                {
                    results.Add(sb.ToString());
                }
            }
            else
            {
                i = start + 1;
            }
        }

        return results;
    }
}
