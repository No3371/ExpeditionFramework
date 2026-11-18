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

        private const int DiceHeard = 4;
        private const float DiceGap = 0.14f;

        private static readonly Dictionary<AudioClip, float> ShotLengths = new Dictionary<AudioClip, float>();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        // The framework's own Sounds/dice file, registered by the game under the framework's mod id.
        private static readonly SoundSpec Dice = new SoundSpec
        {
            Sounds = { new AudioEntry { Audio = "expedition_framework:dice" } },
            VolumeMin = 0.45, VolumeMax = 0.6, PitchMin = 0.92, PitchMax = 1.1,
        };

        // A sound that is kept track of after it starts: it has a channel to stop it by, something it waits for, a loop or a fade.
        private sealed class Voice
        {
            public SoundSpec Spec;
            public GameObject Host;
            public AudioSource Source;
            public float Full;
            public float Level;
            public bool Stopping;
        }

        private readonly List<(SoundSpec Spec, float At)> queued = new List<(SoundSpec, float)>();
        private readonly List<Voice> voices = new List<Voice>();

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
            for (int i = queued.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < queued[i].At) continue;
                SoundSpec spec = queued[i].Spec;
                queued.RemoveAt(i);
                PlayOne(spec);
            }
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                bool gone = voice.Source == null;
                if (!gone)
                {
                    float fade = (float)voice.Spec.Fade;
                    float target = voice.Stopping ? 0f : 1f;
                    voice.Level = fade > 0f ? Mathf.MoveTowards(voice.Level, target, Time.unscaledDeltaTime / fade) : target;
                    voice.Source.volume = voice.Full * voice.Level;
                    // A paused listener reports nothing as playing, which is not the sound having run out.
                    bool ranOut = !voice.Source.loop && !voice.Source.isPlaying && !AudioListener.pause;
                    gone = ranOut || (voice.Stopping && voice.Level <= 0f);
                    if (gone) Object.Destroy(voice.Host);
                }
                if (gone) voices.RemoveAt(i);
            }
        }

        public void Clear()
        {
            later.Clear();
            queued.Clear();
            foreach (Voice voice in voices) if (voice.Host != null) Object.Destroy(voice.Host);
            voices.Clear();
        }

        public void Stop(string channel)
        {
            queued.RemoveAll(q => q.Spec.Channel == channel);
            foreach (Voice voice in voices) if (voice.Spec.Channel == channel) voice.Stopping = true;
        }

        public void End(string until)
        {
            queued.RemoveAll(q => q.Spec.Until == until);
            foreach (Voice voice in voices) if (voice.Spec.Until == until) voice.Stopping = true;
        }

        public void Play(SoundSpec spec)
        {
            for (int i = 0; i < spec.Repeat; i++) Queue(spec, (float)(spec.Delay + i * spec.Gap));
        }

        // One roll per die, a beat apart. A handful is as many as the ear tells apart, so a big pool isn't a long rattle.
        public void PlayDice(int count)
        {
            for (int i = 0; i < Mathf.Min(count, DiceHeard); i++) Queue(Dice, i * DiceGap);
        }

        private void Queue(SoundSpec spec, float wait)
        {
            if (wait <= 0f) PlayOne(spec);
            else queued.Add((spec, Time.unscaledTime + wait));
        }

        // Each play gets a source of its own: pitch belongs to the source, and the game's shared channels would cut one
        // play with the next.
        private void PlayOne(SoundSpec spec)
        {
            AudioEntry entry = spec.Sounds[Random.Range(0, spec.Sounds.Count)];
            if (Random.value * 100f < entry.SkipChance) return;
            AudioDirectory directory = AudioManager.Instance.audioDirectory;
            if (!directory.HasAudioClip(entry.Audio))
            {
                if (Reported.Add(entry.Audio)) ModEntry.Log.Warning("Sound: no sound is registered as \"" + entry.Audio + "\"");
                return;
            }
            AudioClip clip = directory.GetAudioClip(entry.Audio);
            AudioSource like = AudioManager.Instance.audioSource2;
            var host = new GameObject("ExpeditionFramework.Sound");
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = like.outputAudioMixerGroup;
            float full = like.volume * Random.Range((float)spec.VolumeMin, (float)spec.VolumeMax);
            source.pitch = Random.Range((float)spec.PitchMin, (float)spec.PitchMax);
            source.clip = clip;
            if (!spec.Held)
            {
                source.volume = full;
                source.Play();
                Object.Destroy(host, clip.length / source.pitch + 0.2f);
                return;
            }
            // One sound per channel: the newcomer takes over as the old one fades.
            if (spec.Channel != null) foreach (Voice old in voices) if (old.Spec.Channel == spec.Channel) old.Stopping = true;
            var voice = new Voice { Spec = spec, Host = host, Source = source, Full = full, Level = spec.Fade > 0 ? 0f : 1f };
            source.loop = spec.Loop;
            source.volume = full * voice.Level;
            source.Play();
            voices.Add(voice);
        }

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
