using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NKStudio
{
    [UsedImplicitly]
    internal class BlurShaderGUI : ShaderGUI
    {
        private MaterialProperty _blendAmount;
        private MaterialProperty _vibrancy;
        private MaterialProperty _brightness;
        private MaterialProperty _flatten;

        private void FindProperty(MaterialProperty[] properties)
        {
            _blendAmount = FindProperty("_BlendAmount", properties);
            _vibrancy = FindProperty("_Vibrancy", properties);
            _brightness = FindProperty("_Brightness", properties);
            _flatten = FindProperty("_Flatten", properties);
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperty(properties);

            DrawHeader("Blur UI");

            InspectorBox(10, () =>
            {
                EditorGUI.BeginChangeCheck();
                {
                    materialEditor.ShaderProperty(_blendAmount,
                        new GUIContent("Blur Amount", "UI에 적용되는 흐림 정도를 조정합니다."));
                }
            });

            EditorGUILayout.Space(3);
            
            InspectorBox(10, () =>
            {
                EditorGUI.BeginChangeCheck();
                {
                    materialEditor.ShaderProperty(_vibrancy,
                        new GUIContent("Vibrancy", "흐릿한 UI의 생동감을 조정합니다."));
                    materialEditor.ShaderProperty(_brightness,
                        new GUIContent("Brightness", "흐릿한 UI의 밝기를 조정합니다."));
                    materialEditor.ShaderProperty(_flatten,
                        new GUIContent("Flatten", "흐릿한 UI의 병합 효과를 조정합니다."));
                }
            });
            
            EditorGUILayout.Space(3);
            
            EditorGUILayout.HelpBox("Play Mode가 실행되어야 블러가 적용됩니다.", MessageType.Info);
        }

        private void DrawHeader(string name)
        {
            // Init
            GUIStyle rolloutHeaderStyle = new GUIStyle(GUI.skin.box);
            rolloutHeaderStyle.fontStyle = FontStyle.Bold;
            rolloutHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            // Draw
            GUILayout.Label(name, rolloutHeaderStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true));
        }

        private static void InspectorBox(int aBorder, System.Action inside)
        {
            Rect r = EditorGUILayout.BeginHorizontal();

            GUI.Box(r, GUIContent.none);
            GUILayout.Space(aBorder);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(aBorder);
            inside();
            GUILayout.Space(aBorder);
            EditorGUILayout.EndVertical();
            GUILayout.Space(aBorder);
            EditorGUILayout.EndHorizontal();
        }
    }
}