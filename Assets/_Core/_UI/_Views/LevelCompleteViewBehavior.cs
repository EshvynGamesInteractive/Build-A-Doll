using System.Collections;
using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class LevelCompleteViewBehavior : UIScreen
    {
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button nextLevelButton;
        [Tooltip("On: Next opens the level map (Main Menu) with the next level as the current one; Play Now starts it. " +
                 "Off: Next loads the next level straight away.")]
        [SerializeField] private bool nextOpensLevelMap = true;
        [SerializeField] private TMP_Text outfitMatchText;
        [Tooltip("Optional: reward wheel (spin = rewarded ad, the segment under the pin multiplies the completion coins).")]
        [SerializeField] private SpinWheelView spinWheel;

        [Header("Reward")]
        [Tooltip("Swinging pin over the x2 · x3 · x5 · x4 · x2 arc + green ad button (SpinWheel / Multiplier in the prefab).")]
        [SerializeField] private RewardMultiplierView rewardMultiplier;
        [Tooltip("Coins on the Get button (the level's completion coins).")]
        [SerializeField] private TMP_Text completionCoinsText;
        [Tooltip("Seconds after the ad reward before going on as if Next was pressed. Negative = stay on the panel.")]
        [SerializeField] private float nextAfterClaimDelay = 1.2f;

        [Header("Special outfit progress")]
        [Tooltip("Parent of the progress bar. Shown after levels that count toward the next special outfit level, hidden otherwise.")]
        [SerializeField] private GameObject specialProgressRoot;
        [Tooltip("Image Type = Filled. Fills from the old progress to the new one when the panel opens.")]
        [SerializeField] private Image specialProgressFill;
        [Tooltip("e.g. \"Basketball Star 2/3\", or the unlocked text when the bar is full.")]
        [SerializeField] private TMP_Text specialProgressText;
        [Tooltip("Optional: picture of the special outfit.")]
        [SerializeField] private Image specialIcon;
        [Tooltip("Optional: shown only when the bar is full (e.g. an \"UNLOCKED!\" badge).")]
        [SerializeField] private GameObject specialUnlockedObject;
        [Tooltip("{0} = special outfit name, {1} = done, {2} = needed.")]
        [SerializeField] private string specialProgressFormat = "{0}  {1}/{2}";
        [Tooltip("{0} = special outfit name.")]
        [SerializeField] private string specialUnlockedFormat = "{0} UNLOCKED!";
        [SerializeField, Min(0f)] private float specialFillTime = 0.6f;

        [Header("Outfit flow: claim the unlocked outfit")]
        [Tooltip("Shown when an outfit was just unlocked (outfit flow). Holds the Claim / Skip buttons. Built in the prefab.")]
        [SerializeField] private GameObject claimRoot;
        [SerializeField] private Button claimButton;
        [Tooltip("Skip = the outfit is skipped for good (no free style level, no Gift Box / later).")]
        [SerializeField] private Button skipButton;
        [SerializeField] private Image claimIcon;
        [Tooltip("{0} = outfit name.")]
        [SerializeField] private TMP_Text claimTitle;
        [SerializeField] private string claimTitleFormat = "{0} UNLOCKED!";
        [Tooltip("On: Claim plays a rewarded ad first (the Claim button shows an ad icon).")]
        [SerializeField] private bool claimNeedsAd = true;
        [SerializeField] private string claimPlacement = Game.Ads.RewardedAds.ClaimOutfit;
        [Tooltip("Settings (gear) on the claim popup. Settings closes back to this panel with the popup still open.")]
        [SerializeField] private Button claimSettingsButton;

        private bool _hasSpecial;
        private float _fillFrom, _fillTo;
        private Coroutine _fill;

        private void OnEnable() => GameEvents.GameplayEvents.LevelCompleted.Register(OnLevelCompleted);

        private void OnDisable() => GameEvents.GameplayEvents.LevelCompleted.Unregister(OnLevelCompleted);

        private void OnLevelCompleted(LevelResult result)
        {
            _fillPlayed = false;
            _unlockSoundPlayed = false;
            _claimWaitingForAd = false;
            if (claimButton != null)
                claimButton.interactable = true;
            SetOutfitMatch(result);
            SetSpecialProgress(result);
            SetClaim(result);
            if (spinWheel != null)
                spinWheel.Prepare(result.CompletionCoins);
            if (completionCoinsText != null)
                completionCoinsText.text = result.CompletionCoins.ToString();
            // Completion coins are not in the wallet yet: Get pays them, the multiplier pays them × the segment instead.
            if (rewardMultiplier != null)
                rewardMultiplier.Prepare(result.CompletionCoins, false);
        }

        private Coroutine _autoNext;

        private void OnRewardClaimed(int coins)
        {
            if (nextAfterClaimDelay < 0f || !isActiveAndEnabled || container == null || !container.activeInHierarchy)
                return;
            if (_autoNext != null)
                StopCoroutine(_autoNext);
            _autoNext = StartCoroutine(NextAfterClaim());
        }

        private IEnumerator NextAfterClaim()
        {
            yield return new WaitForSecondsRealtime(nextAfterClaimDelay);
            _autoNext = null;
            // The outfit claim popup is still open: let the player choose first.
            if (_claimOpen)
                yield break;
            OnNextLevelButtonClick();
        }

        private void SetOutfitMatch(LevelResult result)
        {
            if (outfitMatchText == null)
                return;

            var show = result.Level.HasTarget || result.Level.IsFreeStyle;
            outfitMatchText.gameObject.SetActive(show);
            if (result.Level.IsFreeStyle)
                outfitMatchText.text = $"Style {result.Score}%";
            else if (result.Level.HasTarget)
                outfitMatchText.text = $"Outfit Match {result.Match.Percent}%";
        }

        // Regular levels after the first outfits fill a bar toward the next special outfit level (Basketball, Police…).
        private void SetSpecialProgress(LevelResult result)
        {
            var database = FindDatabase();
            if (database != null && database.UseThemePlan)
            {
                // Theme plan: levels since the last free style fill the bar toward the next one ("Nurse Free Style 2/3").
                _hasSpecial = database.TryGetThemeProgress(result.LevelNumber - 1, out var freeStyle, out var done, out var needed)
                              && Fill(freeStyle.ThemeName + " Free Style", freeStyle.ThemeIcon, done, needed);
            }
            else if (database != null && database.UseOutfitFlow)
            {
                // Outfit flow: main / Your Look levels fill the bar toward the next outfit reward.
                _hasSpecial = OutfitFlow.TryGetProgress(result.LevelNumber - 1, out var rewardIndex, out var done, out var needed);
                var reward = _hasSpecial ? database.GetReward(rewardIndex) : null;
                _hasSpecial = reward != null && Fill(reward.Title, reward.Icon, done, needed);
            }
            else
            {
                _hasSpecial = database != null && database.TryGetSpecialProgress(result.LevelNumber - 1, out var progress) && Fill(progress);
            }

            if (specialProgressRoot != null)
                specialProgressRoot.SetActive(_hasSpecial);
        }

        private bool Fill(LevelDatabase.SpecialProgress progress) =>
            Fill(progress.Special.SpecialTitle, progress.Special.SpecialIcon, progress.Done, progress.Needed);

        private bool Fill(string title, Sprite icon, int done, int needed)
        {
            if (needed <= 0)
                return false;
            _fillFrom = (float)(done - 1) / needed;
            _fillTo = (float)done / needed;
            var unlocked = done >= needed;

            if (specialProgressFill != null)
                specialProgressFill.fillAmount = _fillFrom;
            if (specialProgressText != null)
                specialProgressText.text = unlocked
                    ? string.Format(specialUnlockedFormat, title)
                    : string.Format(specialProgressFormat, title, done, needed);
            if (specialIcon != null)
            {
                specialIcon.sprite = icon;
                specialIcon.enabled = specialIcon.sprite != null;
            }
            if (specialUnlockedObject != null)
                specialUnlockedObject.SetActive(false); // shown when the fill arrives
            _unlocked = unlocked;
            return true;
        }

        // Outfit flow: an outfit was just unlocked → Claim (free style comes next) or Skip (gone for good).
        private int _claimIndex = -1;
        private bool _themeClaimPending;

        private void SetClaim(LevelResult result)
        {
            var database = FindDatabase();
            if (database != null && database.UseThemePlan)
            {
                // Theme plan: the next level is a free style level she hasn't reached yet → Claim (play it next) or Skip.
                _claimIndex = result.LevelNumber - 1;
                _themeClaimPending = database.TryGetNextFreeStyle(_claimIndex, out var freeStyle)
                                     && LevelProgress.CurrentLevelIndex == _claimIndex + 1;
                SetClaimVisible(_themeClaimPending);
                if (!_themeClaimPending)
                    return;
                if (claimTitle != null)
                    claimTitle.text = string.Format(claimTitleFormat, freeStyle.ThemeName + " Free Style");
                if (claimIcon != null)
                {
                    claimIcon.sprite = freeStyle.ThemeIcon;
                    claimIcon.enabled = claimIcon.sprite != null;
                }
                return;
            }
            _themeClaimPending = false;
            var pending = database != null && database.UseOutfitFlow ? OutfitFlow.PendingUnlock : -1;
            var reward = database != null ? database.GetReward(pending) : null;
            SetClaimVisible(reward != null);
            if (reward == null)
                return;
            if (claimTitle != null)
                claimTitle.text = string.Format(claimTitleFormat, reward.Title);
            if (claimIcon != null)
            {
                claimIcon.sprite = reward.Icon;
                claimIcon.enabled = claimIcon.sprite != null;
            }
        }

        // The popup sits outside Container, so it is hidden / restored with the panel (see Show / Hide).
        private bool _claimOpen;
        private bool _claimWaitingForAd;

        private void SetClaimVisible(bool open)
        {
            _claimOpen = open;
            if (claimRoot != null)
                claimRoot.SetActive(open && container != null && container.activeSelf);
        }

        private void OnClaimClick()
        {
            if (!_claimOpen || _claimWaitingForAd)
                return;
            if (!claimNeedsAd)
            {
                DoClaim();
                return;
            }
            _claimWaitingForAd = true;
            if (claimButton != null)
                claimButton.interactable = false;
            Game.Ads.RewardedAds.Show(claimPlacement, DoClaim, OnClaimAdFailed);
        }

        private void OnClaimAdFailed()
        {
            _claimWaitingForAd = false;
            if (claimButton != null)
                claimButton.interactable = true;
        }

        private void DoClaim()
        {
            _claimWaitingForAd = false;
            if (claimButton != null)
                claimButton.interactable = true;
            if (_themeClaimPending)
                _themeClaimPending = false; // the free style level is simply the next level
            else
                OutfitFlow.ClaimPending();
            SetClaimVisible(false);
            LevelProgress.CollectPendingLevelCoins();
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Raise();
        }

        private void OnSkipClick()
        {
            if (!_claimOpen || _claimWaitingForAd)
                return;
            if (_themeClaimPending)
                SkipThemeFreeStyle();
            else
                OutfitFlow.SkipPending();
            SetClaimVisible(false);
            LevelProgress.CollectPendingLevelCoins();
            GameEvents.GameplayEvents.RequestPlaySavedLevel.Raise();
        }

        // Theme plan Skip: the free style level is passed over (it stays on the map, playable any time; its styled level
        // uses the theme's default look until she designs one).
        private void SkipThemeFreeStyle()
        {
            _themeClaimPending = false;
            if (LevelProgress.CurrentLevelIndex == _claimIndex + 1)
                LevelProgress.CurrentLevelIndex = _claimIndex + 2;
        }

        // Leaving the panel without choosing = Skip (the popup blocks the panel, so this is only a safety net).
        private void SkipIfUndecided()
        {
            SetClaimVisible(false);
            if (_themeClaimPending)
            {
                SkipThemeFreeStyle();
                return;
            }
            if (OutfitFlow.PendingUnlock >= 0)
                OutfitFlow.SkipPending();
        }

        private bool _unlocked;
        private bool _fillPlayed;
        private bool _unlockSoundPlayed;

        // Once per result: the claim popup and the full progress bar can both show the same unlock.
        private void PlayUnlockSound()
        {
            if (_unlockSoundPlayed)
                return;
            _unlockSoundPlayed = true;
            GameEvents.AudioEvents.PlayCue.Raise(_Core._Audio.SoundCue.OutfitUnlocked);
        }

        public override void Show()
        {
            base.Show();
            // Back from Settings with the choice still open: show the popup again.
            if (claimRoot != null)
                claimRoot.SetActive(_claimOpen);
            if (_claimOpen)
                PlayUnlockSound();
            if (!_hasSpecial || _fillPlayed)
                return;
            _fillPlayed = true;

            if (_fill != null)
                StopCoroutine(_fill);
            _fill = StartCoroutine(AnimateFill());
        }

        public override void Hide()
        {
            // Outside Container: hide it ourselves so it never covers Settings / the map / the next level.
            if (claimRoot != null)
                claimRoot.SetActive(false);
            if (_autoNext != null)
            {
                StopCoroutine(_autoNext);
                _autoNext = null;
            }
            if (_fill != null)
            {
                StopCoroutine(_fill);
                _fill = null;
            }
            base.Hide();
        }

        private IEnumerator AnimateFill()
        {
            for (var t = 0f; t < specialFillTime; t += Time.unscaledDeltaTime)
            {
                if (specialProgressFill != null)
                    specialProgressFill.fillAmount = Mathf.Lerp(_fillFrom, _fillTo, Mathf.SmoothStep(0f, 1f, t / specialFillTime));
                yield return null;
            }

            if (specialProgressFill != null)
                specialProgressFill.fillAmount = _fillTo;
            if (specialUnlockedObject != null)
                specialUnlockedObject.SetActive(_unlocked);
            if (_unlocked)
                PlayUnlockSound();
            _fill = null;
        }

        private static LevelDatabase FindDatabase()
        {
            var manager = ReferenceManager.Instance != null ? ReferenceManager.Instance.levelManager : null;
            if (manager == null)
                manager = FindFirstObjectByType<_Core._Level.LevelManager>();
            return manager != null ? manager.Database : null;
        }

        protected override void Initialize()
        {
            base.Initialize();
            
            UIPanelButtonUtility.Bind(retryButton, OnRetryCurrentLevelButtonClick);
            UIPanelButtonUtility.Bind(homeButton, OnHomeButtonClick);
            UIPanelButtonUtility.Bind(nextLevelButton, OnNextLevelButtonClick);
            if (claimButton != null)
                UIPanelButtonUtility.Bind(claimButton, OnClaimClick);
            if (skipButton != null)
                UIPanelButtonUtility.Bind(skipButton, OnSkipClick);
            UIPanelButtonUtility.Bind(claimSettingsButton, OnClaimSettingsClick);
            if (rewardMultiplier != null)
                rewardMultiplier.Claimed += OnRewardClaimed;
        }

        private void OnClaimSettingsClick()
        {
            if (_claimWaitingForAd)
                return;
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings);
        }

        private void OnRetryCurrentLevelButtonClick()
        {
            SkipIfUndecided();
            GameEvents.GameplayEvents.RequestRestartLevel.Raise();
        }

        private void OnHomeButtonClick()
        {
            SkipIfUndecided();
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
        }

        // The level just passed is already saved, so the map's current level is the next one.
        private void OnNextLevelButtonClick()
        {
            SkipIfUndecided();
            LevelProgress.CollectPendingLevelCoins(); // Get button (nothing left if the multiplier already paid)
            if (nextOpensLevelMap)
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
            else
                GameEvents.GameplayEvents.RequestNextLevel.Raise();
        }
    }
}
