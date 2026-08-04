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
using Spine;
using Spine.Unity;

public sealed class EdenRecoveredBattlePreview : MonoBehaviour
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
                return configuredCardId + " 战斗资源载入失败";
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
    private Transform ultimateProjectileAlignmentAnchor;
    private Transform ultimateMuzzleAlignmentAnchor;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private GameObject videoQuad;
    private GameObject videoBackdrop;
    private Material videoMaterial;
    private Material videoBackdropMaterial;
    private AudioSource battleVoiceSource;
    private AudioClip[] normalVoiceClips = new AudioClip[0];
    private AudioClip[] burstVoiceClips = new AudioClip[0];
    private AudioClip[] ultimateVoiceClips = new AudioClip[0];
    private int lastNormalVoiceIndex = -1;
    private int lastBurstVoiceIndex = -1;
    private int lastUltimateVoiceIndex = -1;
    private UltimatePresentationPhase ultimatePresentationPhase =
        UltimatePresentationPhase.None;
    private string configuredCardId = "11300018";
    private EdenRecoveredCharacterBattle characterBattle;
    private EdenRecoveredSkillConfiguration recoveredConfig;

    private EdenRecoveredCharacterBattle CharacterBattle
    {
        get
        {
            if (characterBattle == null)
            {
                characterBattle =
                    EdenRecoveredCharacterBattleRegistry.ForCard(
                        configuredCardId);
            }
            return characterBattle;
        }
    }

    private EdenRecoveredSkillConfiguration RecoveredConfig
    {
        get
        {
            if (recoveredConfig == null)
            {
                recoveredConfig =
                    CharacterBattle.CreateConfiguration();
            }
            return recoveredConfig;
        }
    }

    public float[] NormalHitTimes
    {
        get { return RecoveredConfig.normalHitTimes; }
    }

    public float[] BurstHitTimes
    {
        get { return RecoveredConfig.burstHitTimes; }
    }

    public int UltimateTotalHitCount
    {
        get { return RecoveredConfig.ultimateTotalHitCount; }
    }

    public void Configure(string cardId)
    {
        if (!EdenRecoveredCharacterBattleRegistry.Supports(cardId))
        {
            throw new ArgumentException(
                "Recovered battle skill is unavailable: " + cardId);
        }
        configuredCardId = cardId;
        characterBattle =
            EdenRecoveredCharacterBattleRegistry.ForCard(cardId);
        recoveredConfig = characterBattle.CreateConfiguration();
    }

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

        stage = new GameObject(
            "Skill" + configuredCardId + "OriginalStage").transform;
        SetupBattleVoice();
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

        string directory = Path.Combine(
            Application.streamingAssetsPath,
            RecoveredConfig.BundleDirectoryName);
#if UNITY_ANDROID && !UNITY_EDITOR
        string[] paths = null;
        string manifestUrl = GetStreamingAssetUrl(
            RecoveredConfig.BundleDirectoryName + "/manifest.json");
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
                RecoveredConfig.BundleDirectoryName + "/" + file);
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
        GameObject prefab = LoadPrefab(
            RecoveredConfig.CharacterBundleName,
            "cardspine_" + configuredCardId + ".prefab");
        if (prefab == null)
        {
            Log("Original character prefab was not found.");
            return;
        }

        int rebound = SkillResourceRegistry.RestorePrefab(prefab);
        GameObject container = new GameObject("LabiCharacterContainer");
        container.transform.SetParent(stage, false);
        container.SetActive(false);
        character = Instantiate(prefab, container.transform);
        character.name =
            "CardSpine_" + configuredCardId + "_Original";
        character.transform.localPosition = characterStart;
        character.transform.localScale *= characterScale;
        container.SetActive(true);
        SetCharacterSortingOrder(character, 40);
        PlaySpineAnimation("idle", true);
        Log("Original character instantiated; rebound=" + rebound);
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
        List<string> files = new List<string>();
        files.AddRange(RecoveredConfig.normalEffectBundles);
        files.AddRange(RecoveredConfig.burstEffectBundles);
        files.Add(RecoveredConfig.TimelineBundleName);
        files.AddRange(CharacterBattle.AdditionalInspectionBundles);
        for (int i = 0; i < files.Count; i++)
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
        PlayRandomBattleVoice(
            normalVoiceClips,
            ref lastNormalVoiceIndex);
        Log("APK normal effects=" +
            RecoveredConfig.normalEffectBundles.Length);

        float sequenceStart = Time.time;

        // Start the approach first. Effect start times are absolute offsets
        // from sequenceStart, so a melee character can move while this
        // coroutine waits for the authored slash/impact moment. With the
        // default delay of 0f this still starts movement and effects in the
        // same frame, preserving every older character's timing.
        if (RecoveredConfig.normalMovesToTarget)
        {
            StartCoroutine(MoveCharacter(
                characterStart,
                attackApproach,
                0.24f));
        }
        if (RecoveredConfig.normalEffectStartTime > 0f)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.normalEffectStartTime);
        }
        for (int index = 0;
             index < RecoveredConfig.normalEffectBundles.Length;
            index++)
        {
            SpawnMappedAttackEffect(
                RecoveredConfig.normalEffectBundles[index],
                RecoveredConfig.normalPrimaryEffectAtCaster);
        }
        if (RecoveredConfig.normalMovesToTarget)
        {
            bool hideAndTeleportAfterEffects =
                RecoveredConfig.normalReturnMode ==
                EdenRecoveredAttackReturnMode
                    .HideAndTeleportAfterEffects;
            bool hideAndTeleportAtReturnTime =
                RecoveredConfig.normalReturnMode ==
                EdenRecoveredAttackReturnMode
                    .HideAndTeleportAtReturnTime;
            float returnTime = RecoveredConfig.normalReturnTime;
            if (hideAndTeleportAfterEffects)
            {
                // cleanupTime is the recovered point at which the action's
                // visible effect is considered complete. A melee character
                // using the invisible reset must never leave before it.
                returnTime = Mathf.Max(
                    returnTime,
                    RecoveredConfig.normalCleanupTime);
            }

            // Both timestamps are absolute offsets from sequenceStart rather
            // than delays relative to the effect spawn time.
            yield return WaitForSequenceTime(
                sequenceStart,
                returnTime);
            if (hideAndTeleportAfterEffects ||
                hideAndTeleportAtReturnTime)
            {
                // The AfterEffects mode clears first because its configured
                // return point is also the effect completion point. The
                // AtReturnTime mode deliberately leaves decorative particles
                // alive; the common cleanup below removes them later.
                if (hideAndTeleportAfterEffects)
                    ClearEffects();
                yield return HideAndTeleportCharacterHome();
            }
            else
            {
                Vector3 returnFrom =
                    character.transform.localPosition;
                yield return MoveCharacter(
                    returnFrom,
                    characterStart,
                    0.28f);
            }
        }
        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.normalCleanupTime);
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
        PlayRandomBattleVoice(
            burstVoiceClips,
            ref lastBurstVoiceIndex);
        Log("APK burst effects=" +
            RecoveredConfig.burstEffectBundles.Length);

        float sequenceStart = Time.time;

        // Burst movement follows the same ordering as normal attacks: begin
        // moving first, then create the effects at the per-character absolute
        // start time. A zero-valued field retains the legacy same-frame spawn.
        if (RecoveredConfig.burstMovesToTarget)
        {
            StartCoroutine(MoveCharacter(
                characterStart,
                attackApproach,
                0.26f));
        }
        if (RecoveredConfig.burstEffectStartTime > 0f)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.burstEffectStartTime);
        }
        for (int index = 0;
             index < RecoveredConfig.burstEffectBundles.Length;
            index++)
        {
            SpawnMappedAttackEffect(
                RecoveredConfig.burstEffectBundles[index],
                RecoveredConfig.burstPrimaryEffectAtCaster);
        }
        if (RecoveredConfig.burstMovesToTarget)
        {
            bool hideAndTeleportAfterEffects =
                RecoveredConfig.burstReturnMode ==
                EdenRecoveredAttackReturnMode
                    .HideAndTeleportAfterEffects;
            bool hideAndTeleportAtReturnTime =
                RecoveredConfig.burstReturnMode ==
                EdenRecoveredAttackReturnMode
                    .HideAndTeleportAtReturnTime;
            float returnTime = RecoveredConfig.burstReturnTime;
            if (hideAndTeleportAfterEffects)
            {
                returnTime = Mathf.Max(
                    returnTime,
                    RecoveredConfig.burstCleanupTime);
            }

            yield return WaitForSequenceTime(
                sequenceStart,
                returnTime);
            if (hideAndTeleportAfterEffects ||
                hideAndTeleportAtReturnTime)
            {
                if (hideAndTeleportAfterEffects)
                    ClearEffects();
                yield return HideAndTeleportCharacterHome();
            }
            else
            {
                Vector3 returnFrom =
                    character.transform.localPosition;
                yield return MoveCharacter(
                    returnFrom,
                    characterStart,
                    0.30f);
            }
        }
        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.burstCleanupTime);
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
        PlayRandomBattleVoice(
            ultimateVoiceClips,
            ref lastUltimateVoiceIndex);
        Log("APK ultimate logic + " +
            RecoveredConfig.timelineName);

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
        float processedAttackerCueTime = 0f;
        if (timeline != null &&
            CharacterBattle.UsesUltimateProjectileAlignment)
        {
            StartCoroutine(
                AlignRecoveredUltimateProjectile(sequenceStart));
        }
        if (playCinematic)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.ultimateVideoStartTime);
            SetCharacterVisible(
                IsUltimateAttackerVisibleAt(
                    RecoveredConfig.ultimateVideoStartTime));
            SetTimelineVisualsVisible(false);
            SetUltimatePresentationPhase(
                UltimatePresentationPhase.Video);
            videoPlayer.time = 0.0;
            videoPlayer.Play();
            SetVideoVisible(true);

            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.ultimateVideoEndTime);
            SetVideoVisible(false);
            if (videoPlayer.isPlaying)
                videoPlayer.Pause();
            SetTimelineVisualsVisible(true);
            processedAttackerCueTime =
                RecoveredConfig.ultimateVideoEndTime;
            SetCharacterVisible(
                IsUltimateAttackerVisibleAt(
                    processedAttackerCueTime));
            SetUltimatePresentationPhase(
                UltimatePresentationPhase.Preparation);
        }

        if (RecoveredConfig.ultimateAttackerInvisibleTime >
            processedAttackerCueTime)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.ultimateAttackerInvisibleTime);
            SetCharacterVisible(false);
        }

        if (RecoveredConfig.ultimateAttackerReappearTime >= 0f)
        {
            if (RecoveredConfig.ultimateAttackerReappearTime >
                processedAttackerCueTime)
            {
                yield return WaitForSequenceTime(
                    sequenceStart,
                    RecoveredConfig.ultimateAttackerReappearTime);
                SetCharacterVisible(true);
            }
            if (RecoveredConfig.ultimateAttackerSecondInvisibleTime >
                processedAttackerCueTime)
            {
                yield return WaitForSequenceTime(
                    sequenceStart,
                    RecoveredConfig.ultimateAttackerSecondInvisibleTime);
                SetCharacterVisible(false);
            }
        }

        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.ultimateDefendersVisibleTime);
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.Defender);
        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.ultimateEffectRevealTime);
        SetUltimateSwordVisualsVisible(true);
        bool characterReturned = false;
        for (int hitIndex = 0;
             hitIndex <
                RecoveredConfig.ultimateHits.Length;
             hitIndex++)
        {
            EdenRecoveredSkillHit hit =
                RecoveredConfig.ultimateHits[hitIndex];
            if (!characterReturned &&
                RecoveredConfig.ultimateReturnTime < hit.timeSeconds)
            {
                yield return WaitForSequenceTime(
                    sequenceStart,
                    RecoveredConfig.ultimateReturnTime);
                ReturnUltimateCharacterToStage();
                characterReturned = true;
            }
            yield return WaitForSequenceTime(
                sequenceStart,
                hit.timeSeconds);
            RegisterApkUltimateHit(hitIndex, hit);
        }

        if (!characterReturned)
        {
            yield return WaitForSequenceTime(
                sequenceStart,
                RecoveredConfig.ultimateReturnTime);
            ReturnUltimateCharacterToStage();
        }
        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.ultimateIdleTime);
        PlaySpineAnimation("idle", true);
        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.ultimatePresentationEndTime);
        SetUltimatePresentationPhase(
            UltimatePresentationPhase.None);

        yield return WaitForSequenceTime(
            sequenceStart,
            RecoveredConfig.ultimateCleanupTime);
        if (videoPlayer != null)
            videoPlayer.Stop();
        if (battleVoiceSource != null)
            battleVoiceSource.Stop();
        SetVideoVisible(false);
        SetTimelineVisualsVisible(true);
        RecycleActiveTimeline();
        showUi = true;
        busy = false;
    }

    private bool IsUltimateAttackerVisibleAt(float sequenceTime)
    {
        bool visible = true;
        if (RecoveredConfig.ultimateAttackerInvisibleTime >= 0f &&
            sequenceTime >=
                RecoveredConfig.ultimateAttackerInvisibleTime)
        {
            visible = false;
        }
        if (RecoveredConfig.ultimateAttackerReappearTime >= 0f &&
            sequenceTime >=
                RecoveredConfig.ultimateAttackerReappearTime)
        {
            visible = true;
        }
        if (RecoveredConfig.ultimateAttackerSecondInvisibleTime >= 0f &&
            sequenceTime >=
                RecoveredConfig.ultimateAttackerSecondInvisibleTime)
        {
            visible = false;
        }
        return visible;
    }

    private void ReturnUltimateCharacterToStage()
    {
        character.transform.localPosition = characterStart;
        SetCharacterVisible(true);
    }

    private void RegisterApkUltimateHit(
        int hitIndex,
        EdenRecoveredSkillHit hit)
    {
        int displayIndex = hitIndex + 1;
        Log("APK ultimate hit " + displayIndex + "/" +
            RecoveredConfig.ultimateTotalHitCount +
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

    private void SpawnMappedAttackEffect(
        string file,
        bool primaryEffectAtCaster)
    {
        // The *_2 prefabs carry the original absolute defender position
        // (approximately 18.5, -4). Other attack/skill prefabs have a local
        // root and must be aligned to the caster or target selected by the
        // recovered per-character configuration.
        if (file.EndsWith(
            "_2.aab",
            StringComparison.OrdinalIgnoreCase))
        {
            GameObject prefab = LoadPrefab(file, ".prefab");
            if (prefab == null)
            {
                Log("Effect prefab unavailable: " + file);
                return;
            }
            float scale = GetNormalEffectScale(file);
            Vector3 position = Vector3.zero;
            position.y = enemyFocus.y -
                prefab.transform.localPosition.y * scale;
            SpawnEffect(file, position, false);
        }
        else if (primaryEffectAtCaster)
        {
            SpawnEffectAlignedToPosition(
                file,
                character.transform.localPosition,
                false);
        }
        else
        {
            SpawnEffectAlignedToTarget(file, false);
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
        ConfigureRecoveredCharacterEffect(file, effect);

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
        return SpawnEffectAlignedToPosition(
            file,
            enemyFocus,
            timeline);
    }

    private GameObject SpawnEffectAlignedToPosition(
        string file,
        Vector3 targetPosition,
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
        Vector3 containerPosition = targetPosition - scaledPrefabRoot;
        return SpawnEffect(file, containerPosition, timeline);
    }

    /// <summary>
    /// Places a recovered effect root at a semantic Stage anchor. Offsets are
    /// expressed in the APK prefab's original coordinate units and converted
    /// with the same per-bundle scale used by SpawnEffect. Keeping all
    /// TransformPoint work here prevents character scripts from depending on
    /// the generated effect/container/Stage parent hierarchy.
    /// </summary>
    public void AlignSpawnedEffectToStageAnchor(
        GameObject effect,
        string bundleFile,
        EdenRecoveredEffectAnchor anchor,
        Vector3 prefabSpaceOffset)
    {
        if (effect == null || stage == null)
            return;

        Vector3 anchorPosition;
        switch (anchor)
        {
            case EdenRecoveredEffectAnchor.CurrentCaster:
                anchorPosition = character != null
                    ? character.transform.localPosition
                    : characterStart;
                break;
            case EdenRecoveredEffectAnchor.AttackApproach:
                anchorPosition = attackApproach;
                break;
            case EdenRecoveredEffectAnchor.EnemyFocus:
                anchorPosition = enemyFocus;
                break;
            case EdenRecoveredEffectAnchor.CharacterStart:
                anchorPosition = characterStart;
                break;
            case EdenRecoveredEffectAnchor.UltimateCast:
                anchorPosition = ultimateCastPosition;
                break;
            default:
                anchorPosition = Vector3.zero;
                break;
        }

        Vector3 stageOffset =
            prefabSpaceOffset * GetNormalEffectScale(bundleFile);
        effect.transform.position = stage.TransformPoint(
            anchorPosition + stageOffset);
    }

    /// <summary>
    /// Applies an APK-prefab-space correction to an effect that is already in
    /// the correct semantic area. This preserves previously calibrated visual
    /// placement while still performing the offset in Stage space instead of
    /// relying on a particular generated parent hierarchy.
    /// </summary>
    public void OffsetSpawnedEffectInStage(
        GameObject effect,
        string bundleFile,
        Vector3 prefabSpaceOffset)
    {
        if (effect == null || stage == null)
            return;

        Vector3 currentStagePosition =
            stage.InverseTransformPoint(effect.transform.position);
        Vector3 stageOffset =
            prefabSpaceOffset * GetNormalEffectScale(bundleFile);
        effect.transform.position = stage.TransformPoint(
            currentStagePosition + stageOffset);
    }

    private float GetNormalEffectScale(string file)
    {
        if (file.Equals("eft_fx_" + configuredCardId + "_attack_air.aab",
            StringComparison.OrdinalIgnoreCase))
            return airAttackEffectScale;
        if (file.Equals("eft_fx_" + configuredCardId + "_attack_air_hit.aab",
            StringComparison.OrdinalIgnoreCase))
            return airAttackHitEffectScale;
        if (file.Equals("eft_labi_shouji.aab",
            StringComparison.OrdinalIgnoreCase))
            return impactEffectScale;
        return normalEffectScale;
    }

    private void ConfigureRecoveredCharacterEffect(
        string file,
        GameObject effect)
    {
        CharacterBattle.ConfigureSpawnedEffect(this, file, effect);
    }

    internal static bool RendererUsesTexture(
        Renderer renderer,
        string textureName)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int index = 0; index < materials.Length; index++)
        {
            Material material = materials[index];
            if (material == null || material.mainTexture == null)
                continue;
            if (string.Equals(
                material.mainTexture.name,
                textureName,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    internal void StartWeaponEffectFollower(
        List<Transform> targets,
        float duration,
        EdenRecoveredWeaponAnchor weaponAnchor,
        string file)
    {
        Vector3 centre = Vector3.zero;
        for (int index = 0; index < targets.Count; index++)
            centre += targets[index].position;
        centre /= targets.Count;

        Vector3[] offsets = new Vector3[targets.Count];
        for (int index = 0; index < targets.Count; index++)
            offsets[index] = targets[index].position - centre;

        Vector3 weaponHead;
        if (TryGetWeaponHeadWorld(
            weaponAnchor,
            out weaponHead))
        {
            MoveWeaponEffectTargets(targets, offsets, weaponHead);
        }
        StartCoroutine(FollowWeaponEffect(
            targets,
            offsets,
            weaponAnchor,
            duration));
        Log("Weapon FX follows staff head: " + file +
            " nodes=" + targets.Count +
            " edge=" +
            (weaponAnchor.useFirstEdge ? "first" : "second") +
            " offset=" + weaponAnchor.worldOffset);
    }

    private IEnumerator FollowWeaponEffect(
        List<Transform> targets,
        Vector3[] offsets,
        EdenRecoveredWeaponAnchor weaponAnchor,
        float duration)
    {
        float endTime = Time.time + duration;
        while (Time.time < endTime)
        {
            yield return new WaitForEndOfFrame();
            Vector3 weaponHead;
            if (!TryGetWeaponHeadWorld(
                weaponAnchor,
                out weaponHead))
            {
                continue;
            }
            MoveWeaponEffectTargets(
                targets,
                offsets,
                weaponHead);
        }
    }

    private static void MoveWeaponEffectTargets(
        List<Transform> targets,
        Vector3[] offsets,
        Vector3 weaponHead)
    {
        for (int index = 0; index < targets.Count; index++)
        {
            if (targets[index] != null)
                targets[index].position = weaponHead + offsets[index];
        }
    }

    private bool TryGetWeaponHeadWorld(
        EdenRecoveredWeaponAnchor weaponAnchor,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (character == null)
            return false;
        SkeletonAnimation animation =
            character.GetComponentInChildren<SkeletonAnimation>(true);
        if (animation == null || animation.Skeleton == null)
            return false;

        ExposedList<Slot> slots = animation.Skeleton.Slots;
        for (int index = 0; index < slots.Count; index++)
        {
            Slot slot = slots.Items[index];
            RegionAttachment attachment =
                slot.Attachment as RegionAttachment;
            if (attachment == null)
                continue;
            string slotName = slot.Data.Name ?? string.Empty;
            string attachmentName = attachment.Name ?? string.Empty;
            if ((slotName + "/" + attachmentName).IndexOf(
                weaponAnchor.attachmentNameFragment,
                StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            float[] vertices = new float[8];
            attachment.ComputeWorldVertices(
                slot.Bone,
                vertices,
                0);
            int firstVertex = weaponAnchor.useFirstEdge ? 0 : 4;
            int secondVertex = weaponAnchor.useFirstEdge ? 2 : 6;
            Vector3 skeletonPosition = new Vector3(
                (vertices[firstVertex] +
                 vertices[secondVertex]) * 0.5f,
                (vertices[firstVertex + 1] +
                 vertices[secondVertex + 1]) * 0.5f,
                0f);
            worldPosition =
                animation.transform.TransformPoint(skeletonPosition) +
                weaponAnchor.worldOffset;
            return true;
        }
        return false;
    }

    private void PrepareRecoveredUltimateAlignment(GameObject timeline)
    {
        ultimateProjectileAlignmentAnchor = null;
        ultimateMuzzleAlignmentAnchor = null;
        if (timeline == null ||
            !CharacterBattle.UsesUltimateProjectileAlignment)
        {
            return;
        }

        Transform projectile = FindTransformByName(
            timeline.transform,
            CharacterBattle.UltimateProjectileObjectName);
        Transform muzzle = FindTransformByName(
            timeline.transform,
            CharacterBattle.UltimateMuzzleObjectName);
        ultimateProjectileAlignmentAnchor =
            CreateAlignmentAnchor(projectile);
        ultimateMuzzleAlignmentAnchor =
            CreateAlignmentAnchor(muzzle);
    }

    private static Transform CreateAlignmentAnchor(Transform target)
    {
        if (target == null || target.parent == null)
            return null;
        GameObject anchorObject = new GameObject(
            target.name + "_RecoveredAlignment");
        Transform anchor = anchorObject.transform;
        anchor.SetParent(target.parent, false);
        anchor.localPosition = Vector3.zero;
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        target.SetParent(anchor, false);
        return anchor;
    }

    private static Transform FindTransformByName(
        Transform root,
        string objectName)
    {
        if (root == null)
            return null;
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (string.Equals(
                transforms[index].name,
                objectName,
                StringComparison.Ordinal))
            {
                return transforms[index];
            }
        }
        return null;
    }

    private IEnumerator AlignRecoveredUltimateProjectile(
        float sequenceStart)
    {
        yield return WaitForSequenceTime(
            sequenceStart,
            CharacterBattle.UltimateProjectileAlignmentTime);
        yield return new WaitForEndOfFrame();
        Vector3 weaponHead;
        if (!TryGetWeaponHeadWorld(
            CharacterBattle.UltimateWeaponAnchor,
            out weaponHead))
        {
            Log("Ultimate staff head unavailable at projectile launch.");
            yield break;
        }

        float projectileOffset = AlignAnchorToY(
            ultimateProjectileAlignmentAnchor,
            weaponHead.y);
        float muzzleOffset = AlignAnchorToY(
            ultimateMuzzleAlignmentAnchor,
            weaponHead.y);
        Log("Ultimate projectile aligned to staff head y=" +
            weaponHead.y.ToString("0.00") +
            " projectileDelta=" + projectileOffset.ToString("0.00") +
            " muzzleDelta=" + muzzleOffset.ToString("0.00"));
    }

    private static float AlignAnchorToY(
        Transform anchor,
        float targetY)
    {
        if (anchor == null || anchor.childCount == 0)
            return 0f;
        Transform animatedChild = anchor.GetChild(0);
        float offset = targetY - animatedChild.position.y;
        anchor.position += new Vector3(0f, offset, 0f);
        return offset;
    }

    private void ResetRecoveredUltimateAlignment()
    {
        if (ultimateProjectileAlignmentAnchor != null)
            ultimateProjectileAlignmentAnchor.localPosition = Vector3.zero;
        if (ultimateMuzzleAlignmentAnchor != null)
            ultimateMuzzleAlignmentAnchor.localPosition = Vector3.zero;
    }

    private void PrepareTimelineEffect()
    {
        if (warmedTimelineContainer != null || stage == null ||
            !bundles.ContainsKey(RecoveredConfig.TimelineBundleName))
            return;

        GameObject prefab = LoadPrefab(
            RecoveredConfig.TimelineBundleName,
            ".prefab");
        if (prefab == null)
            return;
        int rebound = SkillResourceRegistry.RestorePrefab(prefab);
        GameObject container = new GameObject(prefab.name + "_WarmContainer");
        container.transform.SetParent(stage, false);
        container.transform.localPosition = GetTimelineContainerPosition();
        container.transform.localScale = Vector3.zero;
        container.SetActive(false);
        GameObject effect = Instantiate(prefab, container.transform);
        effect.name = prefab.name + "_WarmedOriginal";
        SkillResourceRegistry.RestorePrefab(effect);
        PrepareRecoveredUltimateAlignment(effect);
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

        container.transform.localPosition = GetTimelineContainerPosition();
        container.transform.localScale = Vector3.one * ultimateEffectScale;
        ResetRecoveredUltimateAlignment();
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
        activeTimelineContainer.transform.localPosition =
            GetTimelineContainerPosition();
        activeTimelineContainer.transform.localScale = Vector3.one * ultimateEffectScale;
        ResetRecoveredUltimateAlignment();
        warmedTimelineContainer = activeTimelineContainer;
        warmedTimelineEffect = activeTimelineEffect;
        activeTimelineContainer = null;
        activeTimelineEffect = null;
    }

    private Vector3 GetTimelineContainerPosition()
    {
        return timelineOrigin + new Vector3(
            0f,
            RecoveredConfig.timelineContainerYOffset,
            0f);
    }

    private void SetupBattleVoice()
    {
        normalVoiceClips = LoadVoiceClips(
            RecoveredConfig.normalVoiceResources);
        burstVoiceClips = LoadVoiceClips(
            RecoveredConfig.burstVoiceResources);
        ultimateVoiceClips = LoadVoiceClips(
            RecoveredConfig.ultimateVoiceResources);
        if (normalVoiceClips.Length == 0 &&
            burstVoiceClips.Length == 0 &&
            ultimateVoiceClips.Length == 0)
        {
            return;
        }

        battleVoiceSource = stage.gameObject.AddComponent<AudioSource>();
        battleVoiceSource.playOnAwake = false;
        battleVoiceSource.loop = false;
        battleVoiceSource.spatialBlend = 0f;
        battleVoiceSource.volume = 1f;
        battleVoiceSource.priority = 64;
        Log("Battle voices ready normal=" + normalVoiceClips.Length +
            " burst=" + burstVoiceClips.Length +
            " ultimate=" + ultimateVoiceClips.Length);
    }

    private static AudioClip[] LoadVoiceClips(string[] resourcePaths)
    {
        if (resourcePaths == null || resourcePaths.Length == 0)
            return new AudioClip[0];

        List<AudioClip> clips = new List<AudioClip>();
        for (int index = 0; index < resourcePaths.Length; index++)
        {
            AudioClip clip = Resources.Load<AudioClip>(
                resourcePaths[index]);
            if (clip != null)
                clips.Add(clip);
            else
                Debug.LogWarning(
                    "Recovered battle voice is missing: " +
                    resourcePaths[index]);
        }
        return clips.ToArray();
    }

    private void PlayRandomBattleVoice(
        AudioClip[] clips,
        ref int lastIndex)
    {
        if (battleVoiceSource == null || clips == null ||
            clips.Length == 0)
        {
            return;
        }

        int selectedIndex = UnityEngine.Random.Range(0, clips.Length);
        if (clips.Length > 1 && selectedIndex == lastIndex)
            selectedIndex = (selectedIndex + 1) % clips.Length;
        lastIndex = selectedIndex;
        battleVoiceSource.Stop();
        battleVoiceSource.clip = clips[selectedIndex];
        battleVoiceSource.Play();
    }

    private void SetupVideo()
    {
        string path = GetStreamingAssetUrl(
            RecoveredConfig.VideoDirectoryName +
            "/" + RecoveredConfig.videoFileName);
#if !UNITY_ANDROID || UNITY_EDITOR
        if (!File.Exists(path))
        {
            Log("Ultimate cinematic is missing: " + path);
            return;
        }
#endif

        GameObject videoObject = new GameObject(
            "Skill" + configuredCardId + "OriginalVideoPlayer");
        videoObject.transform.SetParent(stage, false);
        videoPlayer = videoObject.AddComponent<VideoPlayer>();
        videoTexture = new RenderTexture(1650, 750, 0, RenderTextureFormat.ARGB32);
        videoTexture.name =
            "Skill" + configuredCardId + "OriginalVideoTexture";
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
        videoBackdrop.name =
            "Skill" + configuredCardId + "CinematicBackdrop";
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
        videoQuad.name =
            "Skill" + configuredCardId + "OriginalCinematicScreen";
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
        videoMaterial.name =
            "Skill" + configuredCardId + "OriginalVideoMaterial";
        videoMaterial.mainTexture = videoTexture;
        videoMaterial.renderQueue = 3998;
        MeshRenderer videoRenderer =
            videoQuad.GetComponent<MeshRenderer>();
        videoRenderer.sharedMaterial = videoMaterial;
        videoRenderer.sortingOrder = VideoSortingOrder;
        SetVideoVisible(false);
        videoPlayer.Prepare();
        Log("Preparing original ultimate cinematic window=" +
            RecoveredConfig.ultimateVideoStartTime.ToString("0.00") +
            "-" +
            RecoveredConfig.ultimateVideoEndTime.ToString("0.00") +
            "s.");
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
            // Hiding is intentionally idempotent. During 11301023's
            // ultimate the video window has already hidden the attacker by
            // the time the Lua's explicit 2000ms hide cue is processed. A
            // second snapshot would record the disabled state and make the
            // character impossible to restore after the ultimate.
            if (hiddenCharacterRenderers.Count > 0)
                return;
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

    /// <summary>
    /// Performs the non-visible return used by recovered melee characters.
    /// The first yield guarantees that the hidden state reaches a rendered
    /// frame before the position changes. The character is then teleported to
    /// its original position and changed to idle while still hidden, so the
    /// player never sees either a backward slide or the final frozen attack
    /// pose at the home position.
    /// </summary>
    private IEnumerator HideAndTeleportCharacterHome()
    {
        SetCharacterVisible(false);
        yield return null;

        character.transform.localPosition = characterStart;
        PlaySpineAnimation("idle", true);
        SetCharacterVisible(true);
    }

    private void ResetPreview()
    {
        StopAllCoroutines();
        if (videoPlayer != null)
            videoPlayer.Stop();
        if (battleVoiceSource != null)
            battleVoiceSource.Stop();
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

    internal void LogCharacterDetail(string message)
    {
        Log(message);
    }

    private void Log(string message)
    {
        Debug.Log("[Skill" + configuredCardId + " Original] " + message);
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
        GUILayout.Label(
            configuredCardId + " — Original AssetBundle Preview");
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
        useUltimateVideo = GUILayout.Toggle(
            useUltimateVideo,
            "使用原始奥义视频");
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
        if (battleVoiceSource != null)
            battleVoiceSource.Stop();
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
