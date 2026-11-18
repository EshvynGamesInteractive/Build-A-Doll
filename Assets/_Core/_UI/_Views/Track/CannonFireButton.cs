using Core._GameEvent;
using Core.Utilities;
using Game.Hurdles;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// The "Fire!" button in the Gameplay HUD, shown while the player sits in a CannonSpinner.
    /// The button and its text are set up in the prefab.
    /// </summary>
    public class CannonFireButton : MonoBehaviour, IHudPresenter<CannonSpinner>
    {
        [Tooltip("Shown while she's in a cannon (holds the Fire! button).")]
        [SerializeField] private GameObject container;
        [SerializeField] private Button fireButton;

        [Tooltip("Optional: an Image (Filled) showing how good the shot would be right now.")]
        [SerializeField] private Image powerFill;

        private CannonSpinner _cannon;

        private void Awake()
        {
            UIPanelButtonUtility.Bind(fireButton, Fire);
            HudPresenter<CannonSpinner>.Register(this);
            GameEvents.GameplayEvents.ResetRun.Register(Close);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            HudPresenter<CannonSpinner>.Unregister(this);
            GameEvents.GameplayEvents.ResetRun.Unregister(Close);
        }

        public void Present(CannonSpinner cannon)
        {
            _cannon = cannon;
            SetVisible(true);
        }

        private void Update()
        {
            if (_cannon == null)
                return;

            if (!_cannon.IsWaiting)
            {
                Close();
                return;
            }

            if (powerFill != null)
                powerFill.fillAmount = _cannon.Power;
        }

        private void Fire()
        {
            var cannon = _cannon;
            Close();
            if (cannon != null)
                cannon.Fire();
        }

        private void Close()
        {
            _cannon = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (container != null)
                container.SetActive(visible);
        }
    }
}
