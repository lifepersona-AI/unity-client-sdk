using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace LP
{
    /// <summary>
    /// Handles playback of PCM audio received as Base64-encoded strings.
    /// Uses an internal queue to play audio clips sequentially through an AudioSource.
    /// </summary>
    public class PcmAudioPlayer : MonoBehaviour
    {
        public event Action OnAgentStartedSpeaking;
        public event Action OnAgentStoppedSpeaking;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private int sampleRate = 16000;

        private readonly Queue<AudioClip> _clipQueue = new();
        private bool _wasPlayingLastFrame = false;

        #region Unity Lifecycle

        private void Start()
        {
            Assert.IsNotNull(audioSource, "Audio source is required");

            Debug.Log($"[PcmAudioPlayer] Initialized - AudioSource: {audioSource.name}, Volume: {audioSource.volume}, Mute: {audioSource.mute}, Sample Rate: {sampleRate}");
        }

        private void Update()
        {
            bool isPlaying = audioSource.isPlaying || _clipQueue.Count > 0;

            // Detect state changes and fire events
            if (isPlaying && !_wasPlayingLastFrame)
            {
                OnAgentStartedSpeaking?.Invoke();
                Debug.Log("[PcmAudioPlayer] Agent started speaking - mic will be muted");
            }
            else if (!isPlaying && _wasPlayingLastFrame)
            {
                OnAgentStoppedSpeaking?.Invoke();
                Debug.Log("[PcmAudioPlayer] Agent stopped speaking - mic will be resumed");
            }

            _wasPlayingLastFrame = isPlaying;

            // Play next clip if available
            if (audioSource.isPlaying || _clipQueue.Count == 0)
                return;

            var clip = _clipQueue.Dequeue();
            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log($"[PcmAudioPlayer] Playing audio clip: {clip.length:F2}s, {clip.samples} samples, queue: {_clipQueue.Count}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Converts Base64-encoded PCM audio into a Unity AudioClip
        /// and adds it to the playback queue.
        /// </summary>
        /// <param name="base64Audio">Base64-encoded PCM16 audio data.</param>
        public void EnqueueBase64Audio(string base64Audio)
        {
            try
            {
                // Decode Base64 string into raw bytes
                var bytes = System.Convert.FromBase64String(base64Audio);

                // Each PCM16 sample uses 2 bytes
                var sampleCount = bytes.Length / 2;
                var samples = new float[sampleCount];

                for (var i = 0; i < sampleCount; i++)
                {
                    var sample = (short)((bytes[i * 2 + 1] << 8) | bytes[i * 2]);
                    samples[i] = sample / 32768f;
                }

                var clip = AudioClip.Create("AIConversationalClip", sampleCount, 1, sampleRate, false);
                clip.SetData(samples, 0);

                _clipQueue.Enqueue(clip);

                Debug.Log($"[PcmAudioPlayer] Enqueued audio: {bytes.Length} bytes, {sampleCount} samples, {clip.length:F2}s, queue size: {_clipQueue.Count}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PcmAudioPlayer] Failed to enqueue audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops playback immediately and clears any queued audio.
        /// </summary>
        public void StopImmediately()
        {
            _clipQueue.Clear();
            audioSource.Stop();
        }

        #endregion
    }
}
