using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    MARIMBA,
    Fruit_High,
    Fruit_Mid,
    Fruit_Low,
}

public enum SoundSet
{
    BGM,
    SFX,
    Vib
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager i;

    [Header("Sound Settings")]
    public bool isSFXOn = true;
    public bool isBGMOn = true;
    public bool isVibOn = true;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 1f;

    public Image Img_BGM;
    public Image Img_SFX;
    public Image Img_Vib;
    
    private const string KEY_SFX_ON   = "SFX_ON";
    private const string KEY_BGM_ON   = "BGM_ON";
    private const string KEY_VIB_ON   = "VIB_ON";
    private const string KEY_SFX_VOL  = "SFX_VOL";
    private const string KEY_BGM_VOL  = "BGM_VOL";

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
        string[] categories = { "Fruit" }; 
        // 💡 필요한 그룹명은 여기에 자유롭게 추가

        foreach (SoundType soundType in Enum.GetValues(typeof(SoundType)))
        {
            string enumName = soundType.ToString(); // 예: "Fruit_High"
            string folderPath = $"Sound/{enumName.ToLower()}"; // 기본 경로

            // ✅ 다중 그룹 접두사 처리
            foreach (var category in categories)
            {
                if (enumName.StartsWith(category + "_", StringComparison.OrdinalIgnoreCase))
                {
                    string subFolder = enumName.Substring(category.Length + 1).ToLower(); // "_" 이후 이름 추출
                    folderPath = $"Sound/{category}/{subFolder}";
                    break;
                }
            }

            AudioClip[] clips = Resources.LoadAll<AudioClip>(folderPath);

            if (clips.Length > 0)
            {
                Array.Sort(clips, (x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
                soundDictionary[soundType] = clips;
                loadedSoundCounts[soundType] = clips.Length;
                // Debug.Log($"Loaded {clips.Length} clips for {soundType} from {folderPath}");
            }
            else
            {
                // Debug.LogWarning($"No clips found for {soundType} in path: {folderPath}");
            }
        }
    }

    // 레벨별 팝/클랩/임팩트 등 선택 호출
    public void PlayBallSound(int level)
    {
        switch (level)
        {
            default: PlaySFX(SoundType.POP); return;
            case 2: PlaySFX(SoundType.POP); return;
            case 3: PlaySFX(SoundType.POP); return;
            case 4: PlaySFX(SoundType.Fruit_High); return;
            case 5: PlaySFX(SoundType.Fruit_High); return;
            case 6: PlaySFX(SoundType.Fruit_Mid); return;
            case 7: PlaySFX(SoundType.Fruit_Mid); return;
            case 8: PlaySFX(SoundType.Fruit_Mid); return;
            case 9: PlaySFX(SoundType.Fruit_Low); return;
            case 10: 
                PlaySFX(SoundType.Fruit_Low);
                PlaySFX(SoundType.IMPACT);
                return;
            case 11: 
                PlaySFX(SoundType.Fruit_Low); 
                PlaySFX(SoundType.IMPACT);
                return;
        }
    }

    // 간단 호출(랜덤, 볼륨 1)
    public void PlaySFX(SoundType soundType) => PlaySFX(soundType, 1f);

    // 랜덤 + 볼륨 지정 + 과호출/반복 방지
    public void PlaySFX(SoundType soundType, float volume)
    {
        if (isSFXOn == false) return;
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
        if (isSFXOn == false) return;
        if (sfxAudioSource == null) return;
        if (!soundDictionary.TryGetValue(soundType, out var clips) || clips.Length == 0) return;
        if (clipIndex < 0 || clipIndex >= clips.Length) return;

        sfxAudioSource.PlayOneShot(clips[clipIndex], sfxVolume * volume);
    }

    // BGM 제어
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (isBGMOn == false) return;
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
    
    

    public void ToggleAudioSettings(SoundSet soundSet)
    {
        switch (soundSet)
        {
            default:
            case SoundSet.BGM:
                isBGMOn = !isBGMOn;
                break;
            case SoundSet.SFX:
                isSFXOn = !isSFXOn;
                break;
            case SoundSet.Vib:
                isVibOn = !isVibOn;
                break;
        }
        SaveAudioSettings();
        RenewAudioSettings();
    }

    public void RenewAudioSettings()
    {
        if (isBGMOn)
        {
            Debug.Log("BGM ON");
            Img_BGM.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Blue);
        }
        else
        {
            Debug.Log("BGM OFF");
            Img_BGM.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Gray);
        }
        if (isSFXOn)
        {
            Debug.Log("SFX ON");
            Img_SFX.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Blue);
        }
        else
        {
            Debug.Log("SFX OFF");
            Img_SFX.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Gray);
        }
        if (isVibOn)
        {
            Debug.Log("Vib ON");
            Img_Vib.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Blue);
        }
        else
        {
            Debug.Log("Vib OFF");
            Img_Vib.sprite = UISpriteStorage.i.GetUIBtn(UIColor.Gray);
        }
    }
    private void LoadAudioSettings()
    {
        isSFXOn   = PlayerPrefs.GetInt(KEY_SFX_ON, 1) == 1;
        isBGMOn   = PlayerPrefs.GetInt(KEY_BGM_ON, 1) == 1;
        isVibOn   = PlayerPrefs.GetInt(KEY_VIB_ON, 1) == 1;
        sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f);
        bgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 1f);

        // 현재 오디오소스에 볼륨 반영 (플래그에 따른 차단은 네가 별도 처리 예정)
        if (sfxAudioSource != null) sfxAudioSource.volume = sfxVolume;
        if (bgmAudioSource != null) bgmAudioSource.volume = bgmVolume;
    }

    private void SaveAudioSettings()
    {
        PlayerPrefs.SetInt(KEY_SFX_ON, isSFXOn == true ? 1 : 0);
        PlayerPrefs.SetInt(KEY_BGM_ON, isBGMOn == true ? 1 : 0);
        PlayerPrefs.SetInt(KEY_VIB_ON, isVibOn == true ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_SFX_VOL, Mathf.Clamp01(sfxVolume));
        PlayerPrefs.SetFloat(KEY_BGM_VOL, Mathf.Clamp01(bgmVolume));
        PlayerPrefs.Save();
    }
}
