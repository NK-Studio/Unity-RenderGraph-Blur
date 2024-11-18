#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Toolbars;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class SceneSwitchFirstSceneGUI
    {
        internal static void OnGUI(VisualElement element)
        {
            ToolbarButton button = new ToolbarButton
            {
                name = "InitSceneButton",
                text = "Start None",
                style =
                {
                    // 버튼 스타일 설정
                    marginRight = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
                
            };

            // 툴팁 설정
            string tooltip = Application.systemLanguage == SystemLanguage.Korean
                ? "최초 세팅 씬부터 게임을 진입합니다."
                : "Start from the first scene.";
            
            button.tooltip = tooltip;
            
            // 버튼 활성화 설정
            button.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 1초마다 실행하여 Update처럼 사용
            button.schedule.Execute(() => button.text = $"Start '{FirstSceneName}'").Every(1000);
            
            // 플레이 모드가 변경 될 때 버튼 활성화를 새로고침 합니다.
            EditorApplication.playModeStateChanged += _ => button.SetEnabled(ToolbarUtility.IsNotPlaying);
            
            // 버튼 클릭 이벤트 설정
            button.clicked += () => SceneHelper.StartScene(TargetScenePath, true);
            
            // 등록
            element.Add(button);
        }

        /// <summary>
        /// 빌드 세팅스에 등록된 모든 씬들의 경로를 배열로 반환합니다.
        /// </summary>
        /// <returns>빌드 세팅스에 등록된 씬들의 경로 배열입니다. 등록된 씬이 없으면 null을 반환합니다.</returns>
        private static string[] GetAllScenesInBuildSettings()
        {
            List<string> list = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    list.Add(scene.path);

            return list.Count == 0 ? null : list.ToArray();
        }

        /// <summary>
        /// 빌드 세팅스에 등록된 씬 중 첫번째 씬의 이름을 반환합니다.
        /// </summary>
        /// <returns>빌드 세팅스에 등록된 씬 중 첫번째 씬의 이름을 반환합니다. 등록된 씬이 없으면 "None" 문자열을 반환합니다.</returns>
        private static string FirstSceneName
        {
            get
            {
                // 첫번째 씬 이름을 가져옵니다.
                string[] allLevelName = GetAllScenesInBuildSettings();
                return allLevelName == null ? "None" : Path.GetFileNameWithoutExtension(allLevelName[0]);
            }
        }

        /// <summary>
        /// 타겟 씬 경로를 반환합니다.
        /// </summary>
        private static string TargetScenePath
        {
            get
            {
                // 첫번째 씬 이름을 가져옵니다.
                string[] allLevelName = GetAllScenesInBuildSettings();

                if (allLevelName == null)
                    return "";

                return allLevelName[0];
            }
        }
    }

    internal static class SceneSwitchListMoveGUI
    {
        public static void OnGUI(VisualElement element)
        {
            var dropdown = new EditorToolbarDropdown
            {
                name = "MoveSceneDropdown",
                text = "씬 선택 이동",
                style =
                {
                    // 버튼 스타일 설정
                    marginLeft = 3
                }
            };

            // 버튼 활성화 설정
            dropdown.SetEnabled(ToolbarUtility.IsNotPlaying);

            // 드롭다운 클릭 이벤트 설정
            dropdown.clicked += () => ShowMenuItem(dropdown);

            // 플레이 모드가 변경 될 때 버튼 활성화를 새로고침 합니다.
            EditorApplication.playModeStateChanged += _ => dropdown.SetEnabled(ToolbarUtility.IsNotPlaying);

            // 등록
            element.Add(dropdown);
        }

        /// <summary>
        /// 드롭다운 메뉴를 표시합니다.
        /// </summary>
        /// <param name="dropdown">드롭다운 메뉴를 표시할 요소입니다.</param>
        private static void ShowMenuItem(VisualElement dropdown)
        {
            var menu = new GenericMenu();
            Dictionary<string, string> allScenes = SceneHelper.FindAllScenes();

            foreach ((string scenePath, string sceneAllPath) in allScenes)
                menu.AddItem(new GUIContent($"{scenePath}"), false, OnClickDropdown, sceneAllPath);

            menu.DropDown(dropdown.worldBound);
        }

        /// <summary>
        /// 드롭다운 메뉴 항목을 클릭했을 때 호출되는 메서드입니다.
        /// </summary>
        /// <param name="parameter">클릭된 항목의 경로입니다.</param>
        private static void OnClickDropdown(object parameter)
        {
            SceneHelper.StartScene((string)parameter, false);
        }
    }
}
#endif