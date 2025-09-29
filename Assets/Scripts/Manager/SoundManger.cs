using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundType
{
    BGM,
    POP,
    MEOW,
    CLICK,
    CLAP,
    COIN,
    CRACK,
    IMPACT,
    WHOOSH,
    DRUM,
    MARIMBA
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager i;

    [Header("Sound Settings")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 1f;

    [Header("Audio Sources")]
    public AudioSource sfxAudioSource;
    public AudioSource bgmAudioSource;

    // 사운드 그룹 테이블
    private Dictionary<SoundType, AudioClip[]> soundDictionary;

    // 인스펙터 확인용 개수
    [System.NonSerialized] public Dictionary<SoundType, int> loadedSoundCounts = new Dictionary<SoundType, int>();

    // 반복 방지 및 과호출 방지
    private Dictionary<SoundType, int> lastPlayedIndex = new Dictionary<SoundType, int>();
    private Dictionary<SoundType, float> lastPlayedTime = new Dictionary<SoundType, float>();

    private void Awake()
    {
        if (i == null)
        {
            i = this;
            DontDestroyOnLoad(gameObject);
            InitializeSoundManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSoundManager()
    {
        // 없으면 자동 생성
        if (sfxAudioSource == null)
        {
            var sfxObject = new GameObject("SFX AudioSource");
            sfxObject.transform.SetParent(transform);
            sfxAudioSource = sfxObject.AddComponent<AudioSource>();
        }

        if (bgmAudioSource == null)
        {
            var bgmObject = new GameObject("BGM AudioSource");
            bgmObject.transform.SetParent(transform);
            bgmAudioSource = bgmObject.AddComponent<AudioSource>();
            bgmAudioSource.loop = true;
        }

        InitializeSoundDictionary();
        LoadSoundsFromResources();
    }

    private void InitializeSoundDictionary()
    {
        soundDictionary = new Dictionary<SoundType, AudioClip[]>();
        loadedSoundCounts = new Dictionary<SoundType, int>();
        lastPlayedIndex = new Dictionary<SoundType, int>();
        lastPlayedTime = new Dictionary<SoundType, float>();
    }

    private void LoadSoundsFromResources()
    {
        foreach (SoundType soundType in Enum.GetValues(typeof(SoundType)))
        {
            string folderPath = $"Sound/{soundType.ToString().ToLower()}";
            AudioClip[] clips = Resources.LoadAll<AudioClip>(folderPath);

            if (clips.Length > 0)
            {
                Array.Sort(clips, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
                soundDictionary[soundType] = clips;
                loadedSoundCounts[soundType] = clips.Length;
                // 필요 시 개발 단계에서만 로그
                // Debug.Log($"Loaded {clips.Length} clips for {soundType}");
            }
            else
            {
                // 폴더가 없어도 조용히 패스 가능
                // Debug.LogWarning($"No clips found for {soundType} in path: {folderPath}");
            }
        }
    }

    // 레벨별 팝/클랩/임팩트 등 선택 호출
    public void PlayBallSound(int level)
    {
        switch (level)
        {
            default: PlaySFX(SoundType.POP, 0); return;
            case 2: PlaySFX(SoundType.POP, 1); return;
            case 3: PlaySFX(SoundType.POP, 2); return;
            case 4: PlaySFX(SoundType.POP, 3); return;
            case 5: PlaySFX(SoundType.CLAP, 0); return;
            case 6: PlaySFX(SoundType.CLAP, 1); return;
            case 7: PlaySFX(SoundType.CLAP, 2); return;
            case 8: PlaySFX(SoundType.IMPACT, 0); return;
        }
    }

    // 간단 호출(랜덤, 볼륨 1)
    public void PlaySFX(SoundType soundType) => PlaySFX(soundType, 1f);

    // 랜덤 + 볼륨 지정 + 과호출/반복 방지
    public void PlaySFX(SoundType soundType, float volume)
    {
        if (sfxAudioSource == null) return;

        float currentTime = Time.time;
        if (lastPlayedTime.TryGetValue(soundType, out float lastTime))
        {
            if (currentTime - lastTime < 0.1f) return; // 100ms 이내 재호출 무시
        }

        if (!soundDictionary.TryGetValue(soundType, out var clips) || clips.Length == 0) return;

        int randomIndex;
        if (clips.Length == 1)
        {
            randomIndex = 0;
        }
        else
        {
            int lastIndex = lastPlayedIndex.TryGetValue(soundType, out int idx) ? idx : -1;
            do { randomIndex = UnityEngine.Random.Range(0, clips.Length); } while (randomIndex == lastIndex);
        }

        AudioClip clip = clips[randomIndex];

        lastPlayedIndex[soundType] = randomIndex;
        lastPlayedTime[soundType] = currentTime;

        sfxAudioSource.PlayOneShot(clip, sfxVolume * volume);
    }

    // 특정 인덱스 지정(기본 볼륨 1)
    public void PlaySFX(SoundType soundType, int clipIndex = 0) => PlaySFX(soundType, clipIndex, 1f);

    // 특정 인덱스 + 볼륨
    public void PlaySFX(SoundType soundType, int clipIndex, float volume)
    {
        if (sfxAudioSource == null) return;
        if (!soundDictionary.TryGetValue(soundType, out var clips) || clips.Length == 0) return;
        if (clipIndex < 0 || clipIndex >= clips.Length) return;

        sfxAudioSource.PlayOneShot(clips[clipIndex], sfxVolume * volume);
    }

    // BGM 제어
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmAudioSource == null || clip == null) return;
        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = loop;
        bgmAudioSource.volume = bgmVolume;
        bgmAudioSource.Play();
    }

    public void StopBGM()
    {
        if (bgmAudioSource != null) bgmAudioSource.Stop();
    }

    public void PauseBGM()
    {
        if (bgmAudioSource != null) bgmAudioSource.Pause();
    }

    public void ResumeBGM()
    {
        if (bgmAudioSource != null) bgmAudioSource.UnPause();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmAudioSource != null) bgmAudioSource.volume = bgmVolume;
    }

    // 유틸
    private AudioClip GetSoundClip(SoundType soundType, int clipIndex)
    {
        if (soundDictionary.TryGetValue(soundType, out var clips))
        {
            if (clipIndex >= 0 && clipIndex < clips.Length) return clips[clipIndex];
        }
        return null;
    }

    public int GetClipCount(SoundType soundType)
        => soundDictionary.TryGetValue(soundType, out var clips) ? clips.Length : 0;

    public AudioClip[] GetAllClips(SoundType soundType)
        => soundDictionary.TryGetValue(soundType, out var clips) ? clips : Array.Empty<AudioClip>();
}
