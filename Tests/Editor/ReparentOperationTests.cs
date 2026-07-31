using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Noctis.Reparent.Tests
{
    public sealed class ReparentOperationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<ParentCandidate> _candidates = new List<ParentCandidate>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
            Undo.ClearAll();
        }

        [Test]
        public void CollectExcludesTheSelectionAndItsDescendants()
        {
            GameObject parent = Spawn("Reparent_Test_Parent");
            GameObject moved = Spawn("Reparent_Test_Moved", parent.transform);
            GameObject child = Spawn("Reparent_Test_Child", moved.transform);

            ReparentIndex.Collect(SceneManager.GetActiveScene(), new[] { moved.transform }, _candidates);

            Assert.IsTrue(Contains(parent.transform), "The untouched parent should stay available.");
            Assert.IsFalse(Contains(moved.transform), "A selected object cannot be its own parent.");
            Assert.IsFalse(Contains(child.transform), "A descendant of the selection would create a cycle.");
        }

        [Test]
        public void CollectReportsTheAncestorPath()
        {
            GameObject root = Spawn("Reparent_Test_Root");
            GameObject middle = Spawn("Reparent_Test_Middle", root.transform);
            GameObject leaf = Spawn("Reparent_Test_Leaf", middle.transform);

            ReparentIndex.Collect(SceneManager.GetActiveScene(), System.Array.Empty<Transform>(), _candidates);

            // Roots are labelled with the scene; an unsaved scene must still produce a usable label.
            string sceneLabel = PathOf(root.transform);
            Assert.IsFalse(string.IsNullOrEmpty(sceneLabel), "Root objects need a non-empty path label.");

            Assert.AreEqual(sceneLabel + "/Reparent_Test_Root", PathOf(middle.transform));
            Assert.AreEqual(sceneLabel + "/Reparent_Test_Root/Reparent_Test_Middle", PathOf(leaf.transform));
        }

        [Test]
        public void ApplySetsTheParent()
        {
            GameObject target = Spawn("Reparent_Test_Target");
            GameObject moved = Spawn("Reparent_Test_Moved");

            ReparentOperation.Apply(new[] { moved.transform }, target.transform, true);

            Assert.AreSame(target.transform, moved.transform.parent);
        }

        [Test]
        public void ApplyKeepsWorldPositionWhenAsked()
        {
            GameObject target = Spawn("Reparent_Test_Target");
            target.transform.position = new Vector3(10f, 0f, 0f);

            GameObject moved = Spawn("Reparent_Test_Moved");
            moved.transform.position = new Vector3(1f, 2f, 3f);

            ReparentOperation.Apply(new[] { moved.transform }, target.transform, true);

            Assert.That(Vector3.Distance(moved.transform.position, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void ApplyKeepsLocalPositionWhenWorldPositionIsNotPreserved()
        {
            GameObject target = Spawn("Reparent_Test_Target");
            target.transform.position = new Vector3(10f, 0f, 0f);

            GameObject moved = Spawn("Reparent_Test_Moved");
            moved.transform.position = new Vector3(1f, 2f, 3f);

            ReparentOperation.Apply(new[] { moved.transform }, target.transform, false);

            Assert.That(Vector3.Distance(moved.transform.localPosition, new Vector3(1f, 2f, 3f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(moved.transform.position, new Vector3(11f, 2f, 3f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void UnparentToRootClearsTheParent()
        {
            GameObject parent = Spawn("Reparent_Test_Parent");
            GameObject moved = Spawn("Reparent_Test_Moved", parent.transform);

            ReparentOperation.UnparentToRoot(new[] { moved.transform }, true);

            Assert.IsNull(moved.transform.parent);
        }

        [Test]
        public void ApplyCollapsesIntoASingleUndoStep()
        {
            GameObject original = Spawn("Reparent_Test_Original");
            GameObject target = Spawn("Reparent_Test_Target");
            GameObject first = Spawn("Reparent_Test_First", original.transform);
            GameObject second = Spawn("Reparent_Test_Second", original.transform);

            ReparentOperation.Apply(new[] { first.transform, second.transform }, target.transform, true);
            Undo.PerformUndo();

            Assert.AreSame(original.transform, first.transform.parent, "One undo should restore every moved object.");
            Assert.AreSame(original.transform, second.transform.parent, "One undo should restore every moved object.");
        }

        [Test]
        public void CreateAndGroupPlacesTheHolderUnderTheOriginalParent()
        {
            GameObject parent = Spawn("Reparent_Test_Parent");
            GameObject first = Spawn("Reparent_Test_First", parent.transform);
            GameObject second = Spawn("Reparent_Test_Second", parent.transform);

            ReparentOperation.CreateAndGroup(new[] { first.transform, second.transform }, "Reparent_Test_Group", false);

            Transform holder = first.transform.parent;
            _spawned.Add(holder.gameObject);

            Assert.AreEqual("Reparent_Test_Group", holder.name);
            Assert.AreSame(parent.transform, holder.parent, "Grouping must not relocate anything.");
            Assert.AreSame(holder, second.transform.parent);
        }

        [Test]
        public void CreateAndGroupCentresTheHolderOnTheSelection()
        {
            GameObject first = Spawn("Reparent_Test_First");
            first.transform.position = new Vector3(0f, 0f, 0f);

            GameObject second = Spawn("Reparent_Test_Second");
            second.transform.position = new Vector3(4f, 0f, 2f);

            ReparentOperation.CreateAndGroup(new[] { first.transform, second.transform }, "Reparent_Test_Group", true);

            Transform holder = first.transform.parent;
            _spawned.Add(holder.gameObject);

            Assert.That(Vector3.Distance(holder.position, new Vector3(2f, 0f, 1f)), Is.LessThan(0.0001f));
        }

        private GameObject Spawn(string name, Transform parent = null)
        {
            var created = new GameObject(name);
            if (parent != null)
            {
                created.transform.SetParent(parent, false);
            }

            _spawned.Add(created);
            return created;
        }

        private bool Contains(Transform target)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Target == target)
                {
                    return true;
                }
            }

            return false;
        }

        private string PathOf(Transform target)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Target == target)
                {
                    return _candidates[i].Path;
                }
            }

            return null;
        }
    }
}
