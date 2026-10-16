using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class Audio
{
    private static readonly Queue<AudioSource> pool = new();
    private static Transform container;

    private static Transform Container
    {
        get
        {
            if (container != null) return container;
            container = new GameObject("AudioPool").transform;
            Object.DontDestroyOnLoad(container.gameObject);
            return container;
        }
    }

    // 2D
    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f, AudioMixerGroup mixerGroup = null)
    {
        if (clip == null) return;
        var source = Setup(clip, volume, pitch, mixerGroup);
        source.spatialBlend = 0f;
        source.Play();
        Timer.After(clip.length / Mathf.Max(Mathf.Abs(pitch), 0.01f), () => Return(source));
    }

    // 3D
    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, AudioMixerGroup mixerGroup = null)
    {
        if (clip == null) return;
        var source = Setup(clip, volume, pitch, mixerGroup);
        source.transform.position = position;
        source.spatialBlend = 1f;
        source.Play();
        Timer.After(clip.length / Mathf.Max(Mathf.Abs(pitch), 0.01f), () => Return(source));
    }

    private static AudioSource Setup(AudioClip clip, float volume, float pitch, AudioMixerGroup mixerGroup)
    {
        var source = Get();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    private static AudioSource Get()
    {
        if (pool.Count > 0) return pool.Dequeue();
        var go = new GameObject("AudioSource");
        go.transform.SetParent(Container);
        return go.AddComponent<AudioSource>();
    }

    private static void Return(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        pool.Enqueue(source);
    }
}