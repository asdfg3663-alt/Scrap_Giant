using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class AudioRuntime : MonoBehaviour
{
    static readonly string[] KnownGameplayTrackFiles =
    {
        "hetyati-ufo-299350.mp3",
        "jean-paul-v-a-quiet-night-aboard-the-space-shuttle-309567.mp3",
        "ribhavagrawal-hans-zimmer-inspired-space-ambience-part01-no-copyright-495107.mp3"
    };

    enum MusicMode
    {
        None,
        Title,
        Gameplay
    }

    static AudioRuntime instance;

    readonly List<AudioClip> gameplayPlaylist = new();
    readonly List<int> gameplayShuffleOrder = new();

    AudioSource bgmSource;
    AudioSource engineLoopSource;
    AudioSource laserLoopSource;
    AudioSource dragLoopSource;
    AudioSource oneShotSource;

    AudioClip titleBgmClip;
    AudioClip engineLoopClip;
    AudioClip laserLoopClip;
    AudioClip moduleAttachClip;
    AudioClip moduleDragClip;
    AudioClip scrapPickupClip;
    AudioClip moduleBreakClip;
    AudioClip powerPlantExplosionClip;

    MusicMode currentMusicMode;
    int gameplayShuffleCursor;
    float engineActiveUntil;
    float laserActiveUntil;
    bool dragLoopActive;
    float nextMusicScanAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("AudioRuntime");
        instance = go.AddComponent<AudioRuntime>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = CreateSource("BGM", true, false);
        engineLoopSource = CreateSource("EngineLoop", true, false);
        laserLoopSource = CreateSource("LaserLoop", true, false);
        dragLoopSource = CreateSource("DragLoop", true, false);
        oneShotSource = CreateSource("OneShot", false, false);

        moduleAttachClip = CreateAttachThunkClip();
        moduleDragClip = CreateElectricBuzzClip();
        scrapPickupClip = CreateScrapPickupClip();
        moduleBreakClip = CreateModuleBreakClip();
        powerPlantExplosionClip = CreatePowerPlantExplosionClip();
        dragLoopSource.clip = moduleDragClip;

        StartCoroutine(LoadAudioLibrary());
    }

    void Update()
    {
        bgmSource.volume = GameOptions.BgmVolume;
        engineLoopSource.volume = GameOptions.SfxVolume * 0.52f;
        laserLoopSource.volume = GameOptions.SfxVolume * 0.65f;
        dragLoopSource.volume = GameOptions.SfxVolume * 0.45f;

        bool allowGameplayLoops = !GameRuntimeState.GameplayBlocked;
        UpdateLoop(engineLoopSource, engineLoopClip, allowGameplayLoops && Time.unscaledTime <= engineActiveUntil);
        UpdateLoop(laserLoopSource, laserLoopClip, allowGameplayLoops && Time.unscaledTime <= laserActiveUntil);
        UpdateLoop(dragLoopSource, moduleDragClip, allowGameplayLoops && dragLoopActive);

        if (Time.unscaledTime >= nextMusicScanAt)
        {
            nextMusicScanAt = Time.unscaledTime + 0.25f;
            UpdateMusicState();
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void SetEngineLoopActive(bool active)
    {
        EnsureInstance();
        if (instance == null)
            return;

        if (active)
            instance.engineActiveUntil = Mathf.Max(instance.engineActiveUntil, Time.unscaledTime + 0.2f);
        else if (Time.unscaledTime >= instance.engineActiveUntil)
            instance.engineActiveUntil = 0f;
    }

    public static void RequestLaserLoop()
    {
        EnsureInstance();
        if (instance == null)
            return;

        instance.laserActiveUntil = Mathf.Max(instance.laserActiveUntil, Time.unscaledTime + 0.12f);
    }

    public static void BeginModuleDrag()
    {
        EnsureInstance();
        if (instance == null)
            return;

        instance.dragLoopActive = true;
    }

    public static void EndModuleDrag()
    {
        if (instance == null)
            return;

        instance.dragLoopActive = false;
    }

    public static void PlayModuleAttach()
    {
        EnsureInstance();
        if (instance == null || instance.moduleAttachClip == null)
            return;

        instance.oneShotSource.PlayOneShot(instance.moduleAttachClip, GameOptions.SfxVolume);
    }

    public static void PlayScrapPickup()
    {
        EnsureInstance();
        if (instance == null || instance.scrapPickupClip == null)
            return;

        instance.oneShotSource.PlayOneShot(instance.scrapPickupClip, GameOptions.SfxVolume * 0.95f);
    }

    public static void PlayPlayerModuleBreak()
    {
        EnsureInstance();
        if (instance == null || instance.moduleBreakClip == null)
            return;

        instance.oneShotSource.PlayOneShot(instance.moduleBreakClip, GameOptions.SfxVolume);
    }

    public static void PlayPowerPlantExplosion()
    {
        EnsureInstance();
        if (instance == null || instance.powerPlantExplosionClip == null)
            return;

        instance.oneShotSource.PlayOneShot(instance.powerPlantExplosionClip, GameOptions.SfxVolume * 1.05f);
    }

    AudioSource CreateSource(string name, bool loop, bool playOnAwake)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.name = name;
        source.loop = loop;
        source.playOnAwake = playOnAwake;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 1f;
        return source;
    }

    void UpdateLoop(AudioSource source, AudioClip clip, bool shouldPlay)
    {
        if (source == null)
            return;

        if (!shouldPlay)
        {
            if (source.isPlaying)
                source.Stop();
            return;
        }

        if (clip == null)
            return;

        if (source.clip != clip)
            source.clip = clip;

        if (!source.isPlaying)
            source.Play();
    }

    void UpdateMusicState()
    {
        bool titleVisible = FindFirstObjectByType<TitleScreenRuntime>() != null;
        MusicMode targetMode = titleVisible ? MusicMode.Title : MusicMode.Gameplay;

        if (targetMode == MusicMode.Title)
        {
            if (titleBgmClip == null)
                return;

            if (currentMusicMode != MusicMode.Title || bgmSource.clip != titleBgmClip || !bgmSource.isPlaying)
            {
                bgmSource.clip = titleBgmClip;
                bgmSource.loop = true;
                bgmSource.Play();
                currentMusicMode = MusicMode.Title;
            }

            return;
        }

        if (gameplayPlaylist.Count == 0)
            return;

        if (currentMusicMode != MusicMode.Gameplay || !bgmSource.isPlaying || bgmSource.clip == null)
        {
            PlayNextGameplayTrack();
            return;
        }

        if (!bgmSource.loop && bgmSource.clip != null && bgmSource.time >= bgmSource.clip.length - 0.05f)
            PlayNextGameplayTrack();
    }

    void PlayNextGameplayTrack()
    {
        if (gameplayPlaylist.Count == 0)
            return;

        if (gameplayShuffleOrder.Count != gameplayPlaylist.Count || gameplayShuffleCursor >= gameplayShuffleOrder.Count)
            RefillGameplayShuffle();

        int nextIndex = gameplayShuffleOrder[Mathf.Clamp(gameplayShuffleCursor, 0, gameplayShuffleOrder.Count - 1)];
        gameplayShuffleCursor++;

        AudioClip clip = gameplayPlaylist[nextIndex];
        if (clip == null)
            return;

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = false;
        bgmSource.Play();
        currentMusicMode = MusicMode.Gameplay;
    }

    void RefillGameplayShuffle()
    {
        gameplayShuffleOrder.Clear();
        for (int i = 0; i < gameplayPlaylist.Count; i++)
            gameplayShuffleOrder.Add(i);

        for (int i = gameplayShuffleOrder.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (gameplayShuffleOrder[i], gameplayShuffleOrder[swapIndex]) = (gameplayShuffleOrder[swapIndex], gameplayShuffleOrder[i]);
        }

        gameplayShuffleCursor = 0;
    }

    IEnumerator LoadAudioLibrary()
    {
        string customSfxFolder = ResolveLocalMediaDirectory("Audio", "SFX");
        string attachOverridePath = FindFirstAudioFile(customSfxFolder, "ModuleAttach");
        if (!string.IsNullOrWhiteSpace(attachOverridePath))
            yield return LoadClip(attachOverridePath, clip => moduleAttachClip = clip ?? moduleAttachClip);

        string dragOverridePath = FindFirstAudioFile(customSfxFolder, "ModuleDrag");
        if (!string.IsNullOrWhiteSpace(dragOverridePath))
        {
            yield return LoadClip(dragOverridePath, clip =>
            {
                moduleDragClip = clip ?? moduleDragClip;
                dragLoopSource.clip = moduleDragClip;
            });
        }

        yield return LoadStreamingAssetsClip(clip => titleBgmClip = clip, "Title", "Title_BGM1.mp3");
        yield return LoadStreamingAssetsClip(clip =>
        {
            engineLoopClip = clip ?? engineLoopClip;
            if (engineLoopClip != null)
                engineLoopClip.name = "EngineLoop";
        }, "module", "Engine", "engine sound.mp3");
        yield return LoadStreamingAssetsClip(clip =>
        {
            laserLoopClip = clip ?? laserLoopClip;
            if (laserLoopClip != null)
                laserLoopClip.name = "LaserLoop";
        }, "module", "Laser", "Laser Sound.mp3");

        string soundtrackFolder = ResolveLocalMediaDirectory("Sound Track");
        if (Directory.Exists(soundtrackFolder))
        {
            string[] playlistFiles = Directory.GetFiles(soundtrackFolder, "*.mp3", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < playlistFiles.Length; i++)
            {
                AudioClip clip = null;
                yield return LoadClip(playlistFiles[i], loaded => clip = loaded);
                if (clip != null)
                    gameplayPlaylist.Add(clip);
            }
        }
        else
        {
            for (int i = 0; i < KnownGameplayTrackFiles.Length; i++)
            {
                string trackFile = KnownGameplayTrackFiles[i];
                AudioClip clip = null;
                yield return LoadStreamingAssetsClip(loaded => clip = loaded, "Sound Track", trackFile);
                if (clip != null)
                    gameplayPlaylist.Add(clip);
            }
        }

        RefillGameplayShuffle();
        UpdateMusicState();
    }

    static string FindFirstAudioFile(string folderPath, string fileStem)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return null;

        string[] extensions = { ".wav", ".mp3", ".ogg" };
        for (int i = 0; i < extensions.Length; i++)
        {
            string candidate = Path.Combine(folderPath, fileStem + extensions[i]);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    IEnumerator LoadClip(string filePath, System.Action<AudioClip> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(ToFileUri(filePath), GuessAudioType(filePath));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to load audio clip: {filePath} ({request.error})");
            onLoaded?.Invoke(null);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip != null)
            clip.name = Path.GetFileNameWithoutExtension(filePath);

        onLoaded?.Invoke(clip);
    }

    IEnumerator LoadStreamingAssetsClip(System.Action<AudioClip> onLoaded, params string[] relativeSegments)
    {
        string uri = BuildStreamingAssetsUri(relativeSegments);
        yield return LoadClipFromUri(uri, GetClipName(relativeSegments), onLoaded);
    }

    IEnumerator LoadClipFromUri(string clipUri, string clipName, System.Action<AudioClip> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(clipUri))
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(clipUri, GuessAudioType(clipUri));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to load audio clip: {clipUri} ({request.error})");
            onLoaded?.Invoke(null);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip != null && !string.IsNullOrWhiteSpace(clipName))
            clip.name = clipName;

        onLoaded?.Invoke(clip);
    }

    static AudioType GuessAudioType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".wav" => AudioType.WAV,
            ".ogg" => AudioType.OGGVORBIS,
            _ => AudioType.MPEG
        };
    }

    static string ResolveLocalMediaDirectory(params string[] relativeSegments)
    {
        string relativePath = Path.Combine(relativeSegments);

        string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (Directory.Exists(streamingAssetsPath))
            return streamingAssetsPath;

        string projectAssetsPath = Path.Combine(Application.dataPath, relativePath);
        if (Directory.Exists(projectAssetsPath))
            return projectAssetsPath;

        return streamingAssetsPath;
    }

    static string BuildStreamingAssetsUri(params string[] relativeSegments)
    {
        string basePath = Application.streamingAssetsPath.TrimEnd('/', '\\');
        if (basePath.Contains("://"))
        {
            string relativeUri = string.Join("/", EncodeUriSegments(relativeSegments));
            return string.IsNullOrWhiteSpace(relativeUri) ? basePath : $"{basePath}/{relativeUri}";
        }

        string fullPath = basePath;
        if (relativeSegments != null)
        {
            for (int i = 0; i < relativeSegments.Length; i++)
            {
                string segment = relativeSegments[i];
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                fullPath = Path.Combine(fullPath, segment);
            }
        }

        return ToFileUri(fullPath);
    }

    static string ToFileUri(string filePath)
    {
        return new System.Uri(filePath).AbsoluteUri;
    }

    static IEnumerable<string> EncodeUriSegments(string[] relativeSegments)
    {
        if (relativeSegments == null)
            yield break;

        for (int i = 0; i < relativeSegments.Length; i++)
        {
            string segment = relativeSegments[i];
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            yield return UnityWebRequest.EscapeURL(segment.Trim('/', '\\'));
        }
    }

    static string GetClipName(string[] relativeSegments)
    {
        if (relativeSegments == null || relativeSegments.Length == 0)
            return string.Empty;

        return Path.GetFileNameWithoutExtension(relativeSegments[relativeSegments.Length - 1]);
    }

    AudioClip CreateAttachThunkClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.09f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float burst = Mathf.Exp(-t * 22f);
            float body = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.65f;
            float click = Mathf.Sin(2f * Mathf.PI * 690f * t) * 0.2f;
            samples[i] = Mathf.Clamp((body + click) * burst, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("ModuleAttachThunk", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip CreateElectricBuzzClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.38f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float hum = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.22f;
            float buzz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t)) * 0.06f;
            float sparkle = Mathf.Sin(2f * Mathf.PI * 820f * t) * (0.02f + 0.02f * Mathf.Sin(2f * Mathf.PI * 3.2f * t));
            float gate = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            samples[i] = Mathf.Clamp((hum + buzz + sparkle) * gate, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("ModuleDragBuzz", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip CreateScrapPickupClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.17f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 10f);
            float toneA = Mathf.Sin(2f * Mathf.PI * 880f * t);
            float toneB = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.65f;
            samples[i] = Mathf.Clamp((toneA + toneB) * 0.28f * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("ScrapPickup", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip CreateModuleBreakClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.24f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 12f);
            float low = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.45f;
            float crack = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.18f;
            float fizz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 78f * t)) * 0.05f;
            samples[i] = Mathf.Clamp((low + crack + fizz) * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("ModuleBreak", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip CreatePowerPlantExplosionClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 6.5f);
            float boom = Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.5f;
            float crack = Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.2f;
            float fizz = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 95f * t)) * 0.08f;
            float tail = Mathf.Sin(2f * Mathf.PI * 34f * t) * 0.18f;
            samples[i] = Mathf.Clamp((boom + crack + fizz + tail) * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("PowerPlantExplosion", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
