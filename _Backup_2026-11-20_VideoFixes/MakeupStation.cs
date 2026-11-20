using System.Collections.Generic;
using _Core._Player;
using Game.Outfit;
using Game.UI;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Makeup mirror on the track (like the reference): running into it opens the paint tool for one zone
    /// (eye shadow or lips) with a palette of up to 3 colours: Free / Coins / Rewarded ad.
    /// Colours (and names) can come from the level entry's Paint Gates list (zone + name + colour, in palette order);
    /// the price of each slot stays as set here. The AI paints by itself (often the target colour).
    /// </summary>
    public class MakeupStation : TrackHurdle
    {
        [SerializeField] private MakeupZone zone = MakeupZone.EyeShadow;
        [SerializeField] private List<PaintToolView.PaletteOption> options = new()
        {
            new() { name = "Pink", color = new Color(1f, 0.4f, 0.85f), price = PaintToolView.PriceType.Free },
            new() { name = "Brown", color = new Color(0.55f, 0.27f, 0.07f), price = PaintToolView.PriceType.Coins, cost = 500 },
            new() { name = "Purple", color = new Color(0.6f, 0.3f, 0.9f), price = PaintToolView.PriceType.RewardedAd }
        };
        [Tooltip("Chance the AI picks the target colour (if it's in the palette).")]
        [SerializeField, Range(0f, 1f)] private float aiTargetChance = 0.6f;

        private TargetLook _look;

        protected override bool AllowRuntimeObjects => false;

        public MakeupZone Zone => zone;

        protected override void Awake()
        {
            base.Awake();
            SetChoiceMatch(true);
        }

        /// <summary>Level entry overrides: zone, name and colour per palette slot (prices stay as on the prefab).</summary>
        public void Configure(PaintGate.Setting[] settings, TargetLook look)
        {
            _look = look;
            if (settings == null || settings.Length == 0)
                return;

            zone = settings[0].zone;
            for (var i = 0; i < settings.Length; i++)
            {
                if (i >= options.Count)
                    options.Add(new PaintToolView.PaletteOption { price = PaintToolView.PriceType.Free });
                options[i].name = settings[i].colorName;
                options[i].color = settings[i].color;
            }

            if (options.Count > settings.Length)
                options.RemoveRange(settings.Length, options.Count - settings.Length);
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var makeup = player.GetComponentInChildren<DollMakeup>();
            if (player.IsAI)
            {
                if (makeup != null && makeup.CanPaint(zone))
                {
                    makeup.RaiseEvents = false;
                    makeup.Paint(zone, AiColor());
                }
                return;
            }

            // Without authored face overlays the paint tool opens in UI preview (nothing is painted).
            if (makeup != null) makeup.RaiseEvents = true;

            var first = options.Count > 0 ? options[0].color : Color.white;
            if (PaintToolView.Instance == null || !PaintToolView.Instance.TryBegin(player, makeup, zone, first, options))
                Debug.LogWarning("Makeup station needs a configured, active PaintToolView in the scene.", this);
        }

        private Color AiColor()
        {
            if (_look != null && _look.TryGetMakeup(zone, out var wanted) && Random.value < aiTargetChance)
                return wanted.color;
            return options.Count > 0 ? options[Random.Range(0, options.Count)].color : Color.white;
        }
    }
}
