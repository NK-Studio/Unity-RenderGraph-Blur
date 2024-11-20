using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace NKStudio
{
    [CustomEditor(typeof(UIBlur))]
    public class UIBlurEditor : Editor
    {
        private SerializedProperty _blendAmount;
        private SerializedProperty _vibrancy;
        private SerializedProperty _brightness;
        private SerializedProperty _flatten;

        private VisualElement _root;
        private StyleSheet _styleSheet;

        private UIBlur _uiBlur;

        private void OnEnable()
        {
            _uiBlur = target as UIBlur;

            if (_uiBlur != null)
                ChangeBlurMaterial(_uiBlur.gameObject);
        }

        private void FindProperty()
        {
            _blendAmount = serializedObject.FindProperty("blendAmount");
            _vibrancy = serializedObject.FindProperty("vibrancy");
            _brightness = serializedObject.FindProperty("brightness");
            _flatten = serializedObject.FindProperty("flatten");
        }

        private void InitElement()
        {
            _root = new VisualElement();

            var stylePath = AssetDatabase.GUIDToAssetPath("84ca34d40840e4760840dae8aebca137");
            _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);

            if (!_styleSheet)
                _styleSheet =
                    AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Plugins/Blur UI/Scripts/Editor/USS/UIBlur.uss");

            _root.styleSheets.Add(_styleSheet);

            var title = new Label();
            title.text = "Blur UI";
            title.AddToClassList("TitleStyle");
            _root.Add(title);

            var mainBox = new GroupBox();
            mainBox.AddToClassList("GroupBoxStyle");
            _root.Add(mainBox);

            var blendAmount = new PropertyField();
            blendAmount.BindProperty(_blendAmount);
            mainBox.Add(blendAmount);

            var optionBox = new GroupBox();
            optionBox.AddToClassList("GroupBoxStyle");
            _root.Add(optionBox);

            var vibrancy = new PropertyField();
            vibrancy.BindProperty(_vibrancy);
            optionBox.Add(vibrancy);

            var brightness = new PropertyField();
            brightness.BindProperty(_brightness);
            optionBox.Add(brightness);

            var flatten = new PropertyField();
            flatten.BindProperty(_flatten);
            optionBox.Add(flatten);

            title.RegisterCallback<MouseDownEvent>(evt => OpenBehaviour(_uiBlur));
        }

        public override VisualElement CreateInspectorGUI()
        {
            FindProperty();
            InitElement();

            return _root;
        }
        
        /// <summary>
        /// 블러 머티리얼이 아니라면 블러 머티리얼로 변경합니다.
        /// </summary>
        /// <param name="targetObject">대상 오브젝트</param>
        private void ChangeBlurMaterial(GameObject targetObject)
        {
            if (targetObject.TryGetComponent(out Image image))
            {
                bool isBlurMaterial = image.material.shader.name == "UI/SG_BlurUI";
                if (!isBlurMaterial)
                {
                    var targetPath = "Assets/Plugins/Blur UI/Art/Materials/BlurUI.mat";
                    var blurMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetPath);

                    Debug.Assert(blurMaterial != null, $"{targetPath}경로에 블러 머티리얼이 존재하지 않습니다.");

                    image.material = blurMaterial;

                }
            }
        }
        
        private static void OpenBehaviour(MonoBehaviour targetBehaviour)
        {
            var scriptAsset = MonoScript.FromMonoBehaviour(targetBehaviour);
            var path = AssetDatabase.GetAssetPath(scriptAsset);

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            AssetDatabase.OpenAsset(textAsset);
        }
    }
}