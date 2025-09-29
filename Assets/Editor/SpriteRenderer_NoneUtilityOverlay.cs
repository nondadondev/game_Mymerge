// File: Assets/Editor/SpriteRenderer_NoneUtilityOverlay.cs
// 내장 SpriteRenderer 인스펙터를 100% 그대로 호출하고,
// 인스펙터 "맨 아래"에 none / Open Sprite Editor 유틸 바만 추가.
//
// 중요: 프로젝트 내 다른 SpriteRenderer용 CustomEditor/PropertyDrawer 제거(또는 주석) 필요.
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(SpriteRenderer), true)]
public class SpriteRenderer_NoneUtilityOverlay : Editor
{
    private Editor builtinEditor;
    private Type builtinType;

    private void OnEnable()
    {
        // 유니티 내장 에디터 타입 획득(버전에 따라 네임스페이스 동일)
        builtinType = Type.GetType("UnityEditor.SpriteRendererEditor, UnityEditor");
        if (builtinType != null)
        {
            builtinEditor = CreateEditor(targets, builtinType);
        }
    }

    private void OnDisable()
    {
        if (builtinEditor != null)
        {
            DestroyImmediate(builtinEditor);
            builtinEditor = null;
        }
    }

    public override void OnInspectorGUI()
    {
        // 1) 기본 인스펙터 그대로 렌더
        if (builtinEditor != null)
        {
            builtinEditor.OnInspectorGUI();
        }
        else
        {
            // 아주 드물게 타입을 못 찾는 경우 안전망
            DrawDefaultInspector();
        }

        // 2) 아래 한 줄 유틸 바: none / Open Sprite Editor
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            // none
            using (new EditorGUI.DisabledScope((targets?.Length ?? 0) == 0))
            {
                if (GUILayout.Button("none", EditorStyles.miniButton, GUILayout.Height(20)))
                {
                    Undo.RecordObjects(targets, "Set Sprite None");
                    foreach (var t in targets)
                    {
                        if (t is SpriteRenderer sr)
                        {
                            sr.sprite = null;
                            EditorUtility.SetDirty(sr);
                        }
                    }
                }
            }
        }
    }

    private Texture2D GetFirstSpriteTexture()
    {
        var sr = targets?.OfType<SpriteRenderer>()?.FirstOrDefault();
        var sp = sr != null ? sr.sprite : null;
        return sp != null ? sp.texture : null;
    }
}
#endif
