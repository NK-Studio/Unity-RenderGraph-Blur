using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace NKStudio
{
    [InitializeOnLoad]
    public static class HierarchyWindowAdapter
    {
        private const string EditorWindowType = "UnityEditor.SceneHierarchyWindow";
        private const double EditorWindowsCacheTtl = 2;

        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;
        
        private static readonly FieldInfo SceneHierarchyField;
        private static readonly FieldInfo TreeViewField;
        private static readonly PropertyInfo TreeViewDataProperty;
        private static readonly MethodInfo TreeViewItemsMethod;
        
        private static double _nextWindowsUpdate;
        private static EditorWindow[] _windowsCache;
        
        static HierarchyWindowAdapter()
        {
            var assembly = Assembly.GetAssembly(typeof(EditorWindow));

            var hierarchyWindowType = assembly.GetType("UnityEditor.SceneHierarchyWindow");
            SceneHierarchyField = hierarchyWindowType.GetField("m_SceneHierarchy", InstancePrivate);

            var sceneHierarchyType = assembly.GetType("UnityEditor.SceneHierarchy");
            TreeViewField = sceneHierarchyType.GetField("m_TreeView", InstancePrivate);

            var treeViewType = assembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");
            TreeViewDataProperty = treeViewType.GetProperty("data", InstancePublic);

            var treeViewDataType = assembly.GetType("UnityEditor.GameObjectTreeViewDataSource");
            TreeViewItemsMethod = treeViewDataType.GetMethod("GetRows", InstancePublic);
        }

        /// <summary>
        /// Retrieves all hierarchy windows in the Unity editor.
        /// </summary>
        /// <param name="forceUpdate">Optional. If true, forces an update to the windows cache. Default is false.</param>
        /// <returns>An IEnumerable collection of EditorWindow objects representing all opened hierarchy windows.</returns>
        private static IEnumerable<EditorWindow> GetAllHierarchyWindows(bool forceUpdate = false)
        {
            if (forceUpdate || _nextWindowsUpdate < EditorApplication.timeSinceStartup)
            {
                _nextWindowsUpdate = EditorApplication.timeSinceStartup + EditorWindowsCacheTtl;
                _windowsCache = UFolderUtility.GetAllWindowsByType(EditorWindowType).ToArray();
            }

            return _windowsCache;
        }

        /// <summary>
        /// GetTreeViewItems method retrieves the TreeViewItems from the specified EditorWindow.
        /// </summary>
        /// <param name="window">The EditorWindow from which to retrieve TreeViewItems.</param>
        /// <returns>An IEnumerable collection of TreeViewItem objects.</returns>
        private static IEnumerable<TreeViewItem> GetTreeViewItems(EditorWindow window)
        {
            object obj1 = SceneHierarchyField.GetValue(window);
            object obj2 = TreeViewField.GetValue(obj1);
            object obj3 = TreeViewDataProperty.GetValue(obj2, null);
            return (IEnumerable<TreeViewItem>)TreeViewItemsMethod.Invoke(obj3, null);
        }

        /// <summary>
        /// 인스턴스ID를 이용하여 아이콘을 적용합니다.
        /// </summary>
        /// <param name="instanceId">아이콘을 적용할 게임 오브젝트 InstanceID</param>
        /// <param name="icon">지정할 아이콘 텍스쳐</param>
        public static void ApplyIconByInstanceId(int instanceId, Texture2D icon)
        {
            var hierarchyWindows = GetAllHierarchyWindows();

            foreach (var window in hierarchyWindows)
            {
                var treeViewItems = GetTreeViewItems(window);
                var treeViewItem = treeViewItems.FirstOrDefault(item => item.id == instanceId);
                if (treeViewItem != null) treeViewItem.icon = icon;
            }
        }

        /// <summary>
        /// 첫번째 HierarchyWindow를 가져옵니다.
        /// </summary>
        /// <returns></returns>
        internal static object GetFirstHierarchy()
        {
            foreach (EditorWindow allHierarchyWindow in GetAllHierarchyWindows())
            {
                object obj1 = SceneHierarchyField.GetValue(allHierarchyWindow);
                if (obj1 != null)
                    return obj1;
            }

            return null;
        }
    }
}
