using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    public enum AudioBus { Auto, Effects, Ambience, Interface }

    /// <summary>Code-owned bus gains, including dynamically created sources. No mixer asset is required.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class AudioMix : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource Source;
            public AudioBus Bus;
            public float Volume;
            public float Output = -1f;
        }
        static readonly List<Voice> voices = new List<Voice>();
        static readonly Dictionary<AudioSource, Voice> bySource = new Dictionary<AudioSource, Voice>();
        public static int VoiceCount { get { return voices.Count; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { voices.Clear(); bySource.Clear(); }

        public static void Register(AudioSource source, AudioBus bus, float volume)
        {
            Voice voice = new Voice { Source = source, Bus = bus, Volume = Mathf.Clamp01(volume) };
            voices.Add(voice);
            bySource[source] = voice;
            Apply(voice);
        }

        public static float Volume(AudioSource source)
        {
            if (source == null) return 0f;
            Voice voice;
            return bySource.TryGetValue(source, out voice) ? voice.Volume : source.volume;
        }

        public static void SetVolume(AudioSource source, float volume)
        {
            if (source == null) return;
            Voice voice;
            if (!bySource.TryGetValue(source, out voice)) { source.volume = Mathf.Clamp01(volume); return; }
            voice.Volume = Mathf.Clamp01(volume);
            Apply(voice);
        }

        static void Apply(Voice voice)
        {
            float gain = voice.Bus == AudioBus.Ambience ? GameSettings.AmbienceVolume :
                         voice.Bus == AudioBus.Interface ? GameSettings.InterfaceVolume : GameSettings.EffectsVolume;
            float output = voice.Volume * Mathf.Clamp01(gain);
            if (Mathf.Abs(output - voice.Output) < 0.000001f) return;
            voice.Output = output;
            voice.Source.volume = output;
        }

        public static void ApplyAll()
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice voice = voices[i];
                if (voice.Source == null)
                {
                    bySource.Remove(voice.Source);
                    voices.RemoveAt(i);
                }
                else Apply(voice);
            }
        }

        void LateUpdate() { ApplyAll(); }

        public static void AddTunnelZone(Transform parent)
        {
            GameObject go = new GameObject("TunnelReverb");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(34 * CityMap.Cell, 2f, 41 * CityMap.Cell);
            AudioReverbZone zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.StoneCorridor;
            zone.minDistance = 5f;
            zone.maxDistance = 20f;
        }
    }
}
