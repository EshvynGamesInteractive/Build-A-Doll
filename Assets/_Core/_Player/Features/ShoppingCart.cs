using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using Game.Hurdles.Data;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// The cart on item-collection levels ("Collect These Kitchen Items"). Holds up to Capacity items,
    /// right or wrong; the level wants the items in Wanted. Emptied on ResetRun (the wanted list stays).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShoppingCart : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 3;

        private readonly List<CollectibleItemData> _items = new();
        private readonly List<CollectibleItemData> _wanted = new();
        private readonly List<CollectibleItemData> _buffer = new();
        private RunnerController _runner;

        public int Capacity => capacity;
        public IReadOnlyList<CollectibleItemData> Items => _items;
        public IReadOnlyList<CollectibleItemData> Wanted => _wanted;
        public bool IsFull => _items.Count >= capacity;

        /// <summary>How many wanted items are in the cart (each wanted entry counts once).</summary>
        public int CorrectCount
        {
            get
            {
                _buffer.Clear();
                _buffer.AddRange(_items);
                var correct = 0;
                foreach (var wanted in _wanted)
                {
                    if (_buffer.Remove(wanted))
                        correct++;
                }

                return correct;
            }
        }

        /// <summary>0..100: share of the wanted list that's in the cart.</summary>
        public int MatchPercent => _wanted.Count == 0 ? 0 : Mathf.RoundToInt(100f * CorrectCount / _wanted.Count);

        public bool IsWanted(CollectibleItemData item) => item != null && _wanted.Contains(item);

        private void Awake() => _runner = GetComponentInParent<RunnerController>();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(Empty);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(Empty);

        public void SetWanted(IReadOnlyList<CollectibleItemData> wanted)
        {
            _wanted.Clear();
            if (wanted != null)
            {
                foreach (var item in wanted)
                {
                    if (item != null)
                        _wanted.Add(item);
                }
            }

            RaiseChanged();
        }

        public bool TryAdd(CollectibleItemData item)
        {
            if (item == null || IsFull)
                return false;

            _items.Add(item);
            RaiseChanged();
            return true;
        }

        private void Empty()
        {
            _items.Clear();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (_runner == null || !_runner.IsAI)
                GameEvents.TrackEvents.CartChanged.Raise(this);
        }
    }
}
