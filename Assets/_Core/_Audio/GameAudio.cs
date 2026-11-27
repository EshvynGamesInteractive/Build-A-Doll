using System;
using System.Collections;
using _Core._UI;
using Core._GameEvent;
using Game.Level;
using Game.Outfit;
using UnityEngine;

namespace _Core._Audio
{
    /// <summary>
    /// All game sound in one place (Bootstrap > Managers > SoundsManager). Listens to game events, so gameplay code
    /// doesn't play clips itself:
    ///   Music:    Menu music on Home / map, gameplay music from the target card to the finish (cross-faded),
    ///             turned down while Pause / Level Complete / Level Failed is open.
    ///   UI:       button click (every button bound through UIPanelButtonUtility, level nodes, tabs, palette),
    ///             a pop when a panel opens.
    ///   Run:      coin pickup, outfit gate, penalty, jump, slide.
    ///   Finish:   comparison drum roll, ✓ / ✗ per compared item, win / lose jingle, confetti.
    ///   Rewards:  outfit unlocked (Level Complete), coin shower (reward multiplier).
    /// Music / Sound on-off come from Settings. Every clip has its own volume; empty clip = silent.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [Serializable]
        public class Sfx
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch ± this much each time (0 = always the same). Keeps repeated sounds (coins) from sounding robotic.")]
            [Range(0f, 0.3f)] public float pitchJitter;
        }

        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource effectsSource;

        [Header("Music")]
        [SerializeField] private AudioClip menuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;
        [Tooltip("Music level (× Music Volume) while Pause / Level Complete / Level Failed is open.")]
        [SerializeField, Range(0f, 1f)] private float duckedMusic = 0.35f;
        [SerializeField, Min(0f)] private float musicFade = 0.6f;

        [Header("UI")]
        [SerializeField] private Sfx buttonClick = new() { volume = 0.8f };
        [SerializeField] private Sfx panelOpen = new() { volume = 0.6f };

        [Header("Run")]
        [SerializeField] private Sfx coinPickup = new() { volume = 0.7f, pitchJitter = 0.06f };
        [Tooltip("Outfit gate passed (an item was put on during the run).")]
        [SerializeField] private Sfx gatePickup = new() { volume = 0.8f };
        [SerializeField] private Sfx penalty = new() { volume = 0.8f };
        [SerializeField] private Sfx jump = new() { volume = 0.7f, pitchJitter = 0.04f };
        [SerializeField] private Sfx slide = new() { volume = 0.7f, pitchJitter = 0.04f };

        [Header("Finish")]
        [SerializeField] private Sfx comparisonStart = new() { volume = 0.7f };
        [SerializeField] private Sfx comparisonMatch = new() { volume = 0.8f };
        [SerializeField] private Sfx comparisonMiss = new() { volume = 0.7f };
        [SerializeField] private Sfx levelComplete = new() { volume = 0.9f };
        [SerializeField] private Sfx levelFailed = new() { volume = 0.6f };
        [SerializeField] private Sfx confetti = new() { volume = 0.8f };

        [Header("Rewards")]
        [SerializeField] private Sfx outfitUnlocked = new() { volume = 0.9f };
        [SerializeField] private Sfx coinReward = new() { volume = 0.8f };

        private AudioSource _musicB;          // second music source for cross-fades
        private AudioSource _musicActive;
        private Coroutine _musicFade;
        private bool _musicMuted;
        private float _musicLevel = 1f;       // 1 or duckedMusic
        private ScreenType? _screen;
        private bool _inRun;
        private int _lastCoins;
        private float _lastCoinTime = -1f;

        private void Awake()
        {
            if (musicSource != null)
            {
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                _musicB = musicSource.gameObject.AddComponent<AudioSource>();
                _musicB.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;
                _musicB.loop = true;
                _musicB.playOnAwake = false;
                _musicB.spatialBlend = musicSource.spatialBlend;
                _musicB.priority = musicSource.priority;
                _musicActive = musicSource;
            }
            if (effectsSource != null)
                effectsSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            SetMusicVolume(GameSettings.IsEnabled(GameConstants.MusicKey) ? 1f : 0f);
            SetSoundVolume(GameSettings.IsEnabled(GameConstants.SoundKey) ? 1f : 0f);

            GameEvents.AudioEvents.PlaySfx.Register(Play);
            GameEvents.AudioEvents.PlayCue.Register(PlayCue);
            GameEvents.AudioEvents.ButtonClicked.Register(PlayButton);
            GameEvents.AudioEvents.SetMusicVolume.Register(SetMusicVolume);
            GameEvents.AudioEvents.SetSoundVolume.Register(SetSoundVolume);
            GameEvents.UIEvents.ShowByType.Register(OnScreen);

            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.CoinsChanged.Register(OnCoinsChanged);
            GameEvents.GameplayEvents.OutfitItemEquipped.Register(OnItemEquipped);
            GameEvents.GameplayEvents.PenaltyTaken.Register(OnPenalty);
            GameEvents.GameplayEvents.ComparisonStarted.Register(OnComparisonStarted);
            GameEvents.GameplayEvents.ComparisonItemShown.Register(OnComparisonItem);
            GameEvents.GameplayEvents.LevelCompleted.Register(OnLevelCompleted);
            GameEvents.GameplayEvents.LevelFailed.Register(OnLevelFailed);
            GameEvents.GameplayEvents.SpinRewardGranted.Register(OnSpinReward);
        }

        private void OnDisable()
        {
            GameEvents.AudioEvents.PlaySfx.Unregister(Play);
            GameEvents.AudioEvents.PlayCue.Unregister(PlayCue);
            GameEvents.AudioEvents.ButtonClicked.Unregister(PlayButton);
            GameEvents.AudioEvents.SetMusicVolume.Unregister(SetMusicVolume);
            GameEvents.AudioEvents.SetSoundVolume.Unregister(SetSoundVolume);
            GameEvents.UIEvents.ShowByType.Unregister(OnScreen);

            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            GameEvents.GameplayEvents.CoinsChanged.Unregister(OnCoinsChanged);
            GameEvents.GameplayEvents.OutfitItemEquipped.Unregister(OnItemEquipped);
            GameEvents.GameplayEvents.PenaltyTaken.Unregister(OnPenalty);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            GameEvents.GameplayEvents.ComparisonItemShown.Unregister(OnComparisonItem);
            GameEvents.GameplayEvents.LevelCompleted.Unregister(OnLevelCompleted);
            GameEvents.GameplayEvents.LevelFailed.Unregister(OnLevelFailed);
            GameEvents.GameplayEvents.SpinRewardGranted.Unregister(OnSpinReward);
        }

        private void Start()
        {
            // Nothing has asked for a screen yet (splash / home): start with the menu music.
            if (_musicActive != null && !_musicActive.isPlaying)
                PlayMusic(menuMusic != null ? menuMusic : gameplayMusic, 1f);
        }

        // ---------- Screens: music + panel pop ----------

        private void OnScreen(ScreenType type)
        {
            var previous = _screen;
            _screen = type;

            switch (type)
            {
                case ScreenType.SplashScreen:
                case ScreenType.MainMenu:
                    PlayMusic(menuMusic, 1f);
                    break;
                case ScreenType.TargetLook:
                case ScreenType.Gameplay:
                case ScreenType.MakeupPanel:
                case ScreenType.ComparisonPanel:
                    PlayMusic(gameplayMusic, 1f);
                    break;
                case ScreenType.PausePanel:
                case ScreenType.LevelCompletePanel:
                case ScreenType.LevelFailedPanel:
                    SetMusicLevel(duckedMusic);
                    break;
                // Settings / Loading: keep whatever is playing.
            }

            if (previous != type && IsPopupPanel(type))
                Play(panelOpen);
        }

        private static bool IsPopupPanel(ScreenType type) =>
            type is ScreenType.LevelCompletePanel or ScreenType.LevelFailedPanel or ScreenType.PausePanel
                or ScreenType.Settings or ScreenType.TargetLook
                or ScreenType.NoInternetView or ScreenType.PrivacyPolicy;

        private void PlayMusic(AudioClip clip, float level)
        {
            if (_musicActive == null)
                return;
            _musicLevel = level;
            if (clip == null)
            {
                FadeTo(null);
                return;
            }
            if (_musicActive.clip == clip && _musicActive.isPlaying)
            {
                FadeTo(_musicActive);
                return;
            }

            var next = _musicActive == musicSource ? _musicB : musicSource;
            next.clip = clip;
            next.volume = 0f;
            next.mute = _musicMuted;
            next.Play();
            FadeTo(next);
        }

        private void SetMusicLevel(float level)
        {
            _musicLevel = level;
            if (_musicActive != null && _musicActive.isPlaying)
                FadeTo(_musicActive);
        }

        // Fades 'target' to the current level and every other music source out. Unscaled: menus pause the game.
        private void FadeTo(AudioSource target)
        {
            if (_musicFade != null)
                StopCoroutine(_musicFade);
            if (target != null)
                _musicActive = target;
            _musicFade = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(AudioSource target)
        {
            var other = target == musicSource ? _musicB : musicSource;
            if (target == null)
                other = _musicActive;
            var goal = musicVolume * _musicLevel;
            var fromTarget = target != null ? target.volume : 0f;
            var fromOther = other != null ? other.volume : 0f;

            for (var t = 0f; t < musicFade; t += Time.unscaledDeltaTime)
            {
                var k = t / musicFade;
                if (target != null) target.volume = Mathf.Lerp(fromTarget, goal, k);
                if (other != null && other != target) other.volume = Mathf.Lerp(fromOther, 0f, k);
                yield return null;
            }

            if (target != null) target.volume = goal;
            if (other != null && other != target)
            {
                other.volume = 0f;
                other.Stop();
            }
            _musicFade = null;
        }

        // ---------- Run ----------

        private void OnLevelLoaded(LevelData level, int number)
        {
            _inRun = false;
            _lastCoins = 0;
        }

        private void OnRunStarted() => _inRun = true;

        private void OnCoinsChanged(int total)
        {
            var gained = total > _lastCoins;
            _lastCoins = total;
            // Many coins in one frame (a row) = one sound.
            if (!gained || Time.unscaledTime - _lastCoinTime < 0.04f)
                return;
            _lastCoinTime = Time.unscaledTime;
            Play(coinPickup);
        }

        // Starting outfits are put on before the run starts; only gates during the run make a sound.
        private void OnItemEquipped(OutfitItem item)
        {
            if (_inRun)
                Play(gatePickup);
        }

        private void OnPenalty(int pointsLost)
        {
            Play(penalty);
            GameSettings.Vibrate();
        }

        // ---------- Finish ----------

        private void OnComparisonStarted(OutfitMatch player, OutfitMatch ai)
        {
            _inRun = false;
            Play(comparisonStart);
        }

        private void OnComparisonItem(ComparisonStep step) => Play(step.PlayerMatched ? comparisonMatch : comparisonMiss);

        private void OnLevelCompleted(LevelResult result) => Play(levelComplete);

        private void OnLevelFailed(LevelResult result) => Play(levelFailed);

        private void OnSpinReward(int coins) => Play(coinReward);

        private void PlayCue(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.PanelOpen: Play(panelOpen); break;
                case SoundCue.Confetti: Play(confetti); break;
                case SoundCue.OutfitUnlocked: Play(outfitUnlocked); break;
                case SoundCue.CoinReward: Play(coinReward); break;
                case SoundCue.Jump: Play(jump); break;
                case SoundCue.Slide: Play(slide); break;
            }
        }

        // ---------- Playback ----------

        private void PlayButton() => Play(buttonClick);

        /// <summary>Raw clip from PlaySfx (hurdle hit sounds etc.).</summary>
        private void Play(AudioClip clip)
        {
            if (clip != null && effectsSource != null && !effectsSource.mute)
                effectsSource.PlayOneShot(clip);
        }

        private void Play(Sfx sfx)
        {
            if (sfx == null || sfx.clip == null || effectsSource == null || effectsSource.mute)
                return;
            if (sfx.pitchJitter > 0f)
            {
                // PlayOneShot uses the source's pitch: give jittered sounds their own short-lived source.
                var src = gameObject.AddComponent<AudioSource>();
                src.outputAudioMixerGroup = effectsSource.outputAudioMixerGroup;
                src.playOnAwake = false;
                src.pitch = 1f + UnityEngine.Random.Range(-sfx.pitchJitter, sfx.pitchJitter);
                src.PlayOneShot(sfx.clip, sfx.volume);
                Destroy(src, sfx.clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
                return;
            }
            effectsSource.PlayOneShot(sfx.clip, sfx.volume);
        }

        private void SetMusicVolume(float volume)
        {
            _musicMuted = volume <= 0f;
            if (musicSource != null) musicSource.mute = _musicMuted;
            if (_musicB != null) _musicB.mute = _musicMuted;
        }

        private void SetSoundVolume(float volume)
        {
            if (effectsSource == null)
                return;

            effectsSource.mute = volume <= 0f;
            if (effectsSource.mute)
                effectsSource.Stop();
        }
    }
}
