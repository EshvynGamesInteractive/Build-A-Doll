using Core._GameEvent;
using Game.Level;
using TMPro;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// On the CoinBar prefab: keeps its number equal to the saved wallet (LevelProgress.TotalCoins).
    /// Every CoinBar (Home top bar, HUD, target-look card, makeup screen) updates by itself.
    /// </summary>
    public class CoinBarView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("{0} = wallet coins.")]
        [SerializeField] private string format = "{0}";

        private void OnEnable()
        {
            GameEvents.GameplayEvents.WalletChanged.Register(SetCoins);
            SetCoins(LevelProgress.TotalCoins);
        }

        private void OnDisable() => GameEvents.GameplayEvents.WalletChanged.Unregister(SetCoins);

        private void SetCoins(int coins)
        {
            if (coinsText != null)
                coinsText.text = string.Format(format, coins);
        }
    }
}
