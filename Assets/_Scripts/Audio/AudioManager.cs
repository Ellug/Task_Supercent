using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [SerializeField] private SfxLibrary _sfxLibrary;
    [SerializeField, Range(1, 32)] private int _sfxSourceCount = 8;
    [Header("World SFX 3D")]
    [SerializeField, Min(0.05f)] private float _worldSfxMinDistance = 2f;
    [SerializeField, Min(0.1f)] private float _worldSfxMaxDistance = 40f;
    [SerializeField, Range(0f, 1f)] private float _worldSfxSpatialBlend = 0.85f;
    [SerializeField] private AudioRolloffMode _worldSfxRolloff = AudioRolloffMode.Linear;

    private AudioSource[] _sfxSources;
    private AudioSource[] _worldSfxSources;
    private int _sfxIndex;
    private int _worldSfxIndex;

    protected override void OnSingletonAwake()
    {
        BuildSfxSources();
    }

    // id에 해당하는 SFX를 풀링된 AudioSource로 재생
    public void PlaySFX(int id)
    {
        EnsureSources();

        if (!TryGetEntry(id, out SfxEntry entry))
            return;

        PlayEntry2D(entry);
    }

    // 월드 좌표에서 3D 감쇠로 SFX 재생
    public void PlayWorldSFX(int id, Vector3 worldPosition)
    {
        EnsureSources();

        if (!TryGetEntry(id, out SfxEntry entry))
            return;

        PlayEntry3D(entry, worldPosition);
    }

    // 외부에서 인스턴스 null 체크 없이 호출하는 공용 진입점
    public static void TryPlaySFX(int id)
    {
        if (Instance == null)
            return;

        Instance.PlaySFX(id);
    }

    // 외부에서 위치 기반 3D SFX 재생
    public static void TryPlayWorldSFX(int id, Vector3 worldPosition)
    {
        if (Instance == null)
            return;

        Instance.PlayWorldSFX(id, worldPosition);
    }

    // 지정된 AudioSource로 id 기반 SFX 재생
    public bool TryPlaySFXOnSource(int id, AudioSource source)
    {
        if (source == null)
            return false;

        if (!TryGetEntry(id, out SfxEntry entry))
            return false;

        source.clip = entry.Clip;
        source.volume = entry.Volume;
        source.Play();
        return true;
    }

    // 외부에서 인스턴스 null 체크 없이 소스 지정 재생
    public static bool TryPlaySFXViaSource(int id, AudioSource source)
    {
        if (Instance == null || source == null)
            return false;

        return Instance.TryPlaySFXOnSource(id, source);
    }

    // 메인 카메라 뷰 안에 월드 좌표가 들어오는지 검사
    public static bool IsInMainCameraView(Vector3 worldPosition, float margin = 0f)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return true;

        Vector3 viewport = mainCamera.WorldToViewportPoint(worldPosition);
        if (viewport.z <= 0f)
            return false;

        return viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    private void BuildSfxSources()
    {
        int count = Mathf.Max(1, _sfxSourceCount);

        // 2D SFX 소스 풀
        _sfxSources = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            _sfxSources[i] = gameObject.AddComponent<AudioSource>();
            _sfxSources[i].playOnAwake = false;
            _sfxSources[i].loop = false;
            _sfxSources[i].dopplerLevel = 0f;
            _sfxSources[i].spatialBlend = 0f;
        }

        // 3D 월드 SFX 소스 풀 (각 소스별 별도 Transform 보유)
        _worldSfxSources = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            GameObject child = new($"WorldSfx_{i + 1}");
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = _worldSfxSpatialBlend;
            source.rolloffMode = _worldSfxRolloff;
            source.minDistance = _worldSfxMinDistance;
            source.maxDistance = Mathf.Max(_worldSfxMinDistance + 0.01f, _worldSfxMaxDistance);
            _worldSfxSources[i] = source;
        }
    }

    // 라운드 로빈으로 다음 AudioSource 반환
    private AudioSource GetNextSfxSource()
    {
        AudioSource source = _sfxSources[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxSources.Length;
        return source;
    }

    private AudioSource GetNextWorldSfxSource()
    {
        AudioSource source = _worldSfxSources[_worldSfxIndex];
        _worldSfxIndex = (_worldSfxIndex + 1) % _worldSfxSources.Length;
        return source;
    }

    private bool TryGetEntry(int id, out SfxEntry entry)
    {
        entry = default;
        if (_sfxLibrary == null)
            return false;

        return _sfxLibrary.TryGet(id, out entry);
    }

    private void PlayEntry2D(SfxEntry entry)
    {
        AudioSource source = GetNextSfxSource();
        source.transform.position = transform.position;
        source.spatialBlend = 0f;
        source.clip = entry.Clip;
        source.volume = entry.Volume;
        source.Play();
    }

    private void PlayEntry3D(SfxEntry entry, Vector3 worldPosition)
    {
        AudioSource source = GetNextWorldSfxSource();
        source.transform.position = worldPosition;
        source.spatialBlend = _worldSfxSpatialBlend;
        source.rolloffMode = _worldSfxRolloff;
        source.minDistance = _worldSfxMinDistance;
        source.maxDistance = Mathf.Max(_worldSfxMinDistance + 0.01f, _worldSfxMaxDistance);
        source.clip = entry.Clip;
        source.volume = entry.Volume;
        source.Play();
    }

    private void EnsureSources()
    {
        bool needs2D = _sfxSources == null || _sfxSources.Length == 0;
        bool needs3D = _worldSfxSources == null || _worldSfxSources.Length == 0;
        if (!needs2D && !needs3D)
            return;

        BuildSfxSources();
    }
}
