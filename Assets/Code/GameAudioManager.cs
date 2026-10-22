using UnityEngine;

/// <summary>Central two-channel audio playback: one music track and one replaceable sound effect.</summary>
[DisallowMultipleComponent]
public class GameAudioManager : MonoBehaviour
{
    private const string LegacyVolumePreference = "MasterVolume";
    private const string EffectVolumePreference = "VfxVolume";
    private const string MusicVolumePreference = "BackgroundMusicVolume";

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

    [Header("Visual Feedback")]
    [SerializeField] private GameObject meleeHitParticlePrefab;
    [SerializeField] private GameObject meleeSlashPrefab;
    [SerializeField] private GameObject rockWallImpactParticlePrefab;

    private AudioSource musicSource;
    private AudioSource effectSource;
    private bool bossMusicActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // The title scene's SessionManager object is a button target and must stay
            // present as a forwarding proxy; only remove its duplicate audio component.
            if (GetComponent<SessionManager>() != null) Destroy(this);
            else Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = gameObject.AddComponent<AudioSource>();
        effectSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        effectSource.spatialBlend = 0f;
        musicSource.volume = GetSavedMusicVolume();
        effectSource.volume = GetSavedEffectVolume();
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

    public static float GetSavedEffectVolume()
    {
        return GetSavedVolume(EffectVolumePreference);
    }

    public static float GetSavedMusicVolume()
    {
        return GetSavedVolume(MusicVolumePreference);
    }

    public static void SetEffectVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (Instance != null && Instance.effectSource != null) Instance.effectSource.volume = volume;
        PlayerPrefs.SetFloat(EffectVolumePreference, volume);
        PlayerPrefs.Save();
    }

    public static void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (Instance != null && Instance.musicSource != null) Instance.musicSource.volume = volume;
        PlayerPrefs.SetFloat(MusicVolumePreference, volume);
        PlayerPrefs.Save();
    }

    public void SpawnMeleeHitEffect(Vector3 position, Vector3 direction)
    {
        SpawnParticleEffect(meleeHitParticlePrefab, position, direction);
    }

    public void SpawnMeleeSlash(Vector3 position, Vector3 direction)
    {
        Vector3 flatDirection = direction;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.001f) flatDirection = Vector3.forward;

        float facingYaw = Quaternion.LookRotation(flatDirection.normalized, Vector3.up).eulerAngles.y;
        SpawnParticleEffect(meleeSlashPrefab, position, Quaternion.Euler(-90f, facingYaw, 0f), 2.5f);
    }

    public void SpawnRockWallImpact(Vector3 position, Vector3 surfaceNormal)
    {
        SpawnParticleEffect(rockWallImpactParticlePrefab, position, surfaceNormal);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    private static float GetSavedVolume(string preference)
    {
        float legacyVolume = PlayerPrefs.GetFloat(LegacyVolumePreference, 1f);
        return Mathf.Clamp01(PlayerPrefs.GetFloat(preference, legacyVolume));
    }

    private static void SpawnParticleEffect(GameObject prefab, Vector3 position, Vector3 forward)
    {
        SpawnParticleEffect(prefab, position, forward, 1f);
    }

    private static void SpawnParticleEffect(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
    {
        if (prefab == null) return;

        GameObject effect = Instantiate(prefab, position, rotation);
        effect.transform.localScale *= scale;
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        float lifetime = 1f;
        foreach (ParticleSystem particle in particles)
        {
            ParticleSystem.MainModule main = particle.main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            particle.Play(true);
        }

        Destroy(effect, lifetime + 0.25f);
    }

    private static void SpawnParticleEffect(GameObject prefab, Vector3 position, Vector3 forward, float scale)
    {
        Vector3 direction = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
        SpawnParticleEffect(prefab, position, Quaternion.LookRotation(direction, up), scale);
    }
}
