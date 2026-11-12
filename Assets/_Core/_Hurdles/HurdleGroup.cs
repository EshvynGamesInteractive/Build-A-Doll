using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Put on the parent of a left/right gate pair (or any set of choices).
    /// The first hurdle the player hits wins; the rest switch off so only one item is taken.
    /// </summary>
    public class HurdleGroup : MonoBehaviour
    {
        private bool _claimed;

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetGroup);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetGroup);

        public bool TryClaim(TrackHurdle winner)
        {
            if (_claimed)
                return false;

            _claimed = true;

            foreach (var hurdle in GetComponentsInChildren<TrackHurdle>())
            {
                if (hurdle != winner)
                    hurdle.Use();
            }

            return true;
        }

        private void ResetGroup() => _claimed = false;
    }
}
