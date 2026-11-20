using System;
using System.Collections;
using Core._GameEvent;
using Game.Level;
using UnityEngine;

namespace _Core._UI._LevelMap
{
    /// <summary>
    /// Main-menu level map. Uses a fixed set of LevelNode buttons built in the prefab (first = earliest level).
    /// Node i shows level i+1 until the player gets near the end of the list; then the numbers slide along so
    /// 'Locked Ahead' locked levels always show after the current one. Scrolls the map to the current level.
    /// Clicking a played or the current level plays it (GameplayEvents.RequestPlayLevel).
    /// </summary>
    public class LevelMapView : MonoBehaviour
    {
        [Tooltip("Level buttons in level order: the first one shows the earliest level.")]
        [SerializeField] private LevelNode[] nodes;
        
        [SerializeField] private Sprite completedState;
        [SerializeField] private Sprite currentState;
        [SerializeField] private Sprite lockedState;
        
        [Tooltip("How many locked levels always show after the current one. Once the player gets that close to the last node, the numbers slide along.")]
        [SerializeField, Min(0)] private int lockedAhead = 4;

        [Tooltip("Optional: the map's scroll view. It scrolls so the current level is in the middle.")]
        [SerializeField] private UnityEngine.UI.ScrollRect scrollRect;

        [Tooltip("Used only when no LevelManager/database is found: every Nth level gets the special badge. Otherwise special outfit levels (LevelData Is Special) get it.")]
        [SerializeField, Min(0)] private int specialEvery = 5;

        private void OnEnable() => Refresh();

        /// <summary>Re-reads saved progress and updates every node.</summary>
        public void Refresh()
        {
            if (nodes == null || nodes.Length == 0)
                return;

            var current = LevelProgress.CurrentLevelIndex;
            var manager = ReferenceManager.Instance != null ? ReferenceManager.Instance.levelManager : null;
            if (manager == null)
                manager = FindFirstObjectByType<_Core._Level.LevelManager>();
            var database = manager != null ? manager.Database : null;
            var first = Mathf.Max(0, current + Mathf.Min(lockedAhead, nodes.Length - 1) + 1 - nodes.Length);
            LevelNode currentNode = null;

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var index = first + i;
                var state = index < current ? LevelNode.State.Completed
                    : index == current ? LevelNode.State.Current
                    : LevelNode.State.Locked;

                switch (state)
                {
                    case LevelNode.State.Completed:
                        node.SetStateIcon(completedState);
                        break;
                    case LevelNode.State.Current:
                        node.SetStateIcon(currentState);
                        break;
                    case LevelNode.State.Locked:
                        node.SetStateIcon(lockedState);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Special outfit levels come from the level data; Special Every is the fallback without a database.
                var special = database != null ? database.IsSpecialAt(index) : specialEvery > 0 && (index + 1) % specialEvery == 0;
                node.Setup(index, state, special, PlayLevel);
                if (state == LevelNode.State.Current)
                    currentNode = node;
            }

            if (scrollRect != null && currentNode != null && isActiveAndEnabled)
            {
                if (_scroll != null)
                    StopCoroutine(_scroll);
                _scroll = StartCoroutine(ScrollTo(currentNode.Rect));
            }
        }

        private Coroutine _scroll;

        // Lets the layout settle (layout group + size fitter), then centres the node in the scroll view.
        private IEnumerator ScrollTo(RectTransform node)
        {
            yield return null;
            var content = scrollRect.content;
            if (content != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            _scroll = null;

            var viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            if (content == null || node == null)
                yield break;

            var scrollable = content.rect.height - viewport.rect.height;
            if (scrollable <= 0f)
                yield break;

            // Distance from the top of the content to the node's centre, then how far down to scroll to centre it.
            var nodeCentre = content.InverseTransformPoint(node.TransformPoint(node.rect.center));
            var fromTop = content.rect.yMax - nodeCentre.y;
            var offset = fromTop - viewport.rect.height * 0.5f;
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
            scrollRect.velocity = Vector2.zero;
        }

        private static void PlayLevel(int index) => GameEvents.GameplayEvents.RequestPlayLevel.Raise(index);
    }
}
