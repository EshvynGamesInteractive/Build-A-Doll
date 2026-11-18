using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class HomeMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TextMeshProUGUI coinsLabel;
        [Tooltip("Level map: its buttons play the clicked level. Play shortcut = the current level.")]
        [SerializeField] private _LevelMap.LevelMapView levelMap;

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(playButton, GameEvents.GameplayEvents.RequestPlaySavedLevel.Raise);
            UIPanelButtonUtility.Bind(settingsButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings));
            SetCoins(LevelProgress.TotalCoins);
        }

        private void OnEnable() => GameEvents.GameplayEvents.WalletChanged.Register(SetCoins);

        private void OnDisable() => GameEvents.GameplayEvents.WalletChanged.Unregister(SetCoins);

        public override void Show()
        {
            base.Show();
            SetCoins(LevelProgress.TotalCoins);
            if (levelMap != null)
                levelMap.Refresh();
        }

        private void SetCoins(int coins)
        {
            if (coinsLabel != null)
                coinsLabel.text = coins.ToString();
        }
    }
}
