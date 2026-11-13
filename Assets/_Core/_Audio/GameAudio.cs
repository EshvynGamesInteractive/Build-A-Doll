using Core._GameEvent;
using Game.Level;
using UnityEngine;

namespace _Core._Audio
{
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource effectsSource;
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip levelComplete;
        [SerializeField] private AudioClip levelFailed;

        private void OnEnable()
        {
            SetMusicVolume(GameSettings.IsEnabled(GameConstants.MusicKey) ? 1f : 0f);
            SetSoundVolume(GameSettings.IsEnabled(GameConstants.SoundKey) ? 1f : 0f);

            GameEvents.AudioEvents.PlaySfx.Register(Play);
            GameEvents.AudioEvents.ButtonClicked.Register(PlayButton);
            GameEvents.AudioEvents.SetMusicVolume.Register(SetMusicVolume);
            GameEvents.AudioEvents.SetSoundVolume.Register(SetSoundVolume);
            GameEvents.GameplayEvents.LevelCompleted.Register(PlayComplete);
            GameEvents.GameplayEvents.LevelFailed.Register(PlayFailed);
            GameEvents.GameplayEvents.PenaltyTaken.Register(OnPenalty);
        }

        private void OnDisable()
        {
            GameEvents.AudioEvents.PlaySfx.Unregister(Play);
            GameEvents.AudioEvents.ButtonClicked.Unregister(PlayButton);
            GameEvents.AudioEvents.SetMusicVolume.Unregister(SetMusicVolume);
            GameEvents.AudioEvents.SetSoundVolume.Unregister(SetSoundVolume);
            GameEvents.GameplayEvents.LevelCompleted.Unregister(PlayComplete);
            GameEvents.GameplayEvents.LevelFailed.Unregister(PlayFailed);
            GameEvents.GameplayEvents.PenaltyTaken.Unregister(OnPenalty);
        }

        private void Start()
        {
            if (musicSource != null && musicSource.clip != null)
                musicSource.Play();
        }

        private void PlayButton() => Play(buttonClick);
        private void PlayComplete(LevelResult result) => Play(levelComplete);
        private void PlayFailed(LevelResult result) => Play(levelFailed);
        private void OnPenalty(int pointsLost) => GameSettings.Vibrate();

        private void Play(AudioClip clip)
        {
            if (clip != null && effectsSource != null && !effectsSource.mute)
                effectsSource.PlayOneShot(clip);
        }

        private void SetMusicVolume(float volume)
        {
            if (musicSource != null)
                musicSource.mute = volume <= 0f;
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
