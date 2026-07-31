using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Noctis.Reparent
{
    /// <summary>
    /// Everything that mutates the hierarchy. Sole owner of the Undo handling for this tool:
    /// each public entry point collapses into a single undo step.
    /// </summary>
    public static class ReparentOperation
    {
        private const string UndoSetParent = "Set Parent";
        private const string UndoUnparent = "Unparent";
        private const string UndoGroup = "Group Under New Parent";

        /// <summary>
        /// Resolves the current selection into the transforms this tool should move, plus the scene
        /// they all belong to. Returns false with a user-facing reason when the selection is unusable.
        /// </summary>
        public static bool TryGetContext(out List<Transform> roots, out Scene scene, out string error)
        {
            roots = new List<Transform>();
            scene = default;
            error = null;

            // Selection.transforms already drops children whose parent is also selected.
            foreach (Transform candidate in Selection.transforms)
            {
                if (candidate == null || EditorUtility.IsPersistent(candidate.gameObject))
                {
                    continue;
                }

                roots.Add(candidate);
            }

            if (roots.Count == 0)
            {
                error = "No scene objects selected.";
                return false;
            }

            scene = roots[0].gameObject.scene;
            for (int i = 1; i < roots.Count; i++)
            {
                if (roots[i].gameObject.scene != scene)
                {
                    error = "The selection spans multiple scenes. Unity does not allow reparenting across scenes.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>Moves <paramref name="roots"/> under <paramref name="newParent"/> in one undo step.</summary>
        public static void Apply(IReadOnlyList<Transform> roots, Transform newParent, bool worldPositionStays)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(newParent == null ? UndoUnparent : UndoSetParent);
            int undoGroup = Undo.GetCurrentGroup();

            ApplyInternal(roots, newParent, worldPositionStays);

            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>Moves <paramref name="roots"/> to the root of their scene in one undo step.</summary>
        public static void UnparentToRoot(IReadOnlyList<Transform> roots, bool worldPositionStays)
        {
            Apply(roots, null, worldPositionStays);
        }

        /// <summary>
        /// Creates an empty GameObject named <paramref name="name"/> as a sibling of the first selected
        /// transform, then moves the whole selection into it. Grouping never relocates anything: the new
        /// parent inherits the first transform's parent and sibling index, and sits at the average world
        /// position of the selection.
        /// </summary>
        public static void CreateAndGroup(IReadOnlyList<Transform> roots, string name, bool worldPositionStays)
        {
            if (roots.Count == 0 || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Transform first = roots[0];
            Transform parent = first.parent;
            Scene scene = first.gameObject.scene;
            int siblingIndex = first.GetSiblingIndex();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoGroup);
            int undoGroup = Undo.GetCurrentGroup();

            var holder = new GameObject(name.Trim());
            StageUtility.PlaceGameObjectInCurrentStage(holder);
            Undo.RegisterCreatedObjectUndo(holder, UndoGroup);

            if (parent != null)
            {
                Undo.SetTransformParent(holder.transform, parent, false, UndoGroup);
            }
            else if (scene.IsValid() && holder.scene != scene)
            {
                Undo.MoveGameObjectToScene(holder, scene, UndoGroup);
            }

            holder.transform.position = AverageWorldPosition(roots);
            holder.transform.SetSiblingIndex(siblingIndex);

            ApplyInternal(roots, holder.transform, worldPositionStays);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = holder;
        }

        private static void ApplyInternal(IReadOnlyList<Transform> roots, Transform newParent, bool worldPositionStays)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                Transform current = roots[i];
                if (current == null || current == newParent || current.parent == newParent)
                {
                    continue;
                }

                Undo.SetTransformParent(current, newParent, worldPositionStays, UndoSetParent);
            }
        }

        private static Vector3 AverageWorldPosition(IReadOnlyList<Transform> roots)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                sum += roots[i].position;
                count++;
            }

            return count == 0 ? Vector3.zero : sum / count;
        }
    }
}
