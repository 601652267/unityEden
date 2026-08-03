using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace EdenGallery
{
    public sealed class EdenBattleController : MonoBehaviour
    {
        private const string SpecialHeroId = "11300018";
        private const string EnemyId = "12010002";
        private const string BattlefieldId = "1001";
        private const string BattlefieldBundleDirectory =
            "EdenBattleOriginal";
        private const string BattlefieldBundleFile =
            "m_battlescene_1001.aab";

        private static readonly Vector3 OriginalHeroPosition =
            new Vector3(-18f, -4f, 0f);
        private static readonly Vector3 OriginalEnemyPosition =
            new Vector3(18f, -4f, 0f);
        private const float OriginalCardScale = 1.2f;

        private readonly List<UnityObject> ownedObjects = new List<UnityObject>();

        private Camera battleCamera;
        private EdenRecoveredBattlePreview heroPreview;
        private EdenBattleHeroPreview genericHeroPreview;
        private string heroId = SpecialHeroId;
        private string heroName = "拉比";
        private SkeletonAnimation enemy;
        private Transform enemyRoot;
        private SpriteRenderer backgroundRenderer;
        private Texture2D backgroundTexture;
        private Sprite backgroundSprite;
        private GameObject originalBattlefield;
        private Texture2D roundedTexture;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallLabelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle versusStyle;
        private float lastAspect = -1f;
        private string statusText = "正在准备战斗…";
        private Coroutine enemyReaction;
        private bool ultimatePresentationActive;
        private bool originalBattlefieldWasActive;
        private bool backgroundRendererWasEnabled;
        private bool enemyWasActive;
        private bool savedUltimatePresentationState;
        private Color cameraColorBeforeUltimate;
        private Vector3 cameraPositionBeforeUltimate;
        private Vector3 enemyPositionBeforeUltimate;
        private Vector3 enemyScaleBeforeUltimate;

        private void Awake()
        {
            battleCamera = Camera.main;
            if (battleCamera == null)
                battleCamera = FindObjectOfType<Camera>();
            if (battleCamera != null)
            {
                battleCamera.orthographic = true;
                battleCamera.orthographicSize = 25f;
                battleCamera.transform.position =
                    new Vector3(0f, 28f, -95f);
                battleCamera.transform.rotation =
                    Quaternion.Euler(15f, 0f, 0f);
                battleCamera.backgroundColor =
                    new Color(0.17f, 0.36f, 0.53f, 1f);
            }

            CreateBackground();

            ResolveSelectedHero();
            CreateHero();

            CreateEnemy();
        }

        private void ResolveSelectedHero()
        {
            string requestedId = EdenGallerySceneNavigation.BattleCardId;
            if (string.IsNullOrEmpty(requestedId))
                requestedId = EdenGallerySceneNavigation.CardId;
            if (!string.IsNullOrEmpty(requestedId))
                heroId = requestedId;

            string requestedName =
                EdenGallerySceneNavigation.BattleDisplayName;
            if (!string.IsNullOrEmpty(requestedName))
                heroName = requestedName;
            else if (!string.Equals(
                heroId,
                SpecialHeroId,
                StringComparison.Ordinal))
            {
                heroName = heroId;
            }
        }

        private void CreateHero()
        {
            if (EdenRecoveredSkillConfiguration.Supports(heroId))
            {
                heroPreview =
                    gameObject.AddComponent<EdenRecoveredBattlePreview>();
                heroPreview.Configure(heroId);
                heroPreview.suppressBuiltInUi = true;
                heroPreview.autoHideUiDuringPlayback = false;
                heroPreview.useRecoveredBattleLayout = true;
                heroPreview.useOriginalGameBattleLayout = true;
                heroPreview.useUltimateVideo = true;
                heroPreview.UltimatePresentationChanged +=
                    HandleUltimatePresentationChanged;
                heroPreview.ApkUltimateHitTriggered +=
                    HandleApkUltimateHit;
                return;
            }

            genericHeroPreview =
                gameObject.AddComponent<EdenBattleHeroPreview>();
            genericHeroPreview.Configure(heroId);
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return LoadOriginalBattlefield();
            yield return FitEnemyToBattleSide();
            statusText = "战斗准备完成";
        }

        private void Update()
        {
            if (battleCamera != null &&
                !Mathf.Approximately(lastAspect, battleCamera.aspect))
            {
                FitBackground();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                ReturnToCharacterDetails();
            if (Input.GetKeyDown(KeyCode.A))
                PlayNormalAttack();
            if (Input.GetKeyDown(KeyCode.S))
                PlayBurst();
            if (Input.GetKeyDown(KeyCode.U))
                PlayUltimate();
        }

        private void CreateBackground()
        {
            if (battleCamera == null)
                return;

            const int width = 96;
            const int height = 54;
            backgroundTexture =
                new Texture2D(width, height, TextureFormat.RGBA32, false);
            backgroundTexture.name = "EdenBattleGradient";
            backgroundTexture.wrapMode = TextureWrapMode.Clamp;
            backgroundTexture.filterMode = FilterMode.Bilinear;

            Color theme = EdenGalleryUISettings.ThemeColor;
            theme.a = 1f;
            Color darkNavy = new Color(0.018f, 0.029f, 0.060f, 1f);
            Color topLeft = Color.Lerp(darkNavy, theme, 0.58f);
            Color bottomRight = Color.Lerp(darkNavy, theme, 0.38f);
            Color enemyTint = Color.Lerp(
                bottomRight,
                new Color(0.20f, 0.07f, 0.17f, 1f),
                0.30f);

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = 1f -
                    (float)y / Mathf.Max(height - 1, 1);
                for (int x = 0; x < width; x++)
                {
                    float horizontal =
                        (float)x / Mathf.Max(width - 1, 1);
                    float diagonal = Mathf.Clamp01(
                        (horizontal + vertical) * 0.5f);
                    diagonal =
                        diagonal * diagonal * (3f - 2f * diagonal);
                    Color color =
                        Color.Lerp(topLeft, bottomRight, diagonal);
                    color = Color.Lerp(
                        color,
                        enemyTint,
                        Mathf.SmoothStep(0f, 1f, horizontal) * 0.24f);
                    pixels[y * width + x] = color;
                }
            }
            backgroundTexture.SetPixels32(pixels);
            backgroundTexture.Apply(false, false);

            backgroundSprite = Sprite.Create(
                backgroundTexture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                32f,
                0u,
                SpriteMeshType.FullRect);
            backgroundSprite.name = "EdenBattleGradientSprite";

            GameObject background = new GameObject("BattleGradientBackground");
            background.transform.SetParent(transform, false);
            backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = -32760;
            FitBackground();
        }

        private void FitBackground()
        {
            if (battleCamera == null ||
                backgroundRenderer == null ||
                backgroundSprite == null)
            {
                return;
            }

            lastAspect = battleCamera.aspect;
            float visibleHeight = battleCamera.orthographicSize * 2f;
            float visibleWidth = visibleHeight *
                Mathf.Max(battleCamera.aspect, 0.01f);
            Bounds spriteBounds = backgroundSprite.bounds;
            backgroundRenderer.transform.position = new Vector3(
                battleCamera.transform.position.x,
                battleCamera.transform.position.y,
                battleCamera.transform.position.z) +
                battleCamera.transform.forward * 250f;
            backgroundRenderer.transform.rotation =
                battleCamera.transform.rotation;
            backgroundRenderer.transform.localScale = new Vector3(
                visibleWidth / Mathf.Max(spriteBounds.size.x, 0.01f),
                visibleHeight / Mathf.Max(spriteBounds.size.y, 0.01f),
                1f);
        }

        private IEnumerator LoadOriginalBattlefield()
        {
            string relativePath =
                BattlefieldBundleDirectory + "/" + BattlefieldBundleFile;
            AssetBundle bundle = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            string url = Application.streamingAssetsPath.TrimEnd('/') +
                "/" + relativePath;
            using (UnityWebRequest request =
                UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                yield return request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.LogWarning(
                        "原版战场资源载入失败: " + request.error);
                    yield break;
                }
                bundle = DownloadHandlerAssetBundle.GetContent(request);
            }
#else
            string path = Path.Combine(
                Application.streamingAssetsPath,
                relativePath);
            bundle = AssetBundle.LoadFromFile(path);
#endif

            if (bundle == null)
            {
                Debug.LogWarning("原版战场 AssetBundle 无法载入");
                yield break;
            }

            string[] assetNames = bundle.GetAllAssetNames();
            GameObject battlefieldPrefab = null;
            for (int i = 0; i < assetNames.Length; i++)
            {
                if (assetNames[i].IndexOf(
                        "battlescene_1001.prefab",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                battlefieldPrefab =
                    bundle.LoadAsset<GameObject>(assetNames[i]);
                if (battlefieldPrefab != null)
                    break;
            }

            if (battlefieldPrefab == null)
            {
                bundle.Unload(false);
                Debug.LogWarning("原版 BattleScene_1001 Prefab 不存在");
                yield break;
            }

            originalBattlefield =
                Instantiate(battlefieldPrefab, transform);
            originalBattlefield.name = "OriginalBattleScene_1001";
            RestoreBattlefieldMaterials(originalBattlefield);
            bundle.Unload(false);

            if (ultimatePresentationActive)
            {
                originalBattlefield.SetActive(false);
            }
            else if (backgroundRenderer != null)
            {
                backgroundRenderer.enabled = false;
            }
            Debug.Log(
                "EDEN_BATTLEFIELD_READY id=" + BattlefieldId +
                " hero=" + OriginalHeroPosition +
                " enemy=" + OriginalEnemyPosition);
        }

        private void RestoreBattlefieldMaterials(GameObject battlefield)
        {
            if (battlefield == null)
                return;

            Shader shader =
                Shader.Find("SkillRestore/Battle Scene Unlit");
            Renderer[] renderers =
                battlefield.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Material[] materials =
                    renderers[rendererIndex].materials;
                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                        continue;

                    Texture2D texture = LoadBattlefieldTexture(
                        material.name);
                    if (shader != null)
                        material.shader = shader;
                    if (texture != null)
                    {
                        // 原版 1001 战场的材质会在 X 轴使用 3～5 倍 UV，
                        // 依靠 Repeat 从大场景中截取当前镜头范围。Unity
                        // 默认导入的 Clamp 会把越界 UV 拉成最后一列像素，
                        // 最终表现成覆盖整屏的横向色带。
                        texture.wrapModeU = TextureWrapMode.Repeat;
                        texture.wrapModeV = TextureWrapMode.Clamp;
                        material.mainTexture = texture;
                    }
                    if (material.HasProperty("_Color"))
                        material.SetColor("_Color", Color.white);
                    ownedObjects.Add(material);
                }
            }
        }

        private static Texture2D LoadBattlefieldTexture(
            string materialName)
        {
            string normalized = (materialName ?? string.Empty)
                .Replace(" (Instance)", string.Empty)
                .ToLowerInvariant();
            string textureName;
            if (normalized.IndexOf("1001_sky") >= 0)
                textureName = "1001_sky";
            else if (normalized.IndexOf("1001_cloud") >= 0)
                textureName = "1001_cloud";
            else if (normalized.IndexOf("1001_ground") >= 0)
                textureName = "1001_ground";
            else if (normalized.IndexOf("1001_near") >= 0)
                textureName = "1001_mid2";
            else if (normalized.IndexOf("1001_far") >= 0)
                textureName = "1001_far";
            else if (normalized.IndexOf("1001_mid") >= 0)
                textureName = "1001_mid";
            else
                return null;

            return Resources.Load<Texture2D>(
                "EdenBattle/Battlefields/1001/" + textureName);
        }

        private void CreateEnemy()
        {
            enemyRoot = new GameObject("Enemy_12010002").transform;
            enemyRoot.SetParent(transform, false);

            EdenGalleryLayer layer = new EdenGalleryLayer();
            layer.name = "CardSpine_12010002";
            layer.atlasPath =
                "EdenBattle/Enemies/12010002/CardSpine_12010002.atlas";
            layer.skeletonPath =
                "EdenBattle/Enemies/12010002/CardSpine_12010002.skel";
            layer.texturePaths = new[]
            {
                "EdenBattle/Enemies/12010002/CardSpine_12010002"
            };
            layer.animationName = "idle";
            layer.displayScale = 1f;
            layer.roleLayer = true;
            layer.useCustomSortingOrder = true;
            layer.sortingOrder = 30;

            try
            {
                enemy = EdenGallerySpineFactory.Create(
                    layer,
                    enemyRoot,
                    30,
                    ownedObjects);
                PlayEnemyLoop();
            }
            catch (Exception exception)
            {
                statusText = "怪物资源载入失败";
                Debug.LogException(exception);
            }
        }

        private IEnumerator FitEnemyToBattleSide()
        {
            if (enemy == null)
                yield break;

            yield return null;
            yield return null;
            enemyRoot.position = OriginalEnemyPosition;
            enemyRoot.localScale = new Vector3(
                -OriginalCardScale,
                OriginalCardScale,
                OriginalCardScale);
        }

        private void PlayNormalAttack()
        {
            if (!CanPlayAction(HasNormalAttack()))
                return;

            if (heroPreview != null)
            {
                heroPreview.BeginNormalAttack();
                BeginEnemyReaction(
                    heroPreview.NormalHitTimes,
                    0.48f);
            }
            else if (genericHeroPreview == null ||
                !genericHeroPreview.BeginNormalAttack())
            {
                return;
            }
            else
            {
                BeginEnemyReaction(new[] { 0.45f }, 0.45f);
            }
            statusText = "普通攻击";
        }

        private void PlayBurst()
        {
            if (!CanPlayAction(HasBurst()))
                return;

            if (heroPreview != null)
            {
                heroPreview.BeginSkill();
                BeginEnemyReaction(
                    heroPreview.BurstHitTimes,
                    0.52f);
            }
            else if (genericHeroPreview == null ||
                !genericHeroPreview.BeginBurst())
            {
                return;
            }
            else
            {
                BeginEnemyReaction(new[] { 0.55f }, 0.48f);
            }
            statusText = "爆气";
        }

        private void PlayUltimate()
        {
            if (!CanPlayAction(HasUltimate()))
                return;

            if (heroPreview != null)
            {
                heroPreview.BeginUltimate();
            }
            else if (genericHeroPreview == null ||
                !genericHeroPreview.BeginUltimate())
            {
                return;
            }
            else
            {
                BeginEnemyReaction(new[] { 0.70f }, 0.55f);
            }
            statusText = "奥义";
        }

        private void HandleApkUltimateHit(
            int hitIndex,
            string defenderState,
            bool isFinal)
        {
            if (heroPreview == null)
                return;

            statusText = "奥义 " + hitIndex + "/" +
                heroPreview.UltimateTotalHitCount +
                (isFinal ? "（最后一击）" : string.Empty);
            PlayEnemyAnimation(
                new[]
                {
                    defenderState,
                    "hurt",
                    "hit",
                    "stun",
                    "fall_down",
                    "damage"
                },
                false);
        }

        private void HandleUltimatePresentationChanged(
            EdenRecoveredBattlePreview.UltimatePresentationPhase phase)
        {
            if (phase ==
                EdenRecoveredBattlePreview.UltimatePresentationPhase.None)
            {
                RestoreBattlePresentation();
                return;
            }

            BeginUltimatePresentation();
            HideBattlefieldForUltimate();

            if (phase ==
                EdenRecoveredBattlePreview.UltimatePresentationPhase.Defender)
            {
                ShowEnemyAtUltimateCenter();
            }
            else
            {
                SetEnemyVisible(false);
            }
        }

        private void BeginUltimatePresentation()
        {
            if (ultimatePresentationActive)
                return;

            ultimatePresentationActive = true;
            savedUltimatePresentationState = true;
            originalBattlefieldWasActive =
                originalBattlefield != null &&
                originalBattlefield.activeSelf;
            backgroundRendererWasEnabled =
                backgroundRenderer != null &&
                backgroundRenderer.enabled;
            enemyWasActive =
                enemyRoot != null &&
                enemyRoot.gameObject.activeSelf;

            if (enemyRoot != null)
            {
                enemyPositionBeforeUltimate =
                    enemyRoot.position;
                enemyScaleBeforeUltimate =
                    enemyRoot.localScale;
            }
            if (battleCamera != null)
            {
                cameraColorBeforeUltimate =
                    battleCamera.backgroundColor;
                cameraPositionBeforeUltimate =
                    battleCamera.transform.position;
                battleCamera.backgroundColor = Color.black;
            }
        }

        private void HideBattlefieldForUltimate()
        {
            if (originalBattlefield != null)
                originalBattlefield.SetActive(false);
            if (backgroundRenderer != null)
                backgroundRenderer.enabled = false;
        }

        private void ShowEnemyAtUltimateCenter()
        {
            if (enemyRoot == null)
                return;

            SetEnemyVisible(true);
            // The recovered ultimate Timeline is authored against the
            // original defender coordinate near X=18. Keep the defender at
            // that coordinate and pan the camera instead of moving the
            // skeleton away from the hit effects.
            enemyRoot.position = enemyPositionBeforeUltimate;
            enemyRoot.localScale = enemyScaleBeforeUltimate;
            if (battleCamera != null)
            {
                battleCamera.transform.position = new Vector3(
                    enemyRoot.position.x,
                    cameraPositionBeforeUltimate.y,
                    cameraPositionBeforeUltimate.z);
            }
            PlayEnemyLoop();
        }

        private void SetEnemyVisible(bool visible)
        {
            if (enemyRoot != null)
                enemyRoot.gameObject.SetActive(visible);
        }

        private void RestoreBattlePresentation()
        {
            if (!savedUltimatePresentationState)
            {
                ultimatePresentationActive = false;
                return;
            }

            if (originalBattlefield != null)
                originalBattlefield.SetActive(
                    originalBattlefieldWasActive);
            if (backgroundRenderer != null)
                backgroundRenderer.enabled =
                    backgroundRendererWasEnabled;
            if (battleCamera != null)
            {
                battleCamera.backgroundColor =
                    cameraColorBeforeUltimate;
                battleCamera.transform.position =
                    cameraPositionBeforeUltimate;
            }
            if (enemyRoot != null)
            {
                enemyRoot.position =
                    enemyPositionBeforeUltimate;
                enemyRoot.localScale =
                    enemyScaleBeforeUltimate;
                enemyRoot.gameObject.SetActive(enemyWasActive);
                if (enemyWasActive)
                    PlayEnemyLoop();
            }

            savedUltimatePresentationState = false;
            ultimatePresentationActive = false;
        }

        private bool CanPlayAction(bool actionAvailable)
        {
            if (!actionAvailable)
                return false;

            if (!IsHeroReady())
            {
                statusText = GetHeroLoadingStatus();
                return false;
            }
            if (IsHeroBusy())
            {
                statusText = "请等待当前动作结束";
                return false;
            }
            return true;
        }

        private bool IsHeroReady()
        {
            return heroPreview != null
                ? heroPreview.IsReady
                : genericHeroPreview != null && genericHeroPreview.IsReady;
        }

        private bool IsHeroBusy()
        {
            return heroPreview != null
                ? heroPreview.IsBusy
                : genericHeroPreview != null && genericHeroPreview.IsBusy;
        }

        private string GetHeroLoadingStatus()
        {
            if (heroPreview != null)
                return heroPreview.LoadingStatus;
            if (genericHeroPreview != null)
                return genericHeroPreview.LoadingStatus;
            return heroId + " 战斗控制器不可用";
        }

        private bool HasNormalAttack()
        {
            return heroPreview != null ||
                (genericHeroPreview != null &&
                 genericHeroPreview.HasNormalAttack);
        }

        private bool HasBurst()
        {
            return heroPreview != null ||
                (genericHeroPreview != null && genericHeroPreview.HasBurst);
        }

        private bool HasUltimate()
        {
            return heroPreview != null ||
                (genericHeroPreview != null && genericHeroPreview.HasUltimate);
        }

        private void BeginEnemyReaction(float[] hitTimes, float recovery)
        {
            if (enemyReaction != null)
                StopCoroutine(enemyReaction);
            enemyReaction = StartCoroutine(
                PlayEnemyReaction(hitTimes, recovery));
        }

        private IEnumerator PlayEnemyReaction(
            float[] hitTimes,
            float recovery)
        {
            float previous = 0f;
            for (int i = 0; i < hitTimes.Length; i++)
            {
                float wait = Mathf.Max(0f, hitTimes[i] - previous);
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);
                PlayEnemyHit();
                previous = hitTimes[i];
            }
            yield return new WaitForSeconds(recovery);
            PlayEnemyLoop();
            enemyReaction = null;
        }

        private void PlayEnemyLoop()
        {
            PlayEnemyAnimation(
                new[] { "idle", "wait", "stand", "float_up", "walk" },
                true);
        }

        private void PlayEnemyHit()
        {
            PlayEnemyAnimation(
                new[] { "hurt", "hit", "stun", "fall_down", "damage" },
                false);
        }

        private void PlayEnemyAnimation(string[] preferredNames, bool loop)
        {
            if (enemy == null ||
                enemy.AnimationState == null ||
                enemy.Skeleton == null)
            {
                return;
            }

            SkeletonData data = enemy.Skeleton.Data;
            string selected = string.Empty;
            for (int nameIndex = 0;
                 nameIndex < preferredNames.Length &&
                 string.IsNullOrEmpty(selected);
                 nameIndex++)
            {
                Spine.Animation animation =
                    data.FindAnimation(preferredNames[nameIndex]);
                if (animation != null)
                    selected = animation.Name;
            }

            if (string.IsNullOrEmpty(selected) &&
                data.Animations != null &&
                data.Animations.Count > 0)
            {
                selected = data.Animations.Items[0].Name;
            }
            if (!string.IsNullOrEmpty(selected))
                enemy.AnimationState.SetAnimation(0, selected, loop);
        }

        private void ReturnToCharacterDetails()
        {
            SceneManager.LoadScene(
                EdenGallerySceneNavigation.CharacterDetailsSceneName,
                LoadSceneMode.Single);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            roundedTexture = CreateRoundedTexture(32, 6f);
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.alignment = TextAnchor.MiddleLeft;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 18;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            smallLabelStyle = new GUIStyle(GUI.skin.label);
            smallLabelStyle.fontSize = 13;
            smallLabelStyle.normal.textColor =
                new Color(0.80f, 0.86f, 0.95f, 1f);
            smallLabelStyle.alignment = TextAnchor.MiddleCenter;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 18;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            buttonStyle.normal.background = roundedTexture;
            buttonStyle.hover.background = roundedTexture;
            buttonStyle.active.background = roundedTexture;
            buttonStyle.border = new RectOffset(8, 8, 8, 8);

            versusStyle = new GUIStyle(labelStyle);
            versusStyle.fontSize = 28;
        }

        private static Texture2D CreateRoundedTexture(int size, float radius)
        {
            Texture2D texture =
                new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "EdenBattleRounded";
            Color32[] pixels = new Color32[size * size];
            float max = size - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(
                        radius - x,
                        x - (max - radius));
                    float dy = Mathf.Max(
                        radius - y,
                        y - (max - radius));
                    float distance = Mathf.Sqrt(
                        Mathf.Max(0f, dx) * Mathf.Max(0f, dx) +
                        Mathf.Max(0f, dy) * Mathf.Max(0f, dy));
                    float alpha = Mathf.Clamp01(radius + 0.7f - distance);
                    pixels[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                rect,
                roundedTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (ultimatePresentationActive)
                return;

            EnsureStyles();
            float width = Screen.width;
            float height = Screen.height;
            float margin = Mathf.Clamp(width * 0.018f, 14f, 28f);
            float headerHeight = Mathf.Clamp(height * 0.105f, 64f, 86f);

            Rect backRect = new Rect(
                margin,
                (headerHeight - 50f) * 0.5f,
                54f,
                50f);
            DrawPanel(backRect, new Color(0.045f, 0.075f, 0.13f, 0.96f));
            GUI.Label(backRect, "‹", versusStyle);
            if (GUI.Button(backRect, GUIContent.none, GUIStyle.none))
                ReturnToCharacterDetails();

            GUI.Label(
                new Rect(
                    backRect.xMax + 14f,
                    0f,
                    width * 0.42f,
                    headerHeight),
                "人物战斗",
                titleStyle);

            Rect statusRect = new Rect(
                width * 0.5f - 230f,
                14f,
                460f,
                36f);
            DrawPanel(statusRect, new Color(0.035f, 0.06f, 0.11f, 0.82f));
            string currentStatus = !IsHeroReady()
                ? GetHeroLoadingStatus()
                : (heroPreview != null
                    ? "APK 原逻辑版 · " + statusText
                    : "角色战斗 · " + statusText);
            GUI.Label(statusRect, currentStatus, smallLabelStyle);

            float sideWidth = Mathf.Min(330f, width * 0.25f);
            Rect heroNameRect = new Rect(
                margin,
                headerHeight,
                sideWidth,
                58f);
            Rect enemyNameRect = new Rect(
                width - margin - sideWidth,
                headerHeight,
                sideWidth,
                58f);
            DrawPanel(heroNameRect, new Color(0.035f, 0.06f, 0.11f, 0.90f));
            DrawPanel(enemyNameRect, new Color(0.035f, 0.06f, 0.11f, 0.90f));
            GUI.Label(
                new Rect(
                    heroNameRect.x,
                    heroNameRect.y + 2f,
                    heroNameRect.width,
                    28f),
                heroName,
                labelStyle);
            GUI.Label(
                new Rect(
                    heroNameRect.x,
                    heroNameRect.y + 28f,
                    heroNameRect.width,
                    24f),
                heroId,
                smallLabelStyle);
            GUI.Label(
                new Rect(
                    enemyNameRect.x,
                    enemyNameRect.y + 2f,
                    enemyNameRect.width,
                    28f),
                "训练怪物",
                labelStyle);
            GUI.Label(
                new Rect(
                    enemyNameRect.x,
                    enemyNameRect.y + 28f,
                    enemyNameRect.width,
                    24f),
                EnemyId,
                smallLabelStyle);

            GUI.Label(
                new Rect(
                    width * 0.5f - 38f,
                    headerHeight + 18f,
                    76f,
                    48f),
                "VS",
                versusStyle);

            float controlsHeight = Mathf.Clamp(
                height * 0.105f,
                66f,
                86f);
            Rect controlsPanel = new Rect(
                width * 0.5f - Mathf.Min(390f, width * 0.34f),
                height - controlsHeight - 16f,
                Mathf.Min(780f, width * 0.68f),
                controlsHeight);
            DrawPanel(
                controlsPanel,
                new Color(0.025f, 0.045f, 0.085f, 0.92f));

            float buttonGap = 10f;
            float buttonWidth =
                (controlsPanel.width - 32f - buttonGap * 2f) / 3f;
            float buttonHeight = controlsPanel.height - 20f;
            Rect normalRect = new Rect(
                controlsPanel.x + 16f,
                controlsPanel.y + 10f,
                buttonWidth,
                buttonHeight);
            Rect burstRect = new Rect(
                normalRect.xMax + buttonGap,
                normalRect.y,
                buttonWidth,
                buttonHeight);
            Rect ultimateRect = new Rect(
                burstRect.xMax + buttonGap,
                normalRect.y,
                buttonWidth,
                buttonHeight);

            bool ready = IsHeroReady() && !IsHeroBusy();
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor =
                new Color(0.20f, 0.38f, 0.70f, 1f);
            GUI.enabled = ready && HasNormalAttack();
            if (GUI.Button(normalRect, "普通攻击  [A]", buttonStyle))
                PlayNormalAttack();
            GUI.backgroundColor =
                new Color(0.42f, 0.30f, 0.78f, 1f);
            GUI.enabled = ready && HasBurst();
            if (GUI.Button(burstRect, "爆气  [S]", buttonStyle))
                PlayBurst();
            GUI.backgroundColor =
                new Color(0.76f, 0.28f, 0.48f, 1f);
            GUI.enabled = ready && HasUltimate();
            if (GUI.Button(ultimateRect, "奥义  [U]", buttonStyle))
                PlayUltimate();
            GUI.backgroundColor = previousColor;
            GUI.enabled = true;
        }

        private void OnDestroy()
        {
            if (heroPreview != null)
            {
                heroPreview.UltimatePresentationChanged -=
                    HandleUltimatePresentationChanged;
                heroPreview.ApkUltimateHitTriggered -=
                    HandleApkUltimateHit;
            }
            for (int i = 0; i < ownedObjects.Count; i++)
            {
                if (ownedObjects[i] != null)
                    Destroy(ownedObjects[i]);
            }
            ownedObjects.Clear();
            if (backgroundSprite != null)
                Destroy(backgroundSprite);
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
            if (originalBattlefield != null)
                Destroy(originalBattlefield);
            if (roundedTexture != null)
                Destroy(roundedTexture);
        }
    }
}
