using _Core._Player;
using Game.Ads;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A set piece that pays out after a rewarded ad: touch it, watch the ad, get its effects.
    /// Rewarded ramp (MountEffect with wings + clouds), rewarded pet (MountEffect: dolphin, bird, reptile),
    /// rewarded seahorse (MountEffect + TerrainEffect Underwater), rewarded shield (StatusEffect Shielded),
    /// fairy companion (CompanionEffect), rewarded fix (RestoreEffect), Fashion House (EquipItemsEffect).
    /// With Requires Ad off it's free (e.g. a Fashion House without an ad). The AI never watches ads.
    /// </summary>
    public class RewardedSetPiece : EffectHurdle
    {
        [Header("Rewarded")]
        [Tooltip("On: touching it offers an ad and the effects happen only if it's watched. Off: free.")]
        [SerializeField] private bool requiresAd = true;

        [Tooltip("Pop-up title, e.g. \"FLY WITH WINGS?\".")]
        [SerializeField] private string offerTitle = "WATCH AD?";
        [SerializeField] private Sprite offerIcon;
        [SerializeField] private string adPlacement = "set_piece";

        [Tooltip("Shown only when an ad is needed (e.g. the Fashion House's AdIcon).")]
        [SerializeField] private GameObject adIcon;

        [Tooltip("Stop her while the offer is open.")]
        [SerializeField] private bool holdRunnerDuringOffer = true;

        protected override void Awake()
        {
            base.Awake();
            if (adIcon != null)
                adIcon.SetActive(requiresAd);
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            if (!requiresAd)
            {
                ApplyEffects(player);
                return;
            }

            if (player.IsAI)
                return;

            if (holdRunnerDuringOffer)
                player.Hold(this);

            RewardedOffer.Request(offerTitle, offerIcon, adPlacement, granted => OnOfferAnswered(player, granted));
        }

        private void OnOfferAnswered(RunnerController player, bool granted)
        {
            if (player == null)
                return;

            player.Release(this);
            if (granted)
                ApplyEffects(player);
        }
    }
}
