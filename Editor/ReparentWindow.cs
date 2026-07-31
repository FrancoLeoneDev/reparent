using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Noctis.Reparent
{
    /// <summary>
    /// The searchable parent picker. Pure UI: it knows nothing about scenes or undo and delegates
    /// every mutation to <see cref="ReparentOperation"/>.
    /// </summary>
    public sealed class ReparentWindow : EditorWindow
    {
        private const string WorldPositionPrefKey = "Noctis.Reparent.WorldPositionStays";
        private const int MaxResults = 200;
        private const float RowHeight = 34f;

        private static readonly Vector2 WindowSize = new Vector2(420f, 340f);

        private readonly List<ParentCandidate> _candidates = new List<ParentCandidate>();
        private readonly List<ParentCandidate> _ranked = new List<ParentCandidate>();
        private readonly List<Row> _rows = new List<Row>();

        private List<Transform> _roots;
        private Scene _scene;

        private TextField _search;
        private ListView _list;
        private Toggle _worldToggle;
        private Label _footer;

        private sealed class Row
        {
            public Transform Target;
            public string CreateName;
            public string Title;
            public string Subtitle;

            public bool IsCreate => CreateName != null;
        }

        /// <summary>Opens the picker anchored at <paramref name="screenAnchor"/> (screen space).</summary>
        public static void Open(Rect screenAnchor)
        {
            if (!ReparentOperation.TryGetContext(out List<Transform> roots, out Scene scene, out string error))
            {
                EditorUtility.DisplayDialog("Set Parent", error, "OK");
                return;
            }

            var window = CreateInstance<ReparentWindow>();
            window._roots = roots;
            window._scene = scene;
            window.ShowAsDropDown(screenAnchor, WindowSize);
        }

        private void CreateGUI()
        {
            ReparentIndex.Collect(_scene, _roots, _candidates);

            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;

            _search = new TextField();
            _search.style.marginBottom = 4f;
            _search.RegisterValueChangedCallback(evt => Refresh(evt.newValue));
            _search.RegisterCallback<KeyDownEvent>(OnSearchKeyDown, TrickleDown.TrickleDown);
            root.Add(_search);

            _list = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _rows
            };
            _list.style.flexGrow = 1f;
            _list.itemsChosen += _ => Confirm();
            _list.selectionChanged += _ => UpdateFooter();
            root.Add(_list);

            _worldToggle = new Toggle("Keep world position")
            {
                value = EditorPrefs.GetBool(WorldPositionPrefKey, true)
            };
            _worldToggle.style.marginTop = 4f;
            _worldToggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(WorldPositionPrefKey, evt.newValue));
            root.Add(_worldToggle);

            _footer = new Label();
            _footer.style.opacity = 0.6f;
            _footer.style.fontSize = 10f;
            root.Add(_footer);

            Refresh(string.Empty);

            // The text field only takes focus once the window has been laid out.
            _search.schedule.Execute(() => _search.Focus());
        }

        private void Refresh(string query)
        {
            string trimmed = query == null ? string.Empty : query.Trim();

            _rows.Clear();
            ReparentIndex.Rank(_candidates, trimmed, _ranked, MaxResults);

            for (int i = 0; i < _ranked.Count; i++)
            {
                ParentCandidate candidate = _ranked[i];
                _rows.Add(new Row
                {
                    Target = candidate.Target,
                    Title = candidate.Name,
                    Subtitle = candidate.Path
                });
            }

            if (trimmed.Length > 0 && !ReparentIndex.ContainsName(_candidates, trimmed))
            {
                // Appended last, so with real results on screen Enter never creates an object by accident.
                _rows.Add(new Row
                {
                    CreateName = trimmed,
                    Title = "+ Create \"" + trimmed + "\" and group",
                    Subtitle = DescribeCreateTarget()
                });
            }

            _list.Rebuild();
            _list.selectedIndex = _rows.Count > 0 ? 0 : -1;
            UpdateFooter();
        }

        private string DescribeCreateTarget()
        {
            Transform parent = _roots.Count > 0 ? _roots[0].parent : null;
            return parent != null ? "in " + parent.name : "at the root of " + _scene.name;
        }

        private static VisualElement MakeRow()
        {
            var container = new VisualElement();
            container.style.paddingLeft = 4f;
            container.style.paddingTop = 3f;
            container.style.justifyContent = Justify.Center;

            var title = new Label { name = "title" };
            container.Add(title);

            var subtitle = new Label { name = "subtitle" };
            subtitle.style.fontSize = 10f;
            subtitle.style.opacity = 0.55f;
            container.Add(subtitle);

            return container;
        }

        private void BindRow(VisualElement element, int index)
        {
            Row row = _rows[index];
            element.Q<Label>("title").text = row.Title;
            element.Q<Label>("subtitle").text = row.Subtitle;
        }

        private void UpdateFooter()
        {
            if (_footer == null)
            {
                return;
            }

            if (_rows.Count == 0)
            {
                _footer.text = "No results.";
                return;
            }

            int index = _list.selectedIndex;
            string target = index >= 0 && index < _rows.Count ? _rows[index].Title : "—";
            _footer.text = _roots.Count + (_roots.Count == 1 ? " object → " : " objects → ") + target;
        }

        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow:
                    Move(1);
                    break;
                case KeyCode.UpArrow:
                    Move(-1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Confirm();
                    break;
                case KeyCode.Escape:
                    Close();
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
        }

        private void Move(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            int next = Mathf.Clamp(_list.selectedIndex + delta, 0, _rows.Count - 1);
            _list.selectedIndex = next;
            _list.ScrollToItem(next);
            UpdateFooter();
        }

        private void Confirm()
        {
            int index = _list.selectedIndex;
            if (index < 0 || index >= _rows.Count)
            {
                return;
            }

            Row row = _rows[index];
            bool worldPositionStays = _worldToggle.value;
            List<Transform> roots = _roots;

            // Close first: the dropdown swallows dialogs and steals focus from the undo group.
            Close();

            if (row.IsCreate)
            {
                ReparentOperation.CreateAndGroup(roots, row.CreateName, worldPositionStays);
            }
            else
            {
                ReparentOperation.Apply(roots, row.Target, worldPositionStays);
            }
        }
    }
}
