using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Noctis.Reparent
{
    /// <summary>
    /// The three ways to reach the picker: keyboard shortcut, hierarchy context menu and the
    /// GameObject inspector header. No logic here beyond resolving the anchor rect.
    /// </summary>
    public static class ReparentEntryPoints
    {
        private const string MenuSetParent = "GameObject/Set Parent...";
        private const string MenuUnparent = "GameObject/Unparent";
        private const string WorldPositionPrefKey = "Noctis.Reparent.WorldPositionStays";

        private static GUIContent _searchIcon;
        private static GUIContent _clearIcon;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawHeader;
        }

        // Ctrl+Shift+P and Ctrl+Alt+P are both taken by Unity (Pause / Profiler), hence H for Hierarchy.
        [Shortcut("Reparent/Set Parent", KeyCode.H, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void SetParentShortcut()
        {
            ReparentWindow.Open(AnchorAtMouse());
        }

        [MenuItem(MenuSetParent, false, 0)]
        private static void SetParentCommand(MenuCommand command)
        {
            // GameObject menu items fire once per selected object; let a single invocation through.
            if (command.context != null && command.context != Selection.activeObject)
            {
                return;
            }

            ReparentWindow.Open(AnchorAtMouse());
        }

        [MenuItem(MenuSetParent, true)]
        private static bool ValidateSetParent()
        {
            return Selection.transforms.Length > 0;
        }

        [MenuItem(MenuUnparent, false, 1)]
        private static void UnparentCommand(MenuCommand command)
        {
            if (command.context != null && command.context != Selection.activeObject)
            {
                return;
            }

            Unparent();
        }

        [MenuItem(MenuUnparent, true)]
        private static bool ValidateUnparent()
        {
            foreach (Transform current in Selection.transforms)
            {
                if (current.parent != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Unparent()
        {
            if (!ReparentOperation.TryGetContext(out List<Transform> roots, out _, out string error))
            {
                EditorUtility.DisplayDialog("Unparent", error, "OK");
                return;
            }

            ReparentOperation.UnparentToRoot(roots, EditorPrefs.GetBool(WorldPositionPrefKey, true));
        }

        private static void DrawHeader(UnityEditor.Editor editor)
        {
            if (editor.target is not GameObject target || EditorUtility.IsPersistent(target))
            {
                return;
            }

            bool multiple = editor.targets.Length > 1;
            Transform parent = target.transform.parent;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Parent");

            using (new EditorGUI.DisabledScope(true))
            {
                if (multiple)
                {
                    EditorGUILayout.LabelField("—", EditorStyles.textField);
                }
                else
                {
                    EditorGUILayout.ObjectField(GUIContent.none, parent, typeof(Transform), true);
                }
            }

            if (GUILayout.Button(SearchIcon(), EditorStyles.miniButton, GUILayout.Width(26f)))
            {
                Rect anchor = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                EditorApplication.delayCall += () => ReparentWindow.Open(anchor);
            }

            using (new EditorGUI.DisabledScope(!multiple && parent == null))
            {
                if (GUILayout.Button(ClearIcon(), EditorStyles.miniButton, GUILayout.Width(26f)))
                {
                    EditorApplication.delayCall += Unparent;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static GUIContent SearchIcon()
        {
            return _searchIcon ??= IconOrText("Search Icon", "F", "Pick a parent");
        }

        private static GUIContent ClearIcon()
        {
            return _clearIcon ??= IconOrText("CrossIcon", "X", "Move to the scene root");
        }

        private static GUIContent IconOrText(string iconName, string fallback, string tooltip)
        {
            // FindTexture returns null quietly; IconContent logs an error for names the skin lacks.
            Texture2D image = EditorGUIUtility.FindTexture(iconName);
            return image != null ? new GUIContent(image, tooltip) : new GUIContent(fallback, tooltip);
        }

        private static Rect AnchorAtMouse()
        {
            if (Event.current != null)
            {
                Vector2 point = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                return new Rect(point, Vector2.zero);
            }

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(main.center, Vector2.zero);
        }
    }
}
