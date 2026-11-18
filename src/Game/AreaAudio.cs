using System;
using System.Collections.Generic;
using ExpeditionFramework.Api;
using ExpeditionFramework.Core;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

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
            // 0 silent to 1 full: where the rail's fade stands.
            public float Level;
            public bool Wanted;
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
                var playing = new Playing { Rail = rail, Source = source, Wanted = rail.When == null, Level = rail.Fade > 0 ? 0f : 1f };
                source.volume = Volume(rail) * playing.Level;
                rails.Add(playing);
                if (playing.Wanted) Begin(playing);
            }
        }

        /// <summary>Asks each conditional rail whether it should be playing. Called when the run begins and after each player action.</summary>
        public void Recheck(Func<Condition, bool> holds)
        {
            foreach (Playing playing in rails)
            {
                if (playing.Rail.When == null || playing.Source == null) continue;
                bool now = holds(playing.Rail.When);
                if (now && !playing.Wanted) Begin(playing);
                playing.Wanted = now;
            }
        }

        public void Tick()
        {
            foreach (Playing playing in rails)
            {
                // The host goes with its scene when the player quits to the menu mid-run.
                if (playing.Source == null) continue;
                AudioRail rail = playing.Rail;
                float target = playing.Wanted ? 1f : 0f;
                playing.Level = rail.Fade > 0 ? Mathf.MoveTowards(playing.Level, target, Time.unscaledDeltaTime / (float)rail.Fade) : target;
                playing.Source.volume = Volume(rail) * playing.Level;
                if (!playing.Wanted)
                {
                    if (playing.Level <= 0f && playing.Source.isPlaying) playing.Source.Stop();
                    continue;
                }
                if (!rail.Timed || AudioListener.pause) continue;
                playing.Wait -= Time.unscaledDeltaTime;
                if (playing.Wait > 0f) continue;
                playing.Wait = NextWait(rail);
                AudioClip clip = Pick(rail);
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

        private static void Begin(Playing playing)
        {
            AudioRail rail = playing.Rail;
            if (rail.Timed)
            {
                playing.Wait = NextWait(rail);
                return;
            }
            // Wanted again while its loop is still fading out: let it swell back rather than restart.
            if (playing.Source.isPlaying && playing.Source.loop) return;
            AudioClip clip = Pick(rail);
            if (clip == null) return;
            playing.Source.clip = clip;
            playing.Source.loop = !rail.Once;
            playing.Source.time = Mathf.Min((float)rail.Start * clip.length, Mathf.Max(0f, clip.length - 0.05f));
            playing.Source.Play();
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
