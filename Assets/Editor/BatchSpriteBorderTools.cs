// File: Assets/Editor/BatchSpriteBorderTools.cs
// Unity 2020+ 호환. 에디터 전용.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

public class BatchSpriteBorderSetterWindow : EditorWindow
{
    // 입력 모드
    private enum BorderMode { Pixels, Percent, AutoDetectAlpha }

    private BorderMode mode = BorderMode.Pixels;

    // 픽셀/퍼센트 입력
    private int leftPx = 32, rightPx = 32, topPx = 32, bottomPx = 32;
    private float leftPercent = 0.12f, rightPercent = 0.12f, topPercent = 0.12f, bottomPercent = 0.12f;

    // 공통 옵션
    private bool forceSprite2DUI = true;
    private bool setMeshFullRect = true;
    private bool setPPU = false;
    private int pixelsPerUnit = 100;
    private bool setCompression = false;
    private TextureImporterCompression compression = TextureImporterCompression.Uncompressed;

    // 자동 감지 옵션
    private bool autodetectClampToEven = true;   // 보더가 9-slice에 깔끔하게 떨어지도록 짝수 맞춤
    private byte alphaThreshold = 5;              // 0~255
    private int marginSafety = 1;                 // 감지선 안쪽으로 여유 픽셀

    // 대상 폴더
    private DefaultAsset targetFolder;

    [MenuItem("Tools/UI/Batch Sprite Border Setter")]
    private static void Open()
    {
        var win = GetWindow<BatchSpriteBorderSetterWindow>();
        win.titleContent = new GUIContent("Sprite Border Setter");
        win.minSize = new Vector2(420, 480);
        win.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch Sprite Border Setter", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        // 모드 선택
        EditorGUILayout.LabelField("Border Mode", EditorStyles.boldLabel);
        mode = (BorderMode)EditorGUILayout.EnumPopup("Mode", mode);

        if (mode == BorderMode.Pixels)
        {
            EditorGUILayout.LabelField("Pixels (absolute)", EditorStyles.miniBoldLabel);
            leftPx   = EditorGUILayout.IntField("Left (px)",   leftPx);
            rightPx  = EditorGUILayout.IntField("Right (px)",  rightPx);
            topPx    = EditorGUILayout.IntField("Top (px)",    topPx);
            bottomPx = EditorGUILayout.IntField("Bottom (px)", bottomPx);
        }
        else if (mode == BorderMode.Percent)
        {
            EditorGUILayout.LabelField("Percent of sprite rect (0~1)", EditorStyles.miniBoldLabel);
            leftPercent   = Mathf.Clamp01(EditorGUILayout.FloatField("Left (%)",   leftPercent));
            rightPercent  = Mathf.Clamp01(EditorGUILayout.FloatField("Right (%)",  rightPercent));
            topPercent    = Mathf.Clamp01(EditorGUILayout.FloatField("Top (%)",    topPercent));
            bottomPercent = Mathf.Clamp01(EditorGUILayout.FloatField("Bottom (%)", bottomPercent));
        }
        else
        {
            EditorGUILayout.LabelField("Auto Detect by Alpha (transparent edges)", EditorStyles.miniBoldLabel);
            alphaThreshold = (byte)Mathf.Clamp(EditorGUILayout.IntSlider("Alpha Threshold", alphaThreshold, 0, 255), 0, 255);
            marginSafety = EditorGUILayout.IntField("Inner Safety Margin (px)", marginSafety);
            autodetectClampToEven = EditorGUILayout.Toggle("Clamp to Even Pixels", autodetectClampToEven);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Import/Common Options", EditorStyles.boldLabel);
        forceSprite2DUI = EditorGUILayout.Toggle("Force TextureType: Sprite(2D/UI)", forceSprite2DUI);
        setMeshFullRect = EditorGUILayout.Toggle("Mesh Type: Full Rect", setMeshFullRect);
        setPPU = EditorGUILayout.Toggle("Set Pixels Per Unit", setPPU);
        if (setPPU == true) pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);
        setCompression = EditorGUILayout.Toggle("Set Compression", setCompression);
        if (setCompression == true) compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compression);

        EditorGUILayout.Space(10);
        // 선택에 적용
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply To Selection", GUILayout.Height(32)))
            {
                var paths = Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath)
                                                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                            p.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
                                                            p.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                                                .ToArray();
                ApplyToPaths(paths);
            }
        }

        // 폴더에 적용
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("Apply To Folder (recursive)", GUILayout.Height(26)))
        {
            if (targetFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(targetFolder);
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                var paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                                 .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                             p.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ||
                                             p.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                                 .ToArray();
                ApplyToPaths(paths);
            }
            else
            {
                EditorUtility.DisplayDialog("Folder Required", "대상 폴더를 지정하세요.", "확인");
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "권장: 9-slice는 Full Rect 메시 사용. Atlas에서는 Tight Packing/Rotation을 끄세요. " +
            "Alpha Auto Detect는 투명 배경의 라운드 버튼/패널에 효과적입니다.", MessageType.Info);
    }

    private void ApplyToPaths(string[] texturePaths)
    {
        int ok = 0, fail = 0;
        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var path in texturePaths)
            {
                try
                {
                    bool changed = ProcessOneTexture(path);
                    if (changed == true) ok++;
                    else ok++; // 적용할 게 없어도 OK로 집계
                }
                catch (Exception e)
                {
                    fail++;
                    Debug.LogError($"[SpriteBorderSetter] Fail: {path}\n{e}");
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Done",
            $"Processed: {texturePaths.Length}\nSuccess: {ok}\nFail: {fail}", "OK");
    }

    private bool ProcessOneTexture(string path)
{
    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
    if (importer == null) return false;

    // 1) 스프라이트 강제 설정 (필요시)
    if (forceSprite2DUI == true)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = (importer.spriteImportMode == SpriteImportMode.Multiple)
            ? SpriteImportMode.Multiple
            : SpriteImportMode.Single;
    }

    // 2) 압축/PPU 설정 (옵션)
    if (setCompression == true) importer.textureCompression = compression;
    if (setPPU == true) importer.spritePixelsPerUnit = pixelsPerUnit;

    // 3) MeshType = FullRect 강제 (심볼 이슈 방지: TextureImporterSettings 경유)
    if (setMeshFullRect == true)
    {
        var tis = new TextureImporterSettings();
        importer.ReadTextureSettings(tis);
        tis.spriteMeshType = SpriteMeshType.FullRect; // ★ 핵심: 직접 할당 대신 Settings 경유
        importer.SetTextureSettings(tis);
    }

    // 4) Single vs Multiple
    if (importer.spriteImportMode == SpriteImportMode.Single)
    {
        Vector4 border = CalculateBorderForSingle(path, importer);
        importer.spriteBorder = border; // Vector4 순서: (Left, Bottom, Right, Top)
        importer.SaveAndReimport();
        return true;
    }
    else
    {
        // Multiple: 각 SpriteMetaData에 border 설정
        var metas = importer.spritesheet;
        if (metas == null || metas.Length == 0)
        {
            importer.SaveAndReimport();
            return false;
        }

        // 원본 텍스쳐 크기
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int texW = tex != null ? tex.width : 0;
        int texH = tex != null ? tex.height : 0;

        for (int i = 0; i < metas.Length; i++)
        {
            var m = metas[i];
            var border = CalculateBorderForRect(path, importer, m.rect, texW, texH);
            m.border = border; // Vector4 순서 동일: (Left, Bottom, Right, Top)
            metas[i] = m;
        }

        importer.spritesheet = metas;
        importer.SaveAndReimport();
        return true;
    }
}


    private Vector4 CalculateBorderForSingle(string path, TextureImporter importer)
    {
        // 단일 스프라이트 전체 영역
        Rect r = new Rect(0, 0, importer.spritePixelsPerUnit > 0 ? importer.spritePixelsPerUnit : 100, 100);
        // 실제 텍스쳐 크기 가져오기
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) r = new Rect(0, 0, tex.width, tex.height);
        return CalculateBorderForRect(path, importer, r, tex != null ? tex.width : 0, tex != null ? tex.height : 0);
    }

    private Vector4 CalculateBorderForRect(string path, TextureImporter importer, Rect spriteRect, int texW, int texH)
    {
        int w = Mathf.RoundToInt(spriteRect.width);
        int h = Mathf.RoundToInt(spriteRect.height);

        int l = 0, r = 0, t = 0, b = 0;

        if (mode == BorderMode.Pixels)
        {
            l = Mathf.Clamp(leftPx, 0, w - 1);
            r = Mathf.Clamp(rightPx, 0, w - 1);
            t = Mathf.Clamp(topPx, 0, h - 1);
            b = Mathf.Clamp(bottomPx, 0, h - 1);
        }
        else if (mode == BorderMode.Percent)
        {
            l = Mathf.Clamp(Mathf.RoundToInt(w * leftPercent), 0, w - 1);
            r = Mathf.Clamp(Mathf.RoundToInt(w * rightPercent), 0, w - 1);
            t = Mathf.Clamp(Mathf.RoundToInt(h * topPercent), 0, h - 1);
            b = Mathf.Clamp(Mathf.RoundToInt(h * bottomPercent), 0, h - 1);
        }
        else // AutoDetectAlpha
        {
            // 읽기 가능 토글
            bool prevReadable = importer.isReadable;
            importer.isReadable = true;
            importer.SaveAndReimport();

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                TryAutoDetect(tex, spriteRect, alphaThreshold, marginSafety, out l, out r, out t, out b);
            }

            importer.isReadable = prevReadable;
            importer.SaveAndReimport();

            if (autodetectClampToEven == true)
            {
                if ((l % 2) != 0) l++;
                if ((r % 2) != 0) r++;
                if ((t % 2) != 0) t++;
                if ((b % 2) != 0) b++;
            }
        }

        // 센터 폭/높이 0 방지
        int centerW = w - (l + r);
        int centerH = h - (t + b);
        if (centerW <= 0) { int reduce = 1 - centerW; r = Mathf.Max(0, r - reduce); }
        if (centerH <= 0) { int reduce = 1 - centerH; b = Mathf.Max(0, b - reduce); }

        // Unity의 Vector4(border)는 (left, bottom, right, top) 순서!
        return new Vector4(l, b, r, t);
    }

    private static void TryAutoDetect(Texture2D tex, Rect rect, byte alphaTh, int safety, out int L, out int R, out int T, out int B)
    {
        // rect 영역의 알파를 스캔해 투명 -> 불투명으로 넘어가는 첫 컬럼/로우를 찾는다.
        // 전형적인 라운드 캡슐/버튼에서 외곽 투명, 내부 불투명이라는 가정.
        int x0 = Mathf.RoundToInt(rect.x);
        int y0 = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);

        Func<Color32, bool> Opaque = c => c.a >= alphaTh;

        int left = 0;
        for (int x = 0; x < w; x++)
        {
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                Color32 c = tex.GetPixel(x0 + x, y0 + y);
                if (Opaque(c) == true) { any = true; break; }
            }
            if (any == true) { left = Mathf.Max(0, x - safety); break; }
        }

        int right = 0;
        for (int x = w - 1; x >= 0; x--)
        {
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                Color32 c = tex.GetPixel(x0 + x, y0 + y);
                if (Opaque(c) == true) { any = true; break; }
            }
            if (any == true) { right = Mathf.Max(0, (w - 1 - x) - safety); break; }
        }

        int top = 0;
        for (int y = h - 1; y >= 0; y--)
        {
            bool any = false;
            for (int x = 0; x < w; x++)
            {
                Color32 c = tex.GetPixel(x0 + x, y0 + y);
                if (Opaque(c) == true) { any = true; break; }
            }
            if (any == true) { top = Mathf.Max(0, (h - 1 - y) - safety); break; }
        }

        int bottom = 0;
        for (int y = 0; y < h; y++)
        {
            bool any = false;
            for (int x = 0; x < w; x++)
            {
                Color32 c = tex.GetPixel(x0 + x, y0 + y);
                if (Opaque(c) == true) { any = true; break; }
            }
            if (any == true) { bottom = Mathf.Max(0, y - safety); break; }
        }

        L = left; R = right; T = top; B = bottom;
    }
}

// ------------------------------------------------------------
// 폴더/패턴별 자동 적용을 원하면: 설정 SO + Postprocessor
// 해당 SO를 프로젝트 어디든 생성하고 값만 채우면, 새로운 텍스쳐 임포트 시 자동 적용.

[Serializable]
public class SpriteBorderRule
{
    public string pathContains;     // 예: "/UI/Buttons/"
    public int left = 32, right = 32, top = 32, bottom = 32;
    public bool percent = false;    // true면 0~1 비율로 처리
    public float leftPercent = 0.12f, rightPercent = 0.12f, topPercent = 0.12f, bottomPercent = 0.12f;
    public bool meshFullRect = true;
    public bool setPPU = false;
    public int ppu = 100;
    public TextureImporterCompression compression = TextureImporterCompression.Uncompressed;
}

public class SpriteBorderConfig : ScriptableObject
{
    public List<SpriteBorderRule> rules = new List<SpriteBorderRule>();

    [MenuItem("Tools/UI/Create SpriteBorderConfig Asset")]
    private static void CreateAsset()
    {
        var asset = CreateInstance<SpriteBorderConfig>();
        string path = EditorUtility.SaveFilePanelInProject("Create SpriteBorderConfig", "SpriteBorderConfig", "asset", "Save config asset");
        if (string.IsNullOrEmpty(path) == false)
        {
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}

public class SpriteBorderPostprocessor : AssetPostprocessor
{
    static SpriteBorderConfig cached;
    private static SpriteBorderConfig Config
    {
        get
        {
            if (cached == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SpriteBorderConfig");
                if (guids != null && guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    cached = AssetDatabase.LoadAssetAtPath<SpriteBorderConfig>(path);
                }
            }
            return cached;
        }
    }

    void OnPreprocessTexture()
    {
        if (Config == null) return;

        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        string path = importer.assetPath;

        foreach (var rule in Config.rules)
        {
            if (string.IsNullOrEmpty(rule.pathContains) == false &&
                path.IndexOf(rule.pathContains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // 1) 스프라이트로 강제
                importer.textureType = TextureImporterType.Sprite;

                // 2) MeshType을 TextureImporterSettings 경유로 설정(컴파일 심볼 이슈 회피)
                var tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                tis.spriteMeshType = rule.meshFullRect == true ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                importer.SetTextureSettings(tis);

                // 3) 기타 옵션
                importer.textureCompression = rule.compression;
                if (rule.setPPU == true)
                {
                    importer.spritePixelsPerUnit = rule.ppu;
                }

                // 4) Single인 경우에만 즉시 보더 지정 (퍼센트는 Postprocess에서)
                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    if (rule.percent == false)
                    {
                        // Unity Vector4 순서: (Left, Bottom, Right, Top)
                        importer.spriteBorder = new Vector4(rule.left, rule.bottom, rule.right, rule.top);
                    }
                }
                break;
            }
        }
    }


    void OnPostprocessTexture(Texture2D texture)
    {
        if (Config == null) return;
        var importer = (TextureImporter)assetImporter;
        string path = importer.assetPath;

        foreach (var rule in Config.rules)
        {
            if (string.IsNullOrEmpty(rule.pathContains) == false && path.IndexOf(rule.pathContains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool changed = false;

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    if (rule.percent == true)
                    {
                        int w = texture.width;
                        int h = texture.height;
                        int l = Mathf.RoundToInt(w * rule.leftPercent);
                        int r = Mathf.RoundToInt(w * rule.rightPercent);
                        int t = Mathf.RoundToInt(h * rule.topPercent);
                        int b = Mathf.RoundToInt(h * rule.bottomPercent);
                        importer.spriteBorder = new Vector4(l, b, r, t);
                        changed = true;
                    }
                }
                else
                {
                    // Multiple
                    var metas = importer.spritesheet;
                    if (metas != null && metas.Length > 0)
                    {
                        for (int i = 0; i < metas.Length; i++)
                        {
                            var m = metas[i];
                            int w = Mathf.RoundToInt(m.rect.width);
                            int h = Mathf.RoundToInt(m.rect.height);

                            if (rule.percent == false)
                                m.border = new Vector4(rule.left, rule.bottom, rule.right, rule.top);
                            else
                                m.border = new Vector4(
                                    Mathf.RoundToInt(w * rule.leftPercent),
                                    Mathf.RoundToInt(h * rule.bottomPercent),
                                    Mathf.RoundToInt(w * rule.rightPercent),
                                    Mathf.RoundToInt(h * rule.topPercent)
                                );

                            metas[i] = m;
                        }
                        importer.spritesheet = metas;
                        changed = true;
                    }
                }

                if (changed == true)
                {
                    // 저장
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            importer.SaveAndReimport();
                        }
                        catch { /* ignore */ }
                    };
                }
                break;
            }
        }
    }
}
#endif
