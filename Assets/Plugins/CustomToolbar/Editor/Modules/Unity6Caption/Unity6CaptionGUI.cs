using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class Unity6CaptionGUI
    {
        internal static void OnGUI(VisualElement element)
        {
            var caption = element.Q<VisualElement>("ToolbarProductCaption");

            if (caption != null)
                element.Remove(caption);
        }
    }
}