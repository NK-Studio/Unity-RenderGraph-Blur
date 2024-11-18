using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace NKStudio
{
    [UsedImplicitly]
    internal class SpriteShaderGUI : ShaderGUI
    {
        private MaterialProperty _blendAmount;

        private void FindProperty(MaterialProperty[] properties)
        {
            _blendAmount = FindProperty("_BlendAmount", properties);

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