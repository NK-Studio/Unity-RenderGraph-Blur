#if USE_LOCALIZATION
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class LocalizationGUI
    {
        internal static void OnGUI(VisualElement element)
        {
            var dropdown = new EditorToolbarDropdown();
            dropdown.name = "LocalizationDropdown";

            // 텍스트 설정
            dropdown.text = $"Localization : {GetLocales[CurrentLocalIndex]}";

            // 아이콘 설정
            dropdown.icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Packages/com.unity.localization/Editor/Icons/Locale/{ToolbarUtility.DarkModePrefix}Locale.png");

            // 로컬 변경 이벤트 설정
            LocalizationSettings.SelectedLocaleChanged += locale => dropdown.text = $"Localization : {locale}";

            // 드롭다운 클릭 이벤트 설정
            dropdown.clicked += () => ShowMenuItem(dropdown);

            element.Add(dropdown);
        }

        /// <summary>
        /// 드롭다운 메뉴를 표시합니다.
        /// </summary>
        /// <param name="dropdown">드롭다운 요소</param>
        private static void ShowMenuItem(EditorToolbarDropdown dropdown)
        {
            var menu = new GenericMenu();
            var locales = GetLocales;

            for (int i = 0; i < locales.Count; i++)
            {
                int index = i;
                menu.AddItem(new GUIContent(locales[index]), false,
                    () => LocalizationSettings.SelectedLocale = LocalizationEditorSettings.GetLocales()[index]);
            }

            menu.DropDown(dropdown.worldBound);
        }

        private static List<string> GetLocales
        {
            get
            {
                if (LocalizationSettings.HasSettings)
                {
                    // 로케일 목록을 가져옴
                    var locals = LocalizationEditorSettings.GetLocales();

                    if (locals.Count > 0)
                    {
                        string[] localNames = new string[locals.Count];

                        for (int i = 0; i < locals.Count; i++)
                            localNames[i] = locals[i].ToString();

                        return localNames.ToList();
                    }
                }

                return new List<string> { "None" };
            }
        }

        /// <summary>
        /// 현재 선택된 로컬 인덱스를 반환합니다.
        /// </summary>
        /// <returns>현재 로컬 인덱스</returns>
        private static int CurrentLocalIndex
        {
            get
            {
                Locale currentLocal = LocalizationSettings.SelectedLocale;
                ReadOnlyCollection<Locale> locals = LocalizationEditorSettings.GetLocales();

                for (int i = 0; i < locals.Count; i++)
                {
                    if (currentLocal == locals[i])
                        return i;
                }

                return 0;
            }
        }
    }
}
#endif