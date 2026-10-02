using UnityEngine;

/// <summary>Central two-channel audio playback: one music track and one replaceable sound effect.</summary>
[DisallowMultipleComponent]
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip bossMusic;
    [Header("Player")]
    [SerializeField] private AudioClip playerHurtSound;
    [SerializeField] private AudioClip playerDeathSound;
    [Header("World")]
    [SerializeField] private AudioClip chestOpenSound;
    [SerializeField] private AudioClip itemPickupSound;
    [SerializeField] private AudioClip wallBreakSound;

    private AudioSource musicSource;
    private AudioSource effectSource;
    private bool bossMusicActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        effectSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        effectSource.spatialBlend = 0f;
        PlayBackgroundMusic();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public static GameAudioManager EnsureInstance()
    {
        if (Instance == null) new GameObject("Game Audio Manager").AddComponent<GameAudioManager>();
        return Instance;
    }

    public void PlayEffect(AudioClip clip)
    {
        if (clip == null) return;
        effectSource.Stop();
        effectSource.clip = clip;
        effectSource.Play();
    }

    public void PlayBackgroundMusic()
    {
        bossMusicActive = false;
        PlayMusic(backgroundMusic);
    }

    public void PlayBossMusic()
    {
        if (bossMusic == null) return;
        bossMusicActive = true;
        PlayMusic(bossMusic);
    }

    public void StopBossMusic() { if (bossMusicActive) PlayBackgroundMusic(); }
    public void PlayPlayerHurt() => PlayEffect(playerHurtSound);
    public void PlayPlayerDeath() => PlayEffect(playerDeathSound);
    public void PlayChestOpen() => PlayEffect(chestOpenSound);
    public void PlayItemPickup() => PlayEffect(itemPickupSound);
    public void PlayWallBreak() => PlayEffect(wallBreakSound);

    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
