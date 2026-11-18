using System.Collections.Generic;
using ExpeditionFramework.Core;
using UnityEngine;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Plays an area's audio rails for one visit. Each rail has its own source, so loops and timed sounds overlap instead
    /// of cutting each other the way the game's shared channels do.
    /// </summary>
    internal sealed class AreaAudio
    {
        private sealed class Playing
        {
            public AudioRail Rail;
            public AudioSource Source;
            public float Wait;
        }

        private static readonly HashSet<string> Reported = new HashSet<string>();

        private readonly AreaDef area;
        private readonly List<Playing> rails = new List<Playing>();
        private GameObject host;

        public AreaAudio(AreaDef area) => this.area = area;

        public void Start()
        {
            Stop();
            if (area.Audio.Count == 0) return;
            host = new GameObject("ExpeditionFramework.Audio." + area.Id);
            foreach (AudioRail rail in area.Audio)
            {
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                AudioSource slider = Slider(rail);
                if (slider != null) source.outputAudioMixerGroup = slider.outputAudioMixerGroup;
                source.volume = Volume(rail);
                var playing = new Playing { Rail = rail, Source = source };
                rails.Add(playing);
                if (rail.Timed)
                {
                    playing.Wait = NextWait(rail);
                    continue;
                }
                AudioClip clip = Pick(rail);
                if (clip == null) continue;
                source.clip = clip;
                source.loop = true;
                source.Play();
            }
        }

        public void Tick()
        {
            foreach (Playing playing in rails)
            {
                // The host goes with its scene when the player quits to the menu mid-run.
                if (playing.Source == null) continue;
                playing.Source.volume = Volume(playing.Rail);
                if (!playing.Rail.Timed || AudioListener.pause) continue;
                playing.Wait -= Time.unscaledDeltaTime;
                if (playing.Wait > 0f) continue;
                playing.Wait = NextWait(playing.Rail);
                AudioClip clip = Pick(playing.Rail);
                // One-shots mix on the source, so a long sound isn't cut when the rail fires again.
                if (clip != null) playing.Source.PlayOneShot(clip);
            }
        }

        public void Stop()
        {
            rails.Clear();
            if (host != null) Object.Destroy(host);
            host = null;
        }

        private static float NextWait(AudioRail rail) => Random.Range((float)rail.EveryMin, (float)rail.EveryMax);

        // Unity's own random numbers, never the run's dice: what plays must not change how a run rolls.
        private static AudioClip Pick(AudioRail rail)
        {
            AudioEntry entry = rail.Sounds[Random.Range(0, rail.Sounds.Count)];
            if (Random.value * 100f < entry.SkipChance) return null;
            AudioDirectory directory = AudioManager.Instance.audioDirectory;
            if (directory.HasAudioClip(entry.Audio)) return directory.GetAudioClip(entry.Audio);
            if (Reported.Add(entry.Audio)) ModEntry.Log.Warning("Area audio: no sound is registered as \"" + entry.Audio + "\"");
            return null;
        }

        private static float Volume(AudioRail rail)
        {
            AudioSource slider = Slider(rail);
            return (float)rail.Volume * (slider != null ? slider.volume : 1f);
        }

        // The game keeps its music and background volume settings on these two sources, so following them follows the options menu.
        private static AudioSource Slider(AudioRail rail)
        {
            StoreSoundPlayer player = StoreSoundPlayer.Instance;
            if (player == null) return null;
            switch (rail.Type)
            {
                case "music": return player.musicAudioSource;
                case "ambience": return player.ambianceAudioSource;
                default: return null;
            }
        }
    }
}
