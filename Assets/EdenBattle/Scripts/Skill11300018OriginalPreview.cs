using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.Video;

public sealed class Skill11300018OriginalPreview : MonoBehaviour
{
    public enum UltimatePresentationPhase
    {
        None,
        Preparation,
        Video,
        Defender
    }

    public event Action<UltimatePresentationPhase>
        UltimatePresentationChanged;
    public event Action<int, string, bool>
        ApkUltimateHitTriggered;

    private const string BundleDirectoryName = "Skill11300018Original";
    private const int AttackEffectMinimumSortingOrder = 100;
    private const int VideoBackdropSortingOrder = 32000;
    private const int VideoSortingOrder = 32001;

    private readonly Dictionary<string, AssetBundle> bundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameObject> spawnedEffects = new List<GameObject>();
    private readonly List<string> messages = new List<string>();
    private readonly Dictionary<Renderer, bool> hiddenCharacterRenderers =
        new Dictionary<Renderer, bool>();
    private readonly Dictionary<Renderer, bool> hiddenTimelineRenderers =
        new Dictionary<Renderer, bool>();
    private readonly Dictionary<Renderer, bool> hiddenUltimateSwordRenderers =
        new Dictionary<Renderer, bool>();

    [Header("Wide stage layout")]
    public Vector3 characterStart = new Vector3(-5.35f, -2.9f, 0f);
    public Vector3 attackApproach = new Vector3(2.0f, -2.85f, 0f);
    public Vector3 attackAirPosition = new Vector3(3.05f, -1.8f, 0f);
    public Vector3 enemyFocus = new Vector3(3.45f, -1.55f, 0f);
    public Vector3 ultimateCastPosition = new Vector3(-1.1f, -2.75f, 0f);
    public Vector3 timelineOrigin = new Vector3(-1.1f, -2.55f, 0f);
    public float characterScale = 0.44f;
    public float cameraSize = 6.4f;
    public bool useRecoveredBattleLayout = true;
    public bool useOriginalGameBattleLayout;
    public bool autoHideUiDuringPlayback = true;

    [Header("Original bundle scale conversion")]
    [Range(0.02f, 1.2f)] public float normalEffectScale = 0.20f;
    [Range(0.005f, 0.5f)] public float airAttackEffectScale = 0.025f;
    [Range(0.005f, 0.8f)] public float airAttackHitEffectScale = 0.025f;
    [Range(0.005f, 0.2f)] public float impactEffectScale = 0.025f;
    [Range(0.1f, 0.8f)] public float ultimateEffectScale = 0.28f;

    [Header("Ultimate cinematic")]
    public bool useUltimateVideo = true;
    public bool suppressBuiltInUi;

    public bool IsReady
    {
        get { return !loading && character != null; }
    }

    public bool IsBusy
    {
        get { return busy; }
    }

    public string LoadingStatus
    {
        get
        {
            if (loading)
                return "正在载入战斗资源…";
            if (character == null)
                return "11300018 战斗资源载入失败";
            return busy ? "动作播放中" : "准备完成";
        }
    }

    private Transform stage;
    private GameObject character;
    private Camera previewCamera;
    private bool loading;
    private bool busy;
    private int bundleFileCount;
    private Vector2 scroll;
    private bool showUi = true;
    private GameObject warmedTimelineContainer;
    private GameObject warmedTimelineEffect;
    private GameObject activeTimelineContainer;
    private GameObject activeTimelineEffect;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private GameObject videoQuad;
    private GameObject videoBackdrop;
    private Material videoMaterial;
    private Material videoBackdropMaterial;
    private UltimatePresentationPhase ultimatePresentationPhase =
        UltimatePresentationPhase.None;

    private void Start()
    {
        if (useRecoveredBattleLayout)
            ApplyRecoveredBattleLayout();
        previewCamera = Camera.main;
        if (previewCamera == null)
            previewCamera = FindObjectOfType<Camera>();
        if (previewCamera != null)
        {
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = cameraSize;
        }

        stage = new GameObject("Skill11300018OriginalStage").transform;
        SetupVideo();
        StartCoroutine(LoadAllBundles());
    }

    private void ApplyRecoveredBattleLayout()
    {
        if (useOriginalGameBattleLayout)
        {
            ApplyOriginalGameBattleLayout();
            return;
        }

        characterStart = new Vector3(-5.25f, -3.05f, 0f);
        attackApproach = new Vector3(2.0f, -2.85f, 0f);
        attackAirPosition = new Vector3(3.05f, -1.8f, 0f);
        enemyFocus = new Vector3(3.25f, -1.65f, 0f);
        ultimateCastPosition = new Vector3(-1.1f, -2.75f, 0f);
        timelineOrigin = new Vector3(-1.1f, -2.55f, 0f);
        characterScale = 0.44f;
        normalEffectScale = 0.20f;
        airAttackEffectScale = 0.025f;
        airAttackHitEffectScale = 0.025f;
        impactEffectScale = 0.025f;
        ultimateEffectScale = 0.28f;
    }

    private void ApplyOriginalGameBattleLayout()
    {
        // 原版 battleconfig.lua：
        // cardScale=1.2，左右中位为 -18 / 18，
        // 所有战斗角色再叠加 cardPositionOff=(0,-4,0)。
        cameraSize = 25f;
        characterStart = new Vector3(-18f, -4f, 0f);
        attackApproach = new Vector3(11.5f, -4f, 0f);
        attackAirPosition = new Vector3(15f, 2f, 0f);
        enemyFocus = new Vector3(18f, -4f, 0f);
        ultimateCastPosition = Vector3.zero;
        timelineOrigin = Vector3.zero;
        characterScale = 1.2f;
        // The four attack/skill prefabs already use the absolute coordinates
        // from battleconfig.lua. Keeping them at unit scale preserves their
        // authored hit positions around x=18.5 instead of pulling them toward
        // the centre of the battlefield.
        normalEffectScale = 1f;
        airAttackEffectScale = 0.1f;
        airAttackHitEffectScale = 0.1f;
        impactEffectScale = 0.1f;
        ultimateEffectScale = 1f;
    }

    private void Update()
    {
        if (loading)
            return;
        if (Input.GetKeyDown(KeyCode.A))
            BeginNormalAttack();
        if (Input.GetKeyDown(KeyCode.S))
            BeginSkill();
        if (Input.GetKeyDown(KeyCode.U))
            BeginUltimate();
        if (Input.GetKeyDown(KeyCode.I))
            ResetPreview();
        if (Input.GetKeyDown(KeyCode.H))
            showUi = !showUi;
    }

    private IEnumerator LoadAllBundles()
    {
        if (loading)
            yield break;
        loading = true;
        SkillResourceRegistry.Clear();

        string directory = Path.Combine(Application.streamingAssetsPath, BundleDirectoryName);
#if UNITY_ANDROID && !UNITY_EDITOR
        string[] paths = null;
        string manifestUrl = GetStreamingAssetUrl(
            BundleDirectoryName + "/manifest.json");
        using (UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUrl))
        {
            yield return manifestRequest.SendWebRequest();
            if (manifestRequest.isNetworkError || manifestRequest.isHttpError)
            {
                Log("Bundle manifest is missing: " + manifestRequest.error);
                loading = false;
                yield break;
            }

            MatchCollection matches = Regex.Matches(
                manifestRequest.downloadHandler.text,
                "\"file\"\\s*:\\s*\"([^\"]+\\.aab)\"");
            paths = new string[matches.Count];
            for (int matchIndex = 0;
                 matchIndex < matches.Count;
                 matchIndex++)
            {
                paths[matchIndex] = matches[matchIndex].Groups[1].Value;
            }
        }
#else
        if (!Directory.Exists(directory))
        {
            Log("Bundle directory is missing: " + directory);
            loading = false;
            yield break;
        }

        string[] paths = Directory.GetFiles(directory, "*.aab");
#endif
        Array.Sort(paths, CompareBundlePaths);
        bundleFileCount = paths.Length;
        Log("Loading original UnityFS bundles: " + bundleFileCount);

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            string file = Path.GetFileName(path);
            AssetBundle bundle = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            string bundleUrl = GetStreamingAssetUrl(
                BundleDirectoryName + "/" + file);
            using (UnityWebRequest bundleRequest =
                UnityWebRequestAssetBundle.GetAssetBundle(bundleUrl))
            {
                yield return bundleRequest.SendWebRequest();
                if (bundleRequest.isNetworkError ||
                    bundleRequest.isHttpError)
                {
                    Log("FAILED " + file + ": " + bundleRequest.error);
                }
                else
                {
                    bundle =
                        DownloadHandlerAssetBundle.GetContent(bundleRequest);
                }
            }
#else
            try
            {
                bundle = AssetBundle.LoadFromFile(path);
            }
            catch (Exception exception)
            {
                Log("FAILED " + file + ": " + exception.Message);
            }
#endif
            if (bundle == null)
            {
                if (!messages[messages.Count - 1].StartsWith("FAILED ", StringComparison.Ordinal))
                    Log("FAILED " + file + " (Android bundle rejected by this Editor)");
                continue;
            }

            bundles[file] = bundle;
            try
            {
                Texture2D[] textures = bundle.LoadAllAssets<Texture2D>();
                for (int t = 0; t < textures.Length; t++)
                    SkillResourceRegistry.Register(textures[t]);
                Mesh[] meshes = bundle.LoadAllAssets<Mesh>();
                for (int m = 0; m < meshes.Length; m++)
                    SkillResourceRegistry.Register(meshes[m]);
                SkillResourceRegistry.RestoreMaterials(bundle.LoadAllAssets<Material>());
            }
            catch (Exception exception)
            {
                Log("Asset preload warning " + file + ": " + exception.Message);
            }

            if ((i + 1) % 12 == 0 || i == paths.Length - 1)
                Log("Loaded " + (i + 1) + "/" + paths.Length);
            yield return null;
        }

        Log("Registry: textures=" + SkillResourceRegistry.TextureCount +
            " meshes=" + SkillResourceRegistry.MeshCount);
        SpawnCharacter();
        InspectCorePrefabs();
        PrepareTimelineEffect();
        loading = false;
        Log("Ready. A=普通攻击, S=爆气, U=奥义, I=重置, H=面板");
    }

    private static int CompareBundlePaths(string a, string b)
    {
        string aName = Path.GetFileName(a);
        string bName = Path.GetFileName(b);
        int priority = BundlePriority(aName).CompareTo(BundlePriority(bName));
        return priority != 0 ? priority : string.CompareOrdinal(aName, bName);
    }

    private static string GetStreamingAssetUrl(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = UnityWebRequest.EscapeURL(parts[i]).Replace("+", "%20");
        return Application.streamingAssetsPath.TrimEnd('/') + "/" +
            string.Join("/", parts);
    }

    private static int BundlePriority(string file)
    {
        if (file.Equals("common.aab", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (file.StartsWith("st_", StringComparison.OrdinalIgnoreCase) ||
            file.Equals("m_soulgames.aab", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (file.StartsWith("m_cardspine_", StringComparison.OrdinalIgnoreCase))
            return 2;
        return 3;
    }

    private void SpawnCharacter()
    {
        GameObject prefab = LoadPrefab("m_cardspine_11300018.aab", "cardspine_11300018.prefab");
        if (prefab == null)
        {
            Log("Original labi character prefab was not found.");
            return;
        }

        int rebound = SkillResourceRegistry.RestorePrefab(prefab);
        GameObject container = new GameObject("LabiCharacterContainer");
        container.transform.SetParent(stage, false);
        container.SetActive(false);
        character = Instantiate(prefab, container.transform);
        character.name = "CardSpine_11300018_Original";
        character.transform.localPosition = characterStart;
        character.transform.localScale *= characterScale;
        container.SetActive(true);
        SetCharacterSortingOrder(character, 40);
        PlaySpineAnimation("idle", true);
        Log("Original labi instantiated; rebound=" + rebound);
    }

    private GameObject LoadPrefab(string file, string nameFragment)
    {
        AssetBundle bundle;
        if (!bundles.TryGetValue(file, out bundle) || bundle == null)
            return null;
        string[] names = bundle.GetAllAssetNames();
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i].IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            GameObject prefab = bundle.LoadAsset<GameObject>(names[i]);
            if (prefab != null)
                return prefab;
        }
        return null;
    }

    private void InspectCorePrefabs()
    {
        string[] files =
        {
            "eft_fx_11300018_attack.aab",
            "eft_fx_11300018_attack_2.aab",
            "eft_fx_11300018_skill.aab",
            "eft_fx_11300018_skill_2.aab",
            "eft_labi_shouji.aab",
            "eft_fx_timeline_11300018_xp.aab"
        };
        for (int i = 0; i < files.Length; i++)
        {
            GameObject prefab = LoadPrefab(files[i], ".prefab");
            if (prefab == null)
            {
                Log("No prefab: " + files[i]);
                continue;
            }
            List<string> missing = SkillResourceRegistry.FindMissingResources(prefab);
            Log(files[i] + " missing resources=" + missing.Count +
                (missing.Count == 0 ? string.Empty : " [" + JoinFirst(missing, 6) + "]"));
        }
    }

    public void BeginNormalAttack()
    {
        if (!busy && character != null)
            StartCoroutine(PlayNormalAttack());
    }

    public void BeginSkill()
    {
        if (!busy && character != null)
            StartCoroutine(PlaySkill());
    }

    public void BeginUltimate()
    {
        if (!busy && character != null)
            StartCoroutine(PlayUltimateSequence());
    }

    private IEnumerator PlayNormalAttack()
    {
        busy = true;
        if (autoHideUiDuringPlayback)
            showUi = false;
        ClearEffects();
        SetCharacterVisible(true);
        character.transform.localPosition = characterStart;
        PlaySpineAnimation("attack", false);
        Log("APK normal: attack_2");

        float sequenceStart = Time.time;
        // attack_2 is authored at the original defender coordinate
        // (18.5, -4), so its container stays at the world origin.
        SpawnEffect(
            Skill11300018ApkBattleLogic.NormalEffectBundles[0],
            Vector3.zero,
            false);
        StartCoroutine(MoveCharacter(
            characterStart,
            attackApproach,
            0.24f));

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.NormalReturnTime);
        Vector3 returnFrom = character.transform.localPosition;
        yield return MoveCharacter(returnFrom, characterStart, 0.28f);
        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.NormalCleanupTime);
        ClearEffects();
        PlaySpineAnimation("idle", true);
        showUi = true;
        busy = false;
    }

    private IEnumerator PlaySkill()
    {
        busy = true;
        if (autoHideUiDuringPlayback)
            showUi = false;
        ClearEffects();
        SetCharacterVisible(true);
        character.transform.localPosition = characterStart;
        PlaySpineAnimation("skill", false);
        Log("APK burst: attack + skill + skill_2");

        float sequenceStart = Time.time;
        // The blue target-relative attack effect belongs to the burst
        // presentation in the recovered mapping, not to the APK normal mode.
        SpawnEffectAlignedToTarget(
            Skill11300018ApkBattleLogic.BurstEffectBundles[0],
            false);
        SpawnEffectAlignedToTarget(
            Skill11300018ApkBattleLogic.BurstEffectBundles[1],
            false);
        // skill_2 already owns the original absolute defender coordinate.
        SpawnEffect(
            Skill11300018ApkBattleLogic.BurstEffectBundles[2],
            Vector3.zero,
            false);
        StartCoroutine(MoveCharacter(
            characterStart,
            attackApproach,
            0.26f));

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.BurstReturnTime);
        Vector3 returnFrom = character.transform.localPosition;
        yield return MoveCharacter(returnFrom, characterStart, 0.30f);
        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.BurstCleanupTime);
        ClearEffects();
        PlaySpineAnimation("idle", true);
        showUi = true;
        busy = false;
    }

    private IEnumerator PlayUltimateSequence()
    {
        busy = true;
        if (autoHideUiDuringPlayback)
            showUi = false;
        ClearEffects();
        SetVideoVisible(false);
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.Preparation);

        bool playCinematic = useUltimateVideo && videoPlayer != null;
        if (playCinematic && !videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            float prepareDeadline = Time.realtimeSinceStartup + 3f;
            while (!videoPlayer.isPrepared && Time.realtimeSinceStartup < prepareDeadline)
                yield return null;
            playCinematic = videoPlayer.isPrepared;
            if (!playCinematic)
                Log("Ultimate video prepare timed out; continuing with the live Timeline.");
        }

        SetCharacterVisible(true);
        character.transform.localPosition = ultimateCastPosition;
        PlaySpineAnimation("uniqueskill", false);
        Log("APK ultimate logic + " +
            Skill11300018ApkBattleLogic.TimelineName);

        GameObject timeline = SpawnPreparedTimeline();
        SetUltimateSwordVisualsVisible(false);
        float duration = 10.8f;
        if (timeline != null)
        {
            PlayableDirector[] directors = timeline.GetComponentsInChildren<PlayableDirector>(true);
            for (int i = 0; i < directors.Length; i++)
            {
                directors[i].RebuildGraph();
                directors[i].time = 0.0;
                directors[i].Play();
                if (directors[i].duration > 0.1 && directors[i].duration < 60.0)
                    duration = Mathf.Max(duration, (float)directors[i].duration);
            }
            Log("Timeline directors=" + directors.Length +
                " duration=" + duration.ToString("0.00") + "s");
        }

        float sequenceStart = Time.time;
        if (playCinematic)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                Skill11300018ApkBattleLogic.UltimateVideoStartTime);
            SetCharacterVisible(false);
            SetTimelineVisualsVisible(false);
            SetUltimatePresentationPhase(
                UltimatePresentationPhase.Video);
            videoPlayer.time = 0.0;
            videoPlayer.Play();
            SetVideoVisible(true);

            yield return WaitForSequenceTime(
                sequenceStart,
                Skill11300018ApkBattleLogic.UltimateVideoEndTime);
            SetVideoVisible(false);
            if (videoPlayer.isPlaying)
                videoPlayer.Pause();
            SetTimelineVisualsVisible(true);
            SetCharacterVisible(true);
            SetUltimatePresentationPhase(
                UltimatePresentationPhase.Preparation);
        }

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.UltimateAttackerInvisibleTime);
        SetCharacterVisible(false);

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.UltimateDefendersVisibleTime);
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.Defender);
        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.UltimateSwordRevealTime);
        SetUltimateSwordVisualsVisible(true);
        for (int hitIndex = 0;
             hitIndex <
                Skill11300018ApkBattleLogic.UltimateHits.Length;
             hitIndex++)
        {
            Skill11300018UltimateHit hit =
                Skill11300018ApkBattleLogic.UltimateHits[hitIndex];
            yield return WaitForSequenceTime(
                sequenceStart,
                hit.timeSeconds);
            RegisterApkUltimateHit(hitIndex, hit);
        }

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.UltimateReturnTime);
        character.transform.localPosition = characterStart;
        SetCharacterVisible(true);
        PlaySpineAnimation("idle", true);
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.None);

        yield return WaitForSequenceTime(
            sequenceStart,
            Skill11300018ApkBattleLogic.UltimateCleanupTime);
        if (videoPlayer != null)
            videoPlayer.Stop();
        SetVideoVisible(false);
        SetTimelineVisualsVisible(true);
        RecycleActiveTimeline();
        showUi = true;
        busy = false;
    }

    private void RegisterApkUltimateHit(
        int hitIndex,
        Skill11300018UltimateHit hit)
    {
        int displayIndex = hitIndex + 1;
        Log("APK ultimate hit " + displayIndex + "/" +
            Skill11300018ApkBattleLogic.UltimateTotalHitCount +
            " t=" + hit.timeSeconds.ToString("0.000") +
            " state=" + hit.defenderState +
            " final=" + hit.isFinal);

        Action<int, string, bool> callback =
            ApkUltimateHitTriggered;
        if (callback != null)
        {
            callback(
                displayIndex,
                hit.defenderState,
                hit.isFinal);
        }
    }

    private GameObject SpawnEffect(string file, Vector3 position, bool timeline)
    {
        GameObject prefab = LoadPrefab(file, ".prefab");
        if (prefab == null)
        {
            Log("Effect prefab unavailable: " + file);
            return null;
        }

        int rebound = SkillResourceRegistry.RestorePrefab(prefab);
        GameObject container = new GameObject(prefab.name + "_Container");
        container.transform.SetParent(stage, false);
        container.transform.localPosition = position;
        float effectScale = GetNormalEffectScale(file);
        container.transform.localScale = Vector3.one * effectScale;
        container.SetActive(false);
        GameObject effect = Instantiate(prefab, container.transform);
        effect.name = prefab.name + "_Original";
        SkillResourceRegistry.RestorePrefab(effect);
        RaiseEffectSortingOrders(
            effect,
            AttackEffectMinimumSortingOrder);

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        PlayableDirector[] directors = effect.GetComponentsInChildren<PlayableDirector>(true);
        ConfigureParticleScaling(particles);
        for (int i = 0; i < directors.Length; i++)
            directors[i].playOnAwake = false;
        container.SetActive(true);
        spawnedEffects.Add(container);
        if (!timeline || directors.Length == 0)
        {
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);
        }
        Log("Spawned " + file + " particles=" + particles.Length +
            " directors=" + directors.Length + " rebound=" + rebound +
            " scale=" + effectScale.ToString("0.00"));
        Destroy(container, timeline ? 18f : 7f);
        return effect;
    }

    private GameObject SpawnEffectAlignedToTarget(
        string file,
        bool timeline)
    {
        GameObject prefab = LoadPrefab(file, ".prefab");
        if (prefab == null)
        {
            Log("Effect prefab unavailable: " + file);
            return null;
        }

        // attack/skill are target-relative prefabs, unlike attack_2/skill_2
        // whose roots already contain the original absolute defender
        // position (18.5, -4, 0). Compensate the relative prefab's own root
        // after scale conversion so its origin lands exactly on enemyFocus.
        float effectScale = GetNormalEffectScale(file);
        Vector3 scaledPrefabRoot =
            prefab.transform.localPosition * effectScale;
        Vector3 containerPosition = enemyFocus - scaledPrefabRoot;
        return SpawnEffect(file, containerPosition, timeline);
    }

    private float GetNormalEffectScale(string file)
    {
        if (file.Equals("eft_fx_11300018_attack_air.aab",
            StringComparison.OrdinalIgnoreCase))
            return airAttackEffectScale;
        if (file.Equals("eft_fx_11300018_attack_air_hit.aab",
            StringComparison.OrdinalIgnoreCase))
            return airAttackHitEffectScale;
        if (file.Equals("eft_labi_shouji.aab",
            StringComparison.OrdinalIgnoreCase))
            return impactEffectScale;
        return normalEffectScale;
    }

    private void PrepareTimelineEffect()
    {
        if (warmedTimelineContainer != null || stage == null ||
            !bundles.ContainsKey("eft_fx_timeline_11300018_xp.aab"))
            return;

        GameObject prefab = LoadPrefab("eft_fx_timeline_11300018_xp.aab", ".prefab");
        if (prefab == null)
            return;
        int rebound = SkillResourceRegistry.RestorePrefab(prefab);
        GameObject container = new GameObject(prefab.name + "_WarmContainer");
        container.transform.SetParent(stage, false);
        container.transform.localPosition = timelineOrigin;
        container.transform.localScale = Vector3.zero;
        container.SetActive(false);
        GameObject effect = Instantiate(prefab, container.transform);
        effect.name = prefab.name + "_WarmedOriginal";
        SkillResourceRegistry.RestorePrefab(effect);
        RaiseEffectSortingOrders(
            effect,
            AttackEffectMinimumSortingOrder);

        PlayableDirector[] directors = effect.GetComponentsInChildren<PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
            directors[i].playOnAwake = false;
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        ConfigureParticleScaling(particles);

        // Allocate the large hierarchy and particle buffers while the loading
        // panel is visible, rather than on the first ultimate frame.
        container.SetActive(true);
        for (int i = 0; i < directors.Length; i++)
            directors[i].Stop();
        for (int i = 0; i < particles.Length; i++)
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        container.SetActive(false);
        container.transform.localScale = Vector3.one * ultimateEffectScale;

        warmedTimelineContainer = container;
        warmedTimelineEffect = effect;
        Log("Prewarmed original XP Timeline particles=" + particles.Length + " rebound=" + rebound);
    }

    private GameObject SpawnPreparedTimeline()
    {
        PrepareTimelineEffect();
        GameObject container = warmedTimelineContainer;
        GameObject effect = warmedTimelineEffect;
        warmedTimelineContainer = null;
        warmedTimelineEffect = null;
        if (container == null || effect == null)
        {
            Log("Original XP Timeline could not be prepared.");
            return null;
        }

        container.transform.localPosition = timelineOrigin;
        container.transform.localScale = Vector3.one * ultimateEffectScale;
        PlayableDirector[] directors = effect.GetComponentsInChildren<PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
            directors[i].playOnAwake = false;
        container.SetActive(true);
        activeTimelineContainer = container;
        activeTimelineEffect = effect;
        Log("Spawned original XP Timeline scale=" + ultimateEffectScale.ToString("0.00"));
        return effect;
    }

    private void RecycleActiveTimeline()
    {
        if (activeTimelineContainer == null || activeTimelineEffect == null)
            return;

        SetTimelineVisualsVisible(true);
        SetUltimateSwordVisualsVisible(true);
        PlayableDirector[] directors =
            activeTimelineEffect.GetComponentsInChildren<PlayableDirector>(true);
        for (int i = 0; i < directors.Length; i++)
        {
            directors[i].Stop();
            directors[i].time = 0.0;
        }
        ParticleSystem[] particles =
            activeTimelineEffect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        activeTimelineContainer.SetActive(false);
        activeTimelineContainer.transform.localPosition = timelineOrigin;
        activeTimelineContainer.transform.localScale = Vector3.one * ultimateEffectScale;
        warmedTimelineContainer = activeTimelineContainer;
        warmedTimelineEffect = activeTimelineEffect;
        activeTimelineContainer = null;
        activeTimelineEffect = null;
    }

    private void SetupVideo()
    {
        string path = GetStreamingAssetUrl(
            "Skill11300018/FX_timeline_11300018_xp.m4v");
#if !UNITY_ANDROID || UNITY_EDITOR
        if (!File.Exists(path))
        {
            Log("Ultimate cinematic is missing: " + path);
            return;
        }
#endif

        GameObject videoObject = new GameObject("Skill11300018OriginalVideoPlayer");
        videoObject.transform.SetParent(stage, false);
        videoPlayer = videoObject.AddComponent<VideoPlayer>();
        videoTexture = new RenderTexture(1650, 750, 0, RenderTextureFormat.ARGB32);
        videoTexture.name = "Skill11300018OriginalVideoTexture";
        videoTexture.Create();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = path;

        float worldHeight = cameraSize * 2f;
        float worldWidth = worldHeight *
            (previewCamera == null ? 16f / 9f : previewCamera.aspect);

        videoBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        videoBackdrop.name = "Skill11300018CinematicBackdrop";
        videoBackdrop.transform.SetParent(stage, false);
        PlaceCinematicSurface(videoBackdrop.transform, 90.2f);
        videoBackdrop.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
        Collider backdropCollider = videoBackdrop.GetComponent<Collider>();
        if (backdropCollider != null)
            Destroy(backdropCollider);
        Shader colorShader = Shader.Find("Unlit/Color");
        if (colorShader == null)
        {
            colorShader = Shader.Find(
                "SkillRestore/Battle Scene Unlit");
        }
        if (colorShader == null)
        {
            AbortVideoSetup(
                videoObject,
                "Ultimate video disabled: no compatible backdrop shader.");
            return;
        }
        videoBackdropMaterial = new Material(colorShader);
        videoBackdropMaterial.color = Color.black;
        videoBackdropMaterial.renderQueue = 3997;
        MeshRenderer backdropRenderer =
            videoBackdrop.GetComponent<MeshRenderer>();
        backdropRenderer.sharedMaterial = videoBackdropMaterial;
        backdropRenderer.sortingOrder = VideoBackdropSortingOrder;

        videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        videoQuad.name = "Skill11300018OriginalCinematicScreen";
        videoQuad.transform.SetParent(stage, false);
        PlaceCinematicSurface(videoQuad.transform, 90f);
        float videoHeight = worldWidth / (1650f / 750f);
        videoQuad.transform.localScale = new Vector3(worldWidth, videoHeight, 1f);
        Collider videoCollider = videoQuad.GetComponent<Collider>();
        if (videoCollider != null)
            Destroy(videoCollider);
        Shader videoShader = Shader.Find("SkillRestore/11300018 Video Unlit");
        if (videoShader == null)
            videoShader = Shader.Find("Unlit/Texture");
        if (videoShader == null)
        {
            videoShader = Shader.Find(
                "SkillRestore/Battle Scene Unlit");
        }
        if (videoShader == null)
        {
            AbortVideoSetup(
                videoObject,
                "Ultimate video disabled: no compatible video shader.");
            return;
        }
        videoMaterial = new Material(videoShader);
        videoMaterial.name = "Skill11300018OriginalVideoMaterial";
        videoMaterial.mainTexture = videoTexture;
        videoMaterial.renderQueue = 3998;
        MeshRenderer videoRenderer =
            videoQuad.GetComponent<MeshRenderer>();
        videoRenderer.sharedMaterial = videoMaterial;
        videoRenderer.sortingOrder = VideoSortingOrder;
        SetVideoVisible(false);
        videoPlayer.Prepare();
        Log("Preparing original 3.83s ultimate cinematic.");
    }

    private void AbortVideoSetup(
        GameObject videoObject,
        string reason)
    {
        Log(reason);
        if (videoQuad != null)
            Destroy(videoQuad);
        if (videoBackdrop != null)
            Destroy(videoBackdrop);
        if (videoMaterial != null)
            Destroy(videoMaterial);
        if (videoBackdropMaterial != null)
            Destroy(videoBackdropMaterial);
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
        if (videoObject != null)
            Destroy(videoObject);

        videoPlayer = null;
        videoTexture = null;
        videoQuad = null;
        videoBackdrop = null;
        videoMaterial = null;
        videoBackdropMaterial = null;
    }

    private void PlaceCinematicSurface(
        Transform surface,
        float originalCameraDistance)
    {
        if (surface == null)
            return;
        if (!useOriginalGameBattleLayout || previewCamera == null)
        {
            surface.localPosition = new Vector3(
                0f,
                0f,
                originalCameraDistance > 90f ? -2f : -2.1f);
            return;
        }

        surface.position = previewCamera.transform.position +
            previewCamera.transform.forward * originalCameraDistance;
        surface.rotation = previewCamera.transform.rotation;
    }

    private void SetVideoVisible(bool visible)
    {
        if (videoBackdrop != null)
            videoBackdrop.SetActive(visible);
        if (videoQuad != null)
            videoQuad.SetActive(visible);
    }

    private static void ConfigureParticleScaling(ParticleSystem[] particles)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    private static void RaiseEffectSortingOrders(
        GameObject root,
        int minimumOrder)
    {
        if (root == null)
            return;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        int currentMinimum = int.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
            currentMinimum = Mathf.Min(
                currentMinimum,
                renderers[i].sortingOrder);

        int offset = Mathf.Max(0, minimumOrder - currentMinimum);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = Mathf.Clamp(
                renderers[i].sortingOrder + offset,
                short.MinValue,
                short.MaxValue);
        }
    }

    private void SetTimelineVisualsVisible(bool visible)
    {
        if (activeTimelineEffect == null)
        {
            if (visible)
                hiddenTimelineRenderers.Clear();
            return;
        }

        if (!visible)
        {
            hiddenTimelineRenderers.Clear();
            Renderer[] renderers =
                activeTimelineEffect.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                hiddenTimelineRenderers[renderers[i]] =
                    renderers[i].enabled;
                renderers[i].enabled = false;
            }
            return;
        }

        foreach (KeyValuePair<Renderer, bool> pair
                 in hiddenTimelineRenderers)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }
        hiddenTimelineRenderers.Clear();
    }

    private void SetUltimateSwordVisualsVisible(bool visible)
    {
        if (visible)
        {
            foreach (KeyValuePair<Renderer, bool> pair
                     in hiddenUltimateSwordRenderers)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
            hiddenUltimateSwordRenderers.Clear();
            return;
        }

        if (activeTimelineEffect == null ||
            hiddenUltimateSwordRenderers.Count > 0)
        {
            return;
        }

        Renderer[] renderers =
            activeTimelineEffect.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsEndBlastSwordRenderer(renderer))
                continue;

            hiddenUltimateSwordRenderers[renderer] = renderer.enabled;
            renderer.enabled = false;
        }
        Log("Hidden end_blass sword preroll renderers=" +
            hiddenUltimateSwordRenderers.Count);
    }

    private static bool IsEndBlastSwordRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        bool underSwordNode = false;
        Transform current = renderer.transform;
        while (current != null)
        {
            if (current.name.StartsWith(
                    "sword",
                    StringComparison.OrdinalIgnoreCase))
            {
                underSwordNode = true;
            }
            if (current.name.Equals(
                    "end_blass",
                    StringComparison.OrdinalIgnoreCase))
            {
                return underSwordNode;
            }
            current = current.parent;
        }
        return false;
    }

    private void SetUltimatePresentationPhase(
        UltimatePresentationPhase phase)
    {
        if (ultimatePresentationPhase == phase)
            return;

        ultimatePresentationPhase = phase;
        Action<UltimatePresentationPhase> handler =
            UltimatePresentationChanged;
        if (handler != null)
            handler(phase);
    }

    private void PlaySpineAnimation(string animationName, bool loop)
    {
        Component spine = FindSpineComponent(character);
        if (spine == null)
            return;
        PropertyInfo stateProperty = spine.GetType().GetProperty("AnimationState");
        object state = stateProperty == null ? null : stateProperty.GetValue(spine, null);
        if (state == null)
            return;
        MethodInfo setAnimation = state.GetType().GetMethod(
            "SetAnimation", new[] { typeof(int), typeof(string), typeof(bool) });
        if (setAnimation != null)
            setAnimation.Invoke(state, new object[] { 0, animationName, loop });
    }

    private static Component FindSpineComponent(GameObject root)
    {
        if (root == null)
            return null;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].GetType().FullName == "Spine.Unity.SkeletonAnimation")
                return components[i];
        }
        return null;
    }

    private static void SetCharacterSortingOrder(GameObject root, int order)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sortingOrder = order;
    }

    private void SetCharacterVisible(bool visible)
    {
        if (character == null)
            return;
        if (!visible)
        {
            hiddenCharacterRenderers.Clear();
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                hiddenCharacterRenderers[renderers[i]] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
            return;
        }

        foreach (KeyValuePair<Renderer, bool> pair in hiddenCharacterRenderers)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }
        hiddenCharacterRenderers.Clear();
    }

    private static IEnumerator WaitForSequenceTime(float start, float seconds)
    {
        while (Time.time - start < seconds)
            yield return null;
    }

    private IEnumerator MoveCharacter(Vector3 start, Vector3 end, float seconds)
    {
        float begin = Time.time;
        while (Time.time - begin < seconds)
        {
            float t = Mathf.Clamp01((Time.time - begin) / seconds);
            character.transform.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        character.transform.localPosition = end;
    }

    private void ResetPreview()
    {
        StopAllCoroutines();
        if (videoPlayer != null)
            videoPlayer.Stop();
        SetVideoVisible(false);
        SetTimelineVisualsVisible(true);
        ClearEffects();
        busy = false;
        if (character != null)
        {
            SetCharacterVisible(true);
            character.transform.localPosition = characterStart;
            PlaySpineAnimation("idle", true);
        }
        showUi = true;
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.None);
        PrepareTimelineEffect();
    }

    private void ClearEffects()
    {
        RecycleActiveTimeline();
        for (int i = 0; i < spawnedEffects.Count; i++)
        {
            if (spawnedEffects[i] != null)
                Destroy(spawnedEffects[i]);
        }
        spawnedEffects.Clear();
    }

    private void Log(string message)
    {
        Debug.Log("[Skill11300018 Original] " + message);
        messages.Add(message);
        if (messages.Count > 140)
            messages.RemoveAt(0);
    }

    private static string JoinFirst(List<string> values, int count)
    {
        int take = Mathf.Min(values.Count, count);
        string[] result = new string[take];
        for (int i = 0; i < take; i++)
            result[i] = values[i];
        return string.Join(", ", result);
    }

    private void OnGUI()
    {
        if (suppressBuiltInUi || !showUi)
            return;
        float width = Mathf.Min(390f, Screen.width - 32f);
        float left = Mathf.Max(16f, Screen.width - width - 16f);
        GUILayout.BeginArea(new Rect(left, 16f, width, Screen.height - 32f), GUI.skin.box);
        GUILayout.Label("labi 11300018 — Original AssetBundle Preview");
        GUILayout.Label(loading ? "Loading..." : "Bundles: " + bundles.Count + "/" + bundleFileCount);
        GUI.enabled = !loading && !busy && character != null;
        if (GUILayout.Button("普通攻击", GUILayout.Height(34f)))
            BeginNormalAttack();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("爆气", GUILayout.Height(34f)))
            BeginSkill();
        if (GUILayout.Button("奥义", GUILayout.Height(34f)))
            BeginUltimate();
        GUILayout.EndHorizontal();
        GUI.enabled = !loading;
        if (GUILayout.Button("Reset / Idle"))
            ResetPreview();
        GUI.enabled = true;
        GUILayout.Space(6f);
        GUILayout.Label("A 普通攻击 · S 爆气 · U 奥义 · I 重置 · H 面板");
        GUILayout.Label("普通攻击特效比例: " + normalEffectScale.ToString("0.00"));
        normalEffectScale = GUILayout.HorizontalSlider(normalEffectScale, 0.02f, 1.2f);
        GUILayout.Label("命中特效比例: " + impactEffectScale.ToString("0.000"));
        impactEffectScale = GUILayout.HorizontalSlider(impactEffectScale, 0.005f, 0.2f);
        useUltimateVideo = GUILayout.Toggle(useUltimateVideo, "使用原始 3.83 秒奥义视频");
        GUILayout.Label("奥义特效比例: " + ultimateEffectScale.ToString("0.00"));
        ultimateEffectScale = GUILayout.HorizontalSlider(ultimateEffectScale, 0.1f, 0.8f);
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < messages.Count; i++)
            GUILayout.Label(messages[i]);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.None);
        if (videoPlayer != null)
            videoPlayer.Stop();
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
        if (videoMaterial != null)
            Destroy(videoMaterial);
        if (videoBackdropMaterial != null)
            Destroy(videoBackdropMaterial);
        if (activeTimelineContainer != null)
            Destroy(activeTimelineContainer);
        if (warmedTimelineContainer != null)
            Destroy(warmedTimelineContainer);
        foreach (KeyValuePair<string, AssetBundle> pair in bundles)
        {
            if (pair.Value != null)
                pair.Value.Unload(false);
        }
        bundles.Clear();
        SkillResourceRegistry.Clear();
    }
}
