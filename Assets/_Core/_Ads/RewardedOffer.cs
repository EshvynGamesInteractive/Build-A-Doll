using System;
using Core._GameEvent;
using UnityEngine;

namespace Game.Ads
{
    /// <summary>
    /// "Watch an ad for X?" asked by a track element (REMOVE FIRE?, Fashion House, rewarded ramp...).
    /// The HUD pop-up presents it; Accept plays the ad, Decline says no. The result arrives once.
    /// </summary>
    public sealed class RewardedOffer
    {
        private readonly Action<bool> _onResult;
        private bool _accepted;

        public string Title { get; }
        public Sprite Icon { get; }
        public string Placement { get; }
        public bool IsResolved { get; private set; }

        private RewardedOffer(string title, Sprite icon, string placement, Action<bool> onResult)
        {
            Title = title;
            Icon = icon;
            Placement = placement;
            _onResult = onResult;
        }

        /// <summary>Asks the player. Without a pop-up in the HUD the answer is "no" straight away.</summary>
        public static void Request(string title, Sprite icon, string placement, Action<bool> onResult)
        {
            var offer = new RewardedOffer(title, icon, placement, onResult);
            if (!HudPresenter<RewardedOffer>.TryPresent(offer))
                offer.Decline();
        }

        /// <summary>Plays the ad. 'onDone' runs after the result was handed back.</summary>
        public void Accept(Action onDone = null)
        {
            if (IsResolved || _accepted)
                return;

            _accepted = true;
            RewardedAds.Show(Placement, granted =>
            {
                Resolve(granted);
                onDone?.Invoke();
            });
        }

        public void Decline() => Resolve(false);

        private void Resolve(bool granted)
        {
            if (IsResolved)
                return;

            IsResolved = true;
            _onResult?.Invoke(granted);
        }
    }
}
