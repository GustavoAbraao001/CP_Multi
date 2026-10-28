using UnityEngine;

/// <summary>
/// Creates lightweight procedural background music and checkpoint feedback.
/// Sounds are local to the player who triggered the checkpoint.
/// </summary>
public sealed class FluxCircuitAudio : MonoBehaviour
{
    private const int SampleRate = 22050;
    private static FluxCircuitAudio _instance;

    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.14f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.55f;

    private AudioSource _musicSource;
    private AudioSource _effectsSource;
    private AudioClip _correctClip;
    private AudioClip _wrongClip;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.volume = musicVolume;
        _musicSource.clip = CreateAmbientLoop();
        _musicSource.Play();

        _effectsSource = gameObject.AddComponent<AudioSource>();
        _effectsSource.playOnAwake = false;
        _effectsSource.spatialBlend = 0f;

        _correctClip = CreateSweep("FluxCircuit_Correct", 0.25f, 520f, 980f);
        _wrongClip = CreateSweep("FluxCircuit_Wrong", 0.32f, 360f, 150f);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static void PlayCorrectGate()
    {
        if (_instance != null && _instance._correctClip != null)
            _instance._effectsSource.PlayOneShot(_instance._correctClip, _instance.effectsVolume);
    }

    public static void PlayWrongGate()
    {
        if (_instance != null && _instance._wrongClip != null)
            _instance._effectsSource.PlayOneShot(_instance._wrongClip, _instance.effectsVolume);
    }

    private static AudioClip CreateAmbientLoop()
    {
        const int seconds = 16;
        int sampleCount = SampleRate * seconds;
        var samples = new float[sampleCount];
        float[] notes = { 110f, 130.81f, 164.81f, 146.83f, 98f, 123.47f, 164.81f, 146.83f };

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)SampleRate;
            int noteIndex = Mathf.Min((int)(time / 2f), notes.Length - 1);
            float beatTime = time % 2f;
            float attack = Mathf.Clamp01(beatTime / 0.28f);
            float release = Mathf.Clamp01((2f - beatTime) / 0.42f);
            float envelope = Mathf.Min(attack, release);
            float root = notes[noteIndex];

            float pad = Mathf.Sin(2f * Mathf.PI * root * time) * 0.62f
                        + Mathf.Sin(2f * Mathf.PI * root * 1.5f * time) * 0.24f
                        + Mathf.Sin(2f * Mathf.PI * root * 2f * time) * 0.14f;
            samples[i] = pad * envelope * 0.12f;
        }

        AudioClip clip = AudioClip.Create("FluxCircuit_AmbientLoop", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateSweep(string name, float duration, float startFrequency, float endFrequency)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)(sampleCount - 1);
            float time = i / (float)SampleRate;
            float frequencyDelta = endFrequency - startFrequency;
            float phase = 2f * Mathf.PI * (startFrequency * time + frequencyDelta * time * time / (2f * duration));
            float envelope = Mathf.Sin(Mathf.PI * progress);
            samples[i] = (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f)) * envelope * 0.45f;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
