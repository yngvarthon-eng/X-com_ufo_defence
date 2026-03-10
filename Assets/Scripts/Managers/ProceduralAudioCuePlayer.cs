using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProceduralAudioCuePlayer : MonoBehaviour
{
    private const int SampleRate = 44100;

    private readonly Dictionary<string, AudioClip> clipCache = new();
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.22f;
    }

    public void PlayUfoDetected()
    {
        PlayOneShot("ufo.detected", 720f, 0.14f, 0.24f, Wave.Sine);
    }

    public void PlayResponseAccepted()
    {
        PlayOneShot("response.accepted", 860f, 0.09f, 0.28f, Wave.Sine);
    }

    public void PlayResponseExpired()
    {
        PlayOneShot("response.expired", 230f, 0.18f, 0.3f, Wave.Square);
    }

    public void PlayMissionVictory()
    {
        StartCoroutine(PlaySequence(new[] { 523.25f, 659.25f, 783.99f }, 0.1f, 0.08f, 0.28f));
    }

    public void PlayMissionFailure()
    {
        StartCoroutine(PlaySequence(new[] { 320f, 260f, 210f }, 0.12f, 0.09f, 0.3f));
    }

    public void PlayMedalAward(int medalTier)
    {
        switch (medalTier)
        {
            case 3:
                StartCoroutine(PlaySequence(new[] { 740f, 932f, 1175f }, 0.08f, 0.06f, 0.25f));
                break;
            case 2:
                StartCoroutine(PlaySequence(new[] { 660f, 830f }, 0.08f, 0.06f, 0.24f));
                break;
            case 1:
                StartCoroutine(PlaySequence(new[] { 580f, 690f }, 0.07f, 0.05f, 0.2f));
                break;
            default:
                PlayOneShot("medal.none", 430f, 0.08f, 0.18f, Wave.Sine);
                break;
        }
    }

    private IEnumerator PlaySequence(IReadOnlyList<float> frequencies, float duration, float gapSeconds, float volume)
    {
        if (frequencies == null || frequencies.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < frequencies.Count; i++)
        {
            PlayOneShot($"seq.{frequencies[i]:0.##}.{duration:0.###}", frequencies[i], duration, volume, Wave.Sine);
            if (i < frequencies.Count - 1)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, gapSeconds));
            }
        }
    }

    private enum Wave
    {
        Sine,
        Square,
    }

    private void PlayOneShot(string key, float frequency, float durationSeconds, float volume, Wave wave)
    {
        if (audioSource == null)
        {
            return;
        }

        if (!clipCache.TryGetValue(key, out var clip) || clip == null)
        {
            clip = CreateToneClip(key, frequency, durationSeconds, wave);
            clipCache[key] = clip;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip CreateToneClip(string clipName, float frequency, float durationSeconds, Wave wave)
    {
        var sampleCount = Mathf.Max(32, Mathf.RoundToInt(SampleRate * Mathf.Max(0.02f, durationSeconds)));
        var data = new float[sampleCount];

        var attackSamples = Mathf.Max(1, Mathf.RoundToInt(sampleCount * 0.08f));
        var releaseSamples = Mathf.Max(1, Mathf.RoundToInt(sampleCount * 0.14f));

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)SampleRate;
            var phase = 2f * Mathf.PI * frequency * t;

            float sample;
            switch (wave)
            {
                case Wave.Square:
                    sample = Mathf.Sin(phase) >= 0f ? 0.8f : -0.8f;
                    break;
                default:
                    sample = Mathf.Sin(phase);
                    break;
            }

            float envelope;
            if (i < attackSamples)
            {
                envelope = i / (float)attackSamples;
            }
            else if (i > sampleCount - releaseSamples)
            {
                envelope = (sampleCount - i) / (float)releaseSamples;
            }
            else
            {
                envelope = 1f;
            }

            data[i] = sample * envelope * 0.5f;
        }

        var clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
