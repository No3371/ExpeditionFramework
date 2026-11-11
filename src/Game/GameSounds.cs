using System.Collections.Generic;
using ExpeditionFramework.Core;
using UnityEngine;

namespace ExpeditionFramework.Game
{
    /// <summary>Plays what one action asked for, so the sounds of one press don't land on top of each other.</summary>
    internal sealed class GameSounds
    {
        private const float LaterSoundDelay = 0.45f;
        private const float ShotWindow = 0.01f;
        private const float ShotFallback = 0.35f;
        private const float ShotMinClip = 0.5f;

        private static readonly Dictionary<AudioClip, float> ShotLengths = new Dictionary<AudioClip, float>();

        private readonly List<(string Id, float At)> later = new List<(string, float)>();
        private readonly HashSet<string> thisFrame = new HashSet<string>();
        private int frame = -1;
        private float stopShotAt;

        public void Tick()
        {
            if (stopShotAt > 0f && Time.unscaledTime >= stopShotAt)
            {
                stopShotAt = 0f;
                AudioManager.Instance.audioSource3.Stop();
            }
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < later[i].At) continue;
                PlayNow(later[i].Id);
                later.RemoveAt(i);
            }
        }

        public void Clear() => later.Clear();

        public void Play(string id)
        {
            switch (Claim(id))
            {
                case Slot.Now: PlayNow(id); break;
                case Slot.Later: later.Add((id, Time.unscaledTime + LaterSoundDelay)); break;
            }
        }

        public void PlayItem(RunItem item, string fallback)
        {
            string id = !string.IsNullOrEmpty(item.Sound) && AudioManager.Instance.CanPlay(item.Sound) ? item.Sound : fallback;
            if (string.IsNullOrEmpty(id)) return;
            if (!item.HasTag("firearm"))
            {
                Play(id);
                return;
            }
            Claim(id);
            PlayShot(id);
        }

        private enum Slot { Now, Later, Dropped }

        private Slot Claim(string id)
        {
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                thisFrame.Clear();
            }
            if (!thisFrame.Add(id)) return Slot.Dropped;
            return thisFrame.Count == 1 ? Slot.Now : Slot.Later;
        }

        private static void PlayNow(string id)
        {
            if (AudioManager.Instance.CanPlay(id)) AudioManager.Instance.Play2(id);
        }

        // The game's fire clips can hold several shots (they were made for bursts), but one action is one shot. So the
        // clip plays on its own channel, where other sounds can't cut it, and stops after the first shot.
        private void PlayShot(string id)
        {
            if (!AudioManager.Instance.CanPlay(id)) return;
            AudioManager.Instance.Play3(id);
            float length = FirstShotLength(AudioManager.Instance.audioDirectory.GetAudioClip(id));
            stopShotAt = length > 0f ? Time.unscaledTime + length : 0f;
        }

        // Seconds until the second shot in the clip starts, or 0 when the clip holds just one. Reads the samples when
        // the clip allows it, otherwise cuts long clips at a typical single-shot length.
        private static float FirstShotLength(AudioClip clip)
        {
            if (clip == null || clip.length < ShotMinClip) return 0f;
            if (ShotLengths.TryGetValue(clip, out float cached)) return cached;
            float result = ShotFallback;
            var data = new float[clip.samples * clip.channels];
            if (data.Length > 0 && clip.GetData(data, 0))
            {
                int window = Mathf.Max(1, (int)(clip.frequency * ShotWindow)) * clip.channels;
                int frames = data.Length / window;
                var envelope = new float[frames];
                float peak = 0f;
                for (int f = 0; f < frames; f++)
                {
                    float loudest = 0f;
                    for (int s = f * window; s < (f + 1) * window; s++) loudest = Mathf.Max(loudest, Mathf.Abs(data[s]));
                    envelope[f] = loudest;
                    peak = Mathf.Max(peak, loudest);
                }
                // Past the first loud burst, through its decay, to where the next one starts.
                int i = 0;
                while (i < frames && envelope[i] < peak * 0.5f) i++;
                while (i < frames && envelope[i] >= peak * 0.15f) i++;
                while (i < frames && envelope[i] < peak * 0.5f) i++;
                result = i < frames ? i * ShotWindow : 0f;
            }
            ShotLengths[clip] = result;
            return result;
        }
    }
}
