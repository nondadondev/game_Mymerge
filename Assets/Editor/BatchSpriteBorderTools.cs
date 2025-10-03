// File: Assets/Editor/BatchSpriteBorderTools.cs
// Unity 2020+ (Editor only).
// 변경점 요약:
// 1) 전역 가드(SpriteBorderApplyGuard.InManualApply) 추가 → 윈도에서 수동 적용 중엔 Postprocessor가 개입하지 않음.
// 2) 픽셀 모드 절대값 보장(원하면 ensurePositiveCenter 옵션으로만 최소 센터 폭 보정).
// 3) 싱글=importer.spriteBorder, 멀티=metas[i].border 정확 적용(L,B,R,T).
// 4) 불필요한 재임포트 최소화, 적용 후 실제 반영치 로그로 검증.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ──────────────────────────────────────────────────────────────────────────
// 전역 가드: 수동 일괄 적용 중에는 Postprocessor가 스킵하도록 신호
// ──────────────────────────────────────────────────────────────────────────
internal static class SpriteBorderApplyGuard
{
    public static bool InManualApply = false;
}

public class BatchSpriteBorderSetterWindow : EditorWindow
{
    private enum BorderMode { Pixels, Percent, AutoDetectAlpha }

    private BorderMode mode = BorderMode.Pixels;

    // 입력값(L/R/T/B) - 사람이 보기 좋은 순서
    private int leftPx = 32, rightPx = 32, topPx = 32, bottomPx = 32;
    private float leftPercent = 0.12f, rightPercent = 0.12f, topPercent = 0.12f, bottomPercent = 0.12f;

    // 공통 옵션
    private bool forceSprite2DUI = true;
    private bool setMeshFullRect = true;
    private bool setPPU = false;
    private int pixelsPerUnit = 100;
    private bool setCompression = false;
    private TextureImporterCompression compression = TextureImporterCompression.Uncompressed;

    // 절대값 보장 옵션
    private bool ensurePositiveCenter = false; // 기본: 꺼둠(네가 넣은 픽셀을 최대한 그대로)

    // 오토디텍트 옵션
    private bool autodetectClampToEven = true;
    private byte alphaThreshold = 5; // 0~255
    private int marginSafety = 1;

    private DefaultAsset targetFolder;

    [MenuItem("Tools/UI/Batch Sprite Border Setter")]
    private static void Open()
    {
        var win = GetWindow<BatchSpriteBorderSetterWindow>();
        win.titleContent = new GUIContent("Sprite Border Setter");
        win.minSize = new Vector2(440, 520);
        win.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch Sprite Border Setter", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        // 모드
        EditorGUILayout.LabelField("Border Mode", EditorStyles.boldLabel);
        mode = (BorderMode)EditorGUILayout.EnumPopup("Mode", mode);

        if (mode == BorderMode.Pixels)
        {
            EditorGUILayout.LabelField("Pixels (absolute)", EditorStyles.miniBoldLabel);
            leftPx   = EditorGUILayout.IntField("Left (px)", leftPx);
            rightPx  = EditorGUILayout.IntField("Right (px)", rightPx);
            topPx    = EditorGUILayout.IntField("Top (px)", topPx);
            bottomPx = EditorGUILayout.IntField("Bottom (px)", bottomPx);
        }
        else if (mode == BorderMode.Percent)
        {
            EditorGUILayout.LabelField("Percent of sprite rect (0~1)", EditorStyles.miniBoldLabel);
            leftPercent   = Mathf.Clamp01(EditorGUILayout.FloatField("Left (%)", leftPercent));
            rightPercent  = Mathf.Clamp01(EditorGUILayout.FloatField("Right (%)", rightPercent));
            topPercent    = Mathf.Clamp01(EditorGUILayout.FloatField("Top (%)", topPercent));
            bottomPercent = Mathf.Clamp01(EditorGUILayout.FloatField("Bottom (%)", bottomPercent));
        }
        else
        {
            EditorGUILayout.LabelField("Auto Detect by Alpha", EditorStyles.miniBoldLabel);
            alphaThreshold = (byte)Mathf.Clamp(EditorGUILayout.IntSlider("Alpha Threshold", alphaThreshold, 0, 255), 0, 255);
            marginSafety = EditorGUILayout.IntField("Inner Safety Margin (px)", marginSafety);
            autodetectClampToEven = EditorGUILayout.Toggle("Clamp to Even Pixels", autodetectClampToEven);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Import/Common Options", EditorStyles.boldLabel);
        forceSprite2DUI  = EditorGUILayout.Toggle("Force TextureType: Sprite(2D/UI)", forceSprite2DUI);
        setMeshFullRect  = EditorGUILayout.Toggle("Mesh Type: Full Rect", setMeshFullRect);
        setPPU           = EditorGUILayout.Toggle("Set Pixels Per Unit", setPPU);
        if (setPPU == true) pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);
        setCompression   = EditorGUILayout.Toggle("Set Compression", setCompression);
        if (setCompression == true) compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compression);

        EditorGUILayout.Space(6);
        ensurePositiveCenter = EditorGUILayout.Toggle(new GUIContent("Ensure center > 0 (balanced clamp)"), ensurePositiveCenter);

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply To Selection", GUILayout.Height(30)))
            {
                var paths = Selection.assetGUIDs
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsSupportedTexturePath)
                    .ToArray();
                ApplyToPaths(paths);
            }
        }

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
        using (new EditorGUI.DisabledScope(targetFolder == null))
        {
            if (GUILayout.Button("Apply To Folder (recursive)", GUILayout.Height(26)))
            {
                string folderPath = AssetDatabase.GetAssetPath(targetFolder);
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Where(IsSupportedTexturePath).ToArray();
                ApplyToPaths(paths);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "픽셀 모드 = 절대값. 퍼센트/규칙 Postprocessor가 뒤에서 덮어쓰지 않도록, 이 창이 작업 중일 때는 Postprocessor 가드로 차단됩니다.\n" +
            "권장: 9-slice는 Full Rect 메시, 아틀라스 Tight/Rotation 끄기.", MessageType.Info);
    }

    private bool IsSupportedTexturePath(string p)
    {
        return p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".psd", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyToPaths(string[] texturePaths)
    {
        int ok = 0, fail = 0;

        SpriteBorderApplyGuard.InManualApply = true; // ★ 가드 켜기
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var path in texturePaths)
            {
                try
                {
                    if (ProcessOneTexture(path) == true) ok++; else ok++;
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
            SpriteBorderApplyGuard.InManualApply = false; // ★ 가드 끄기
        }

        EditorUtility.DisplayDialog("Done", $"Processed: {texturePaths.Length}\nSuccess: {ok}\nFail: {fail}", "OK");
    }

    private bool ProcessOneTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool changed = false;

        // 1) Sprite 강제(선택)
        if (forceSprite2DUI == true && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        // 2) 압축/PPU
        if (setCompression == true && importer.textureCompression != compression)
        {
            importer.textureCompression = compression;
            changed = true;
        }
        if (setPPU == true && importer.spritePixelsPerUnit != pixelsPerUnit)
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            changed = true;
        }

        // 3) MeshType = FullRect
        if (setMeshFullRect == true)
        {
            var tis = new TextureImporterSettings();
            importer.ReadTextureSettings(tis);
            if (tis.spriteMeshType != SpriteMeshType.FullRect)
            {
                tis.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(tis);
                changed = true;
            }
        }

        // 4) Border 적용
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int texW = tex != null ? tex.width : 0;
        int texH = tex != null ? tex.height : 0;

        if (importer.spriteImportMode == SpriteImportMode.Single)
        {
            Rect r = new Rect(0, 0, texW, texH);
            Vector4 border = CalculateBorderForRect(path, importer, r);
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border; // (L,B,R,T)
                changed = true;
            }
        }
        else
        {
            var metas = importer.spritesheet;
            if (metas != null && metas.Length > 0)
            {
                bool metaChanged = false;
                for (int i = 0; i < metas.Length; i++)
                {
                    var m = metas[i];
                    Vector4 newBorder = CalculateBorderForRect(path, importer, m.rect);
                    if (m.border != newBorder)
                    {
                        m.border = newBorder; // (L,B,R,T)
                        metas[i] = m;
                        metaChanged = true;
                    }
                }
                if (metaChanged == true)
                {
                    importer.spritesheet = metas;
                    changed = true;
                }
            }
        }

        if (changed == true)
        {
            importer.SaveAndReimport();

            // 검증 로그
            var re = AssetImporter.GetAtPath(path) as TextureImporter;
            if (re != null)
            {
                if (re.spriteImportMode == SpriteImportMode.Single)
                {
                    Vector4 b = re.spriteBorder;
                    Debug.Log($"[Applied:Single] {path} -> L:{b.x}, B:{b.y}, R:{b.z}, T:{b.w}");
                }
                else
                {
                    foreach (var m in re.spritesheet)
                    {
                        var b = m.border;
                        Debug.Log($"[Applied:Multi] {path}::{m.name} -> L:{b.x}, B:{b.y}, R:{b.z}, T:{b.w}");
                    }
                }
            }
        }

        return changed;
    }

    // 내부 Vector4는 (Left, Bottom, Right, Top)
    private Vector4 CalculateBorderForRect(string path, TextureImporter importer, Rect spriteRect)
    {
        int w = Mathf.RoundToInt(spriteRect.width);
        int h = Mathf.RoundToInt(spriteRect.height);

        int L, R, T, B;

        if (mode == BorderMode.Pixels)
        {
            L = Mathf.Clamp(leftPx,   0, w);
            R = Mathf.Clamp(rightPx,  0, w);
            T = Mathf.Clamp(topPx,    0, h);
            B = Mathf.Clamp(bottomPx, 0, h);
        }
        else if (mode == BorderMode.Percent)
        {
            L = Mathf.Clamp(Mathf.RoundToInt(w * leftPercent),   0, w);
            R = Mathf.Clamp(Mathf.RoundToInt(w * rightPercent),  0, w);
            T = Mathf.Clamp(Mathf.RoundToInt(h * topPercent),    0, h);
            B = Mathf.Clamp(Mathf.RoundToInt(h * bottomPercent), 0, h);
        }
        else // AutoDetectAlpha
        {
            bool prevReadable = importer.isReadable;
            importer.isReadable = true;
            importer.SaveAndReimport();

            int x0 = Mathf.RoundToInt(spriteRect.x);
            int y0 = Mathf.RoundToInt(spriteRect.y);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TryAutoDetect(tex, x0, y0, w, h, alphaThreshold, marginSafety, out L, out R, out T, out B);

            if (autodetectClampToEven == true)
            {
                if ((L & 1) == 1) L++;
                if ((R & 1) == 1) R++;
                if ((T & 1) == 1) T++;
                if ((B & 1) == 1) B++;
            }

            importer.isReadable = prevReadable;
            importer.SaveAndReimport();
        }

        if (ensurePositiveCenter == true)
        {
            // centerW/H 최소 1px 보장. (넘치면 균형 축소)
            BalanceClamp(ref L, ref R, w);
            BalanceClamp(ref B, ref T, h);
        }
        else
        {
            // 절대값 그대로 쓰되, Unity 제약 때문에 너무 큰 값은 잘라냄
            L = Mathf.Clamp(L, 0, w - 1);
            R = Mathf.Clamp(R, 0, w - 1);
            T = Mathf.Clamp(T, 0, h - 1);
            B = Mathf.Clamp(B, 0, h - 1);
            // L+R 또는 T+B가 size-1을 넘어가면 Unity 내부에서 어차피 보정됨
        }

        return new Vector4(L, B, R, T);
    }

    private void BalanceClamp(ref int a, ref int b, int size)
    {
        int sum = a + b;
        if (sum <= size - 1) return;

        int over = sum - (size - 1);
        if (sum == 0) return;

        float ra = a / (float)sum;
        int reduceA = Mathf.RoundToInt(over * ra);
        int reduceB = over - reduceA;

        a = Mathf.Max(0, a - reduceA);
        b = Mathf.Max(0, b - reduceB);

        while (a + b > size - 1)
        {
            if (a >= b && a > 0) a--;
            else if (b > 0) b--;
            else break;
        }
    }

    private static void TryAutoDetect(Texture2D tex, int x0, int y0, int w, int h, byte alphaTh, int safety, out int L, out int R, out int T, out int B)
    {
        if (tex == null) { L = R = T = B = 0; return; }

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

// ──────────────────────────────────────────────────────────────────────────
// 규칙 SO + Postprocessor
// ──────────────────────────────────────────────────────────────────────────

[Serializable]
public class SpriteBorderRule
{
    public string pathContains;
    public int left = 32, right = 32, top = 32, bottom = 32;
    public bool percent = false;
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

    // ★ 수동 적용 중이면 완전히 패스
    private bool ShouldSkip() => SpriteBorderApplyGuard.InManualApply == true || Config == null;

    void OnPreprocessTexture()
    {
        if (ShouldSkip()) return;

        var importer = assetImporter as TextureImporter;
        if (importer == null) return;

        string path = importer.assetPath;

        foreach (var rule in Config.rules)
        {
            if (string.IsNullOrEmpty(rule.pathContains) == false &&
                path.IndexOf(rule.pathContains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                importer.textureType = TextureImporterType.Sprite;

                var tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                tis.spriteMeshType = rule.meshFullRect == true ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                importer.SetTextureSettings(tis);

                importer.textureCompression = rule.compression;
                if (rule.setPPU == true) importer.spritePixelsPerUnit = rule.ppu;

                if (importer.spriteImportMode == SpriteImportMode.Single && rule.percent == false)
                {
                    importer.spriteBorder = new Vector4(rule.left, rule.bottom, rule.right, rule.top);
                }
                break;
            }
        }
    }

    void OnPostprocessTexture(Texture2D texture)
    {
        if (ShouldSkip()) return;

        var importer = (TextureImporter)assetImporter;
        string path = importer.assetPath;

        foreach (var rule in Config.rules)
        {
            if (string.IsNullOrEmpty(rule.pathContains) == false &&
                path.IndexOf(rule.pathContains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool changed = false;

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    if (rule.percent == true)
                    {
                        int w = texture.width;
                        int h = texture.height;
                        int L = Mathf.RoundToInt(w * rule.leftPercent);
                        int R = Mathf.RoundToInt(w * rule.rightPercent);
                        int T = Mathf.RoundToInt(h * rule.topPercent);
                        int B = Mathf.RoundToInt(h * rule.bottomPercent);

                        // 최소 센터(선호시 균형 축소). 퍼센트 규칙이라도 과도합 방지 차원.
                        BalanceClampStatic(ref L, ref R, w);
                        BalanceClampStatic(ref B, ref T, h);

                        importer.spriteBorder = new Vector4(L, B, R, T);
                        changed = true;
                    }
                }
                else
                {
                    var metas = importer.spritesheet;
                    if (metas != null && metas.Length > 0)
                    {
                        for (int i = 0; i < metas.Length; i++)
                        {
                            var m = metas[i];
                            int w = Mathf.RoundToInt(m.rect.width);
                            int h = Mathf.RoundToInt(m.rect.height);

                            int L, R, T, B;
                            if (rule.percent == false)
                            {
                                L = Mathf.Clamp(rule.left, 0, w);
                                R = Mathf.Clamp(rule.right, 0, w);
                                T = Mathf.Clamp(rule.top, 0, h);
                                B = Mathf.Clamp(rule.bottom, 0, h);
                            }
                            else
                            {
                                L = Mathf.Clamp(Mathf.RoundToInt(w * rule.leftPercent),   0, w);
                                R = Mathf.Clamp(Mathf.RoundToInt(w * rule.rightPercent),  0, w);
                                T = Mathf.Clamp(Mathf.RoundToInt(h * rule.topPercent),    0, h);
                                B = Mathf.Clamp(Mathf.RoundToInt(h * rule.bottomPercent), 0, h);
                            }

                            BalanceClampStatic(ref L, ref R, w);
                            BalanceClampStatic(ref B, ref T, h);

                            m.border = new Vector4(L, B, R, T);
                            metas[i] = m;
                        }
                        importer.spritesheet = metas;
                        changed = true;
                    }
                }

                if (changed == true)
                {
                    try { importer.SaveAndReimport(); } catch { /* ignore */ }
                }
                break;
            }
        }
    }

    private static void BalanceClampStatic(ref int a, ref int b, int size)
    {
        int sum = a + b;
        if (sum <= size - 1) return;

        int over = sum - (size - 1);
        if (sum == 0) return;

        float ra = a / (float)sum;
        int reduceA = Mathf.RoundToInt(over * ra);
        int reduceB = over - reduceA;

        a = Mathf.Max(0, a - reduceA);
        b = Mathf.Max(0, b - reduceB);

        while (a + b > size - 1)
        {
            if (a >= b && a > 0) a--;
            else if (b > 0) b--;
            else break;
        }
    }
}
#endif
