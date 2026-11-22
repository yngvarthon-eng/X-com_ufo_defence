using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProceduralAudioCuePlayer : MonoBehaviour
{
    private const int SampleRate = 44100;
    private const string SfxVolumePrefKey = "xcon.audio.sfxVolume";

    [Header("Curated Clips (Optional)")]
    [SerializeField] private AudioClip ufoDetectedClip;
    [SerializeField] private AudioClip responseAcceptedClip;
    [SerializeField] private AudioClip responseExpiredClip;
    [SerializeField] private AudioClip expireStreakWarningClip;
    [SerializeField] private AudioClip moodShiftMysteriousClip;
    [SerializeField] private AudioClip moodShiftMilitaryClip;
    [SerializeField] private AudioClip missionVictoryClip;
    [SerializeField] private AudioClip missionFailureClip;
    [SerializeField] private AudioClip medalBronzeClip;
    [SerializeField] private AudioClip medalSilverClip;
    [SerializeField] private AudioClip medalGoldClip;
    [SerializeField] private AudioClip medalNoneClip;

    [Header("SFX")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultSfxVolume = 0.22f;

    private readonly Dictionary<string, AudioClip> clipCache = new();
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        var stored = PlayerPrefs.GetFloat(SfxVolumePrefKey, defaultSfxVolume);
        SetSfxVolume(stored, persist: false);
    }

    private void OnValidate()
    {
        defaultSfxVolume = Mathf.Clamp01(defaultSfxVolume);
    }

    public void PlayUfoDetected()
    {
        if (PlayCuratedClip(ufoDetectedClip, 1f))
        {
            return;
        }

        PlayOneShot("ufo.detected", 720f, 0.14f, 0.24f, Wave.Sine);
    }

    public void PlayResponseAccepted()
    {
        if (PlayCuratedClip(responseAcceptedClip, 1f))
        {
            return;
        }

        PlayOneShot("response.accepted", 860f, 0.09f, 0.28f, Wave.Sine);
    }

    public void PlayResponseExpired()
    {
        if (PlayCuratedClip(responseExpiredClip, 1f))
        {
            return;
        }

        PlayOneShot("response.expired", 230f, 0.18f, 0.3f, Wave.Square);
    }

    public void PlayExpireStreakWarning()
    {
        if (PlayCuratedClip(expireStreakWarningClip, 1f))
        {
            return;
        }

        StartCoroutine(PlaySequence(new[] { 760f, 640f }, 0.1f, 0.08f, 0.24f));
    }

    public void PlayMoodShiftMysterious()
    {
        if (PlayCuratedClip(moodShiftMysteriousClip, 1f))
        {
            return;
        }

        StartCoroutine(PlaySequence(new[] { 560f, 520f }, 0.06f, 0.06f, 0.1f));
    }

    public void PlayMoodShiftMilitary()
    {
        if (PlayCuratedClip(moodShiftMilitaryClip, 1f))
        {
            return;
        }

        StartCoroutine(PlaySequence(new[] { 540f, 620f }, 0.06f, 0.05f, 0.11f));
    }

    public void PlayMissionVictory()
    {
        if (PlayCuratedClip(missionVictoryClip, 1f))
        {
            return;
        }

        StartCoroutine(PlaySequence(new[] { 523.25f, 659.25f, 783.99f }, 0.1f, 0.08f, 0.28f));
    }

    public void PlayMissionFailure()
    {
        if (PlayCuratedClip(missionFailureClip, 1f))
        {
            return;
        }

        StartCoroutine(PlaySequence(new[] { 320f, 260f, 210f }, 0.12f, 0.09f, 0.3f));
    }

    public void PlayMedalAward(int medalTier)
    {
        switch (medalTier)
        {
            case 3:
                if (PlayCuratedClip(medalGoldClip, 1f))
                {
                    return;
                }
                StartCoroutine(PlaySequence(new[] { 740f, 932f, 1175f }, 0.08f, 0.06f, 0.25f));
                break;
            case 2:
                if (PlayCuratedClip(medalSilverClip, 1f))
                {
                    return;
                }
                StartCoroutine(PlaySequence(new[] { 660f, 830f }, 0.08f, 0.06f, 0.24f));
                break;
            case 1:
                if (PlayCuratedClip(medalBronzeClip, 1f))
                {
                    return;
                }
                StartCoroutine(PlaySequence(new[] { 580f, 690f }, 0.07f, 0.05f, 0.2f));
                break;
            default:
                if (PlayCuratedClip(medalNoneClip, 1f))
                {
                    return;
                }
                PlayOneShot("medal.none", 430f, 0.08f, 0.18f, Wave.Sine);
                break;
        }
    }

    public void SetSfxVolume(float volume, bool persist = true)
    {
        var clamped = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = clamped;
        }

        if (!persist)
        {
            return;
        }

        PlayerPrefs.SetFloat(SfxVolumePrefKey, clamped);
        PlayerPrefs.Save();
    }

    public float GetSfxVolume()
    {
        if (audioSource != null)
        {
            return audioSource.volume;
        }

        return PlayerPrefs.GetFloat(SfxVolumePrefKey, defaultSfxVolume);
    }

    private bool PlayCuratedClip(AudioClip clip, float volumeScale)
    {
        if (audioSource == null || clip == null)
        {
            return false;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        return true;
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
