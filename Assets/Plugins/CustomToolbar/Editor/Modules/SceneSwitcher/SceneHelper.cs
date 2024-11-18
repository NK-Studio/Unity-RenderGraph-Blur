using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NKStudio
{
    internal static class SceneHelper
    {
        private static string _sceneToOpen;
        private static bool _isAutoPlay;

        private const string TargetSceneFolderPath = "Assets/Scenes"; // 여기롤 변경해주세요.

        /// <summary>
        /// 지정된 씬을 시작하고 선택적으로 플레이 모드를 시작합니다.
        /// </summary>
        /// <param name="sceneName">열려는 씬의 이름입니다.</param>
        /// <param name="isPlay">true이면 씬이 플레이 모드에서 시작됩니다.</param>
        public static void StartScene(string sceneName, bool isPlay)
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            _sceneToOpen = sceneName;
            _isAutoPlay = isPlay;

            EditorApplication.update += OnUpdate;
        }

        /// <summary>
        /// 모든 씬을 찾습니다.
        /// </summary>
        /// <returns>씬 경로와 씬 이름의 딕셔너리를 반환합니다.</returns>
        public static Dictionary<string, string> FindAllScenes()
        {
            // ScenePath-SceneName
            Dictionary<string, string> result = new();

            string[] guids = AssetDatabase.FindAssets("t:scene ", new[] { TargetSceneFolderPath });

            foreach (string guid in guids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                string filterSceneName = scenePath.Replace(".unity", "");

                try
                {
                    string resultPath = filterSceneName.Replace($"{TargetSceneFolderPath}/", "");
                    string resultSceneName = scenePath;

                    result.Add(resultPath, resultSceneName);
                }
                catch (Exception)
                {
                }
            }

            return result;
        }

        /// <summary>
        /// 에디터 업데이트 콜백입니다.
        /// 씬을 열고 자동 재생 모드를 설정합니다.
        /// </summary>
        private static void OnUpdate()
        {
            if (_sceneToOpen == null ||
                EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(_sceneToOpen);
                EditorApplication.isPlaying = _isAutoPlay;
            }

            _isAutoPlay = false;
            _sceneToOpen = null;
        }
    }
}