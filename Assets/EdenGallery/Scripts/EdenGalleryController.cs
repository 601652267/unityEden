using System;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Networking;
using UnityObject = UnityEngine.Object;

namespace EdenGallery
{
    public sealed class EdenGalleryController : MonoBehaviour
    {
        public static EdenGalleryController Instance { get; private set; }

        public int CharacterIndex { get { return characterIndex; } }
        public int StageIndex { get { return stageIndex; } }
        public EdenGalleryCharacter CurrentCharacter
        {
            get
            {
                return manifest != null && manifest.characters != null &&
                       characterIndex >= 0 && characterIndex < manifest.characters.Length
                    ? manifest.characters[characterIndex]
                    : null;
            }
        }
        public EdenGalleryStage CurrentStage
        {
            get
            {
                EdenGalleryCharacter character = CurrentCharacter;
                return character != null && character.stages != null &&
                       stageIndex >= 0 && stageIndex < character.stages.Length
                    ? character.stages[stageIndex]
                    : null;
            }
        }

        public event Action<EdenGalleryCharacter, EdenGalleryStage> StageChanged;

        private const string ManifestResourcePath = "EdenGallery/gallery";
        private const string VoiceCatalogResourcePath = "EdenGallery/voice_catalog";
        private const string CharacterIndexKey = "EdenGallery.CharacterIndex";
        private const string StageIndexKey = "EdenGallery.StageIndex";
        private const string NameLanguageKey = "EdenGallery.NameLanguage";
        private const string FavoriteCharactersKey = "EdenGallery.FavoriteCharacterIds";

        private readonly List<UnityObject> ownedObjects = new List<UnityObject>();
        private readonly List<SpriteRenderer> fullscreenSprites = new List<SpriteRenderer>();
        private readonly List<Transform> roleSpineLayers = new List<Transform>();
        private readonly HashSet<string> favoriteCharacterIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, EdenGalleryVoiceCatalogEntry> voiceEntriesByFolder =
            new Dictionary<string, EdenGalleryVoiceCatalogEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> nextVoiceLineByFolder =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private EdenGalleryManifest manifest;
        private EdenGalleryVoiceCatalog voiceCatalog;
        private Camera galleryCamera;
        private GameObject stageRoot;
        private Transform spineRoot;
        private SpriteRenderer backgroundRenderer;
        private GameObject gradientBackgroundObject;
        private SpriteRenderer gradientBackgroundRenderer;
        private Texture2D gradientBackgroundTexture;
        private Sprite gradientBackgroundSprite;
        private Color gradientThemeColor;
        private Coroutine loadRoutine;
        private int characterIndex;
        private int stageIndex;
        private string errorMessage = string.Empty;
        private string loadingMessage = string.Empty;
        private int fittedScreenWidth;
        private int fittedScreenHeight;
        private Bounds fittedBounds;
        private bool hasFittedBounds;
        private Vector2 characterScroll;
        private Vector2 touchStart;
        private bool trackingSwipe;
        private Texture2D[] portraitTextures;
        private Texture2D roundedMaskTexture;
        private Texture2D subtleRoundedMaskTexture;
        private Texture2D circleMaskTexture;
        private Texture2D settingsIconTexture;
        private bool uiVisible = true;
        private bool settingsVisible;
        private EdenGalleryVoiceImportService voiceImportService;
        private AudioSource voiceAudioSource;
        private Coroutine voicePlaybackRoutine;
        private AudioClip activeVoiceClip;
        private EdenGalleryVoiceLine activeVoiceLine;
        private int voicePlaybackSerial;
        private bool trackingSceneVoiceTap;
        private Vector2 sceneVoiceTapStart;
        private bool copyingVoiceArchive;
        private float voiceArchiveCopyProgress = -1f;
        private string voiceArchiveCopyStatus = string.Empty;
        private bool trackingCharacterStripDrag;
        private Vector2 characterStripDragStart;
        private float characterStripScrollStart;
        private bool characterStripDragged;
        private float suppressCharacterClickUntil;
        private float characterStripMaxScroll;
        private int scrollTargetCharacterIndex = -1;
        private int[] characterDisplayOrder = new int[0];
        private int[] characterDisplayPositions = new int[0];

        private GUIStyle titleStyle;
        private GUIStyle statusStyle;
        private GUIStyle characterNameStyle;
        private GUIStyle namePlateNameStyle;
        private GUIStyle namePlateIdStyle;
        private GUIStyle circleButtonStyle;
        private GUIStyle toggleButtonStyle;
        private GUIStyle favoriteButtonStyle;
        private GUIStyle settingsTitleStyle;
        private GUIStyle settingsSectionStyle;
        private GUIStyle settingsBodyStyle;
        private GUIStyle settingsButtonStyle;
        private GUIStyle voiceSubtitleStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            TextAsset manifestAsset = Resources.Load<TextAsset>(ManifestResourcePath);
            if (manifestAsset == null)
            {
                errorMessage = "找不到 Resources/" + ManifestResourcePath + ".json";
                return;
            }

            try
            {
                manifest = JsonUtility.FromJson<EdenGalleryManifest>(manifestAsset.text);
                if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                    throw new InvalidOperationException("角色清单为空。");
                portraitTextures = new Texture2D[manifest.characters.Length];
                LoadFavoriteCharacters();
                RebuildCharacterDisplayOrder();
                EdenGalleryUISettings.NameLanguage =
                    PlayerPrefs.GetInt(NameLanguageKey, 0) == 1
                        ? EdenGalleryNameLanguage.Japanese
                        : EdenGalleryNameLanguage.Chinese;
                voiceImportService = new EdenGalleryVoiceImportService();
                LoadVoiceCatalog();
                SetupVoicePlayback();
            }
            catch (Exception exception)
            {
                errorMessage = "角色清单解析失败：" + exception.Message;
                return;
            }

            SetupCamera();
        }

        private void Start()
        {
#if UNITY_ANDROID || UNITY_IOS
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                return;
            int savedCharacter = Mathf.Clamp(PlayerPrefs.GetInt(CharacterIndexKey, 0), 0, manifest.characters.Length - 1);
            int savedStage = Mathf.Max(PlayerPrefs.GetInt(StageIndexKey, 0), 0);
            LoadCharacter(savedCharacter, savedStage);
        }

        private void Update()
        {
            if (gradientBackgroundTexture != null &&
                gradientThemeColor != EdenGalleryUISettings.ThemeColor)
            {
                RefreshGradientBackgroundTexture();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
                PreviousCharacter();
            if (Input.GetKeyDown(KeyCode.RightArrow))
                NextCharacter();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                LoadStage(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                LoadStage(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                LoadStage(2);

            HandleCharacterStripDrag();
            HandleSwipe();
            HandleSceneVoiceTap();

            if (hasFittedBounds &&
                (Screen.width != fittedScreenWidth || Screen.height != fittedScreenHeight))
            {
                FitCamera(fittedBounds);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            StopVoicePlayback(true);
            ClearStage();
            if (roundedMaskTexture != null)
                Destroy(roundedMaskTexture);
            if (subtleRoundedMaskTexture != null)
                Destroy(subtleRoundedMaskTexture);
            if (circleMaskTexture != null)
                Destroy(circleMaskTexture);
            if (settingsIconTexture != null)
                Destroy(settingsIconTexture);
            if (gradientBackgroundObject != null)
                Destroy(gradientBackgroundObject);
            if (gradientBackgroundSprite != null)
                Destroy(gradientBackgroundSprite);
            if (gradientBackgroundTexture != null)
                Destroy(gradientBackgroundTexture);
        }

        public void LoadCharacter(int newCharacterIndex)
        {
            LoadCharacter(newCharacterIndex, 0);
        }

        public void LoadCharacter(int newCharacterIndex, int requestedStageIndex)
        {
            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                return;

            int resolvedCharacterIndex = Mathf.Clamp(
                newCharacterIndex,
                0,
                manifest.characters.Length - 1);
            if (resolvedCharacterIndex != characterIndex)
                StopVoicePlayback(true);
            characterIndex = resolvedCharacterIndex;
            scrollTargetCharacterIndex = characterIndex;
            EdenGalleryCharacter character = manifest.characters[characterIndex];
            int stageCount = character.stages == null ? 0 : character.stages.Length;
            if (stageCount == 0)
            {
                errorMessage = "角色 " + character.cardId + " 没有立绘。";
                ClearStage();
                return;
            }
            stageIndex = Mathf.Clamp(requestedStageIndex, 0, stageCount - 1);
            BeginLoadStage();
        }

        public bool LoadCharacter(string cardId, int requestedStageIndex)
        {
            if (manifest == null || manifest.characters == null || string.IsNullOrEmpty(cardId))
                return false;
            for (int i = 0; i < manifest.characters.Length; i++)
            {
                if (string.Equals(manifest.characters[i].cardId, cardId, StringComparison.Ordinal))
                {
                    LoadCharacter(i, requestedStageIndex);
                    return true;
                }
            }
            return false;
        }

        public void LoadStage(int newStageIndex)
        {
            EdenGalleryCharacter character = CurrentCharacter;
            if (character == null || character.stages == null ||
                newStageIndex < 0 || newStageIndex >= character.stages.Length ||
                newStageIndex == stageIndex)
            {
                return;
            }
            stageIndex = newStageIndex;
            BeginLoadStage();
        }

        public void PreviousCharacter()
        {
            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                return;
            int displayPosition = GetCharacterDisplayPosition(characterIndex);
            int previousPosition =
                (displayPosition - 1 + characterDisplayOrder.Length) % characterDisplayOrder.Length;
            LoadCharacter(characterDisplayOrder[previousPosition], 0);
        }

        public void NextCharacter()
        {
            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                return;
            int displayPosition = GetCharacterDisplayPosition(characterIndex);
            int nextPosition = (displayPosition + 1) % characterDisplayOrder.Length;
            LoadCharacter(characterDisplayOrder[nextPosition], 0);
        }

        private void LoadFavoriteCharacters()
        {
            favoriteCharacterIds.Clear();
            if (manifest == null || manifest.characters == null)
                return;

            string cached = PlayerPrefs.GetString(FavoriteCharactersKey, string.Empty);
            if (string.IsNullOrEmpty(cached))
                return;
            string[] cachedIds = cached.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int cachedIndex = 0; cachedIndex < cachedIds.Length; cachedIndex++)
            {
                string cachedId = cachedIds[cachedIndex].Trim();
                for (int characterIndex = 0;
                     characterIndex < manifest.characters.Length;
                     characterIndex++)
                {
                    EdenGalleryCharacter character = manifest.characters[characterIndex];
                    if (character != null &&
                        string.Equals(character.cardId, cachedId, StringComparison.Ordinal))
                    {
                        favoriteCharacterIds.Add(cachedId);
                        break;
                    }
                }
            }
        }

        private void SaveFavoriteCharacters()
        {
            List<string> orderedIds = new List<string>();
            if (manifest != null && manifest.characters != null)
            {
                for (int i = 0; i < manifest.characters.Length; i++)
                {
                    EdenGalleryCharacter character = manifest.characters[i];
                    if (character != null &&
                        favoriteCharacterIds.Contains(character.cardId))
                    {
                        orderedIds.Add(character.cardId);
                    }
                }
            }
            PlayerPrefs.SetString(FavoriteCharactersKey, string.Join(",", orderedIds.ToArray()));
            PlayerPrefs.Save();
        }

        private void RebuildCharacterDisplayOrder()
        {
            if (manifest == null || manifest.characters == null)
            {
                characterDisplayOrder = new int[0];
                characterDisplayPositions = new int[0];
                return;
            }

            List<int> order = new List<int>(manifest.characters.Length);
            for (int pass = 0; pass < 2; pass++)
            {
                bool includeFavorites = pass == 0;
                for (int characterIndex = 0;
                     characterIndex < manifest.characters.Length;
                     characterIndex++)
                {
                    EdenGalleryCharacter character = manifest.characters[characterIndex];
                    bool favorite = character != null &&
                        favoriteCharacterIds.Contains(character.cardId);
                    if (favorite == includeFavorites)
                        order.Add(characterIndex);
                }
            }

            characterDisplayOrder = order.ToArray();
            characterDisplayPositions = new int[manifest.characters.Length];
            for (int displayPosition = 0;
                 displayPosition < characterDisplayOrder.Length;
                 displayPosition++)
            {
                characterDisplayPositions[characterDisplayOrder[displayPosition]] = displayPosition;
            }
        }

        private int GetCharacterDisplayPosition(int manifestCharacterIndex)
        {
            if (manifestCharacterIndex < 0 ||
                manifestCharacterIndex >= characterDisplayPositions.Length)
            {
                return 0;
            }
            return characterDisplayPositions[manifestCharacterIndex];
        }

        private bool IsFavoriteCharacter(int manifestCharacterIndex)
        {
            return manifest != null && manifest.characters != null &&
                   manifestCharacterIndex >= 0 &&
                   manifestCharacterIndex < manifest.characters.Length &&
                   manifest.characters[manifestCharacterIndex] != null &&
                   favoriteCharacterIds.Contains(
                       manifest.characters[manifestCharacterIndex].cardId);
        }

        private void ToggleFavoriteCharacter(int manifestCharacterIndex)
        {
            if (manifest == null || manifest.characters == null ||
                manifestCharacterIndex < 0 ||
                manifestCharacterIndex >= manifest.characters.Length)
            {
                return;
            }

            EdenGalleryCharacter character = manifest.characters[manifestCharacterIndex];
            if (character == null || string.IsNullOrEmpty(character.cardId))
                return;
            if (!favoriteCharacterIds.Add(character.cardId))
                favoriteCharacterIds.Remove(character.cardId);

            SaveFavoriteCharacters();
            RebuildCharacterDisplayOrder();
            scrollTargetCharacterIndex = manifestCharacterIndex;
            GUI.changed = true;
        }

        public void SetNameLanguage(EdenGalleryNameLanguage language)
        {
            if (EdenGalleryUISettings.NameLanguage == language)
                return;
            EdenGalleryUISettings.NameLanguage = language;
            PlayerPrefs.SetInt(NameLanguageKey,
                language == EdenGalleryNameLanguage.Japanese ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ToggleNameLanguage()
        {
            SetNameLanguage(EdenGalleryUISettings.NameLanguage == EdenGalleryNameLanguage.Chinese
                ? EdenGalleryNameLanguage.Japanese
                : EdenGalleryNameLanguage.Chinese);
        }

        public void SetUIVisible(bool visible)
        {
            uiVisible = visible;
            trackingCharacterStripDrag = false;
            trackingSwipe = false;
        }

        public void ToggleUIVisible()
        {
            SetUIVisible(!uiVisible);
        }

        public void SetSettingsVisible(bool visible)
        {
            settingsVisible = visible;
            trackingCharacterStripDrag = false;
            trackingSwipe = false;
        }

        public void OnAndroidVoiceArchiveSelected(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return;
            if (string.Equals(payload, "COPYING", StringComparison.Ordinal))
            {
                copyingVoiceArchive = true;
                voiceArchiveCopyProgress = -1f;
                voiceArchiveCopyStatus = "正在把压缩包复制到应用缓存…";
                return;
            }
            if (payload.StartsWith("COPY_PROGRESS:", StringComparison.Ordinal))
            {
                string[] values = payload.Substring("COPY_PROGRESS:".Length).Split(':');
                long copiedBytes;
                long totalBytes;
                if (values.Length >= 2 &&
                    long.TryParse(values[0], out copiedBytes) &&
                    long.TryParse(values[1], out totalBytes))
                {
                    voiceArchiveCopyProgress = totalBytes > 0L
                        ? Mathf.Clamp01((float)copiedBytes / totalBytes)
                        : -1f;
                    voiceArchiveCopyStatus = totalBytes > 0L
                        ? "正在复制压缩包 " +
                          Mathf.RoundToInt(voiceArchiveCopyProgress * 100f) + "%"
                        : "正在复制压缩包（已复制 " + FormatByteCount(copiedBytes) + "）";
                }
                return;
            }
            if (payload.StartsWith("FILE:", StringComparison.Ordinal))
            {
                copyingVoiceArchive = false;
                voiceArchiveCopyProgress = -1f;
                string archivePath = payload.Substring("FILE:".Length);
                voiceArchiveCopyStatus = "正在启动语音资源导入…";
                if (voiceImportService == null)
                    voiceImportService = new EdenGalleryVoiceImportService();
                if (voiceImportService.BeginImport(archivePath, true))
                    voiceArchiveCopyStatus = string.Empty;
                else
                    voiceArchiveCopyStatus = voiceImportService.StatusMessage;
                return;
            }
            if (string.Equals(payload, "CANCELLED", StringComparison.Ordinal))
            {
                copyingVoiceArchive = false;
                voiceArchiveCopyProgress = -1f;
                voiceArchiveCopyStatus = "已取消选择语音压缩包。";
                return;
            }
            if (payload.StartsWith("ERROR:", StringComparison.Ordinal))
            {
                copyingVoiceArchive = false;
                voiceArchiveCopyProgress = -1f;
                voiceArchiveCopyStatus = "读取压缩包失败：" + payload.Substring("ERROR:".Length);
            }
        }

        private void BeginVoiceArchiveSelection()
        {
            if (copyingVoiceArchive ||
                (voiceImportService != null && voiceImportService.IsBusy))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass bridge =
                    new AndroidJavaClass("com.eden.gallery.EdenVoiceImportBridge"))
                {
                    bridge.CallStatic(
                        "open",
                        gameObject.name,
                        "OnAndroidVoiceArchiveSelected");
                }
                voiceArchiveCopyStatus = "正在打开系统文件选择器…";
            }
            catch (Exception exception)
            {
                voiceArchiveCopyStatus = "无法打开系统文件选择器：" + exception.Message;
            }
#elif UNITY_EDITOR
            string archivePath = UnityEditor.EditorUtility.OpenFilePanel(
                "选择语音 ZIP 压缩包",
                string.Empty,
                "zip");
            if (!string.IsNullOrEmpty(archivePath))
            {
                if (voiceImportService == null)
                    voiceImportService = new EdenGalleryVoiceImportService();
                voiceImportService.BeginImport(archivePath, false);
            }
            else
            {
                voiceArchiveCopyStatus = "已取消选择语音压缩包。";
            }
#else
            voiceArchiveCopyStatus = "当前平台暂不支持系统压缩包选择器。";
#endif
        }

        private bool IsVoiceImportBusy()
        {
            return copyingVoiceArchive ||
                   (voiceImportService != null && voiceImportService.IsBusy);
        }

        private float GetVoiceImportProgress()
        {
            if (copyingVoiceArchive)
                return voiceArchiveCopyProgress;
            return voiceImportService != null && voiceImportService.IsBusy
                ? voiceImportService.Progress
                : -1f;
        }

        private string GetVoiceImportStatus()
        {
            if (copyingVoiceArchive || !string.IsNullOrEmpty(voiceArchiveCopyStatus))
            {
                if (voiceImportService == null || !voiceImportService.IsBusy)
                    return voiceArchiveCopyStatus;
            }
            return voiceImportService == null
                ? "尚未导入语音资源。"
                : voiceImportService.StatusMessage;
        }

        private static string FormatByteCount(long byteCount)
        {
            if (byteCount < 1024L)
                return byteCount + " B";
            double value = byteCount;
            string[] units = { "KB", "MB", "GB", "TB" };
            int unitIndex = -1;
            do
            {
                value /= 1024d;
                unitIndex++;
            }
            while (value >= 1024d && unitIndex < units.Length - 1);
            return value.ToString(value >= 100d ? "0" : "0.0") + " " + units[unitIndex];
        }

        private void BeginLoadStage()
        {
            if (loadRoutine != null)
                StopCoroutine(loadRoutine);
            loadRoutine = StartCoroutine(LoadCurrentStage());
        }

        private IEnumerator LoadCurrentStage()
        {
            ClearStage();
            errorMessage = string.Empty;
            EdenGalleryCharacter character = CurrentCharacter;
            EdenGalleryStage stage = CurrentStage;
            if (character == null || stage == null)
                yield break;

            loadingMessage = "正在加载 " + character.cardId + " / " + stage.label;
            stageRoot = new GameObject("Stage_" + stage.folder);
            stageRoot.transform.SetParent(transform, false);
            spineRoot = new GameObject("SpineLayers").transform;
            spineRoot.SetParent(stageRoot.transform, false);
            Transform decorationRoot = new GameObject("ScreenLayers").transform;
            decorationRoot.SetParent(stageRoot.transform, false);

            try
            {
                if (!string.IsNullOrEmpty(stage.backgroundPath))
                {
                    int backgroundSortingOrder = stage.useCustomBackgroundSortingOrder
                        ? stage.backgroundSortingOrder
                        : -100;
                    backgroundRenderer = CreateSpriteRenderer(
                        stage.backgroundPath,
                        decorationRoot,
                        backgroundSortingOrder,
                        "Background");
                    ApplyDisplayTransform(
                        backgroundRenderer.transform,
                        stage.backgroundScale,
                        stage.backgroundScaleX,
                        stage.backgroundScaleY,
                        stage.backgroundOffsetX,
                        stage.backgroundOffsetY,
                        stage.backgroundOffsetZ,
                        stage.backgroundRotationX,
                        stage.backgroundRotationY,
                        stage.backgroundRotationZ);
                }

                EdenGalleryLayer[] layers = stage.layers ?? new EdenGalleryLayer[0];
                for (int i = 0; i < layers.Length; i++)
                {
                    EdenGalleryLayer layer = layers[i];
                    if (layer == null)
                        continue;
                    if (string.Equals(layer.type, "image", StringComparison.OrdinalIgnoreCase))
                    {
                        int imageSortingOrder = layer.useCustomSortingOrder
                            ? layer.sortingOrder
                            : (layer.backgroundLayer ? -20 + i : 100 + i);
                        SpriteRenderer imageRenderer = CreateSpriteRenderer(
                            layer.imagePath,
                            decorationRoot,
                            imageSortingOrder,
                            layer.name);
                        ApplyDisplayTransform(
                            imageRenderer.transform,
                            layer.displayScale,
                            layer.displayScaleX,
                            layer.displayScaleY,
                            layer.offsetX,
                            layer.offsetY,
                            layer.offsetZ,
                            layer.rotationX,
                            layer.rotationY,
                            layer.rotationZ);
                        if (layer.fullscreen)
                            fullscreenSprites.Add(imageRenderer);
                    }
                    else
                    {
                        int sortingOrder = layer.backgroundLayer ? -20 + i : 10 + i;
                        SkeletonAnimation animation = EdenGallerySpineFactory.Create(
                            layer,
                            spineRoot,
                            sortingOrder,
                            ownedObjects);
                        if (layer.roleLayer && animation != null)
                            roleSpineLayers.Add(animation.transform);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                errorMessage = "立绘加载失败：" + exception.Message;
                loadingMessage = string.Empty;
                yield break;
            }

            yield return null;
            yield return null;

            FitStage();
            loadingMessage = string.Empty;
            PlayerPrefs.SetInt(CharacterIndexKey, characterIndex);
            PlayerPrefs.SetInt(StageIndexKey, stageIndex);
            PlayerPrefs.Save();
            if (StageChanged != null)
                StageChanged(character, stage);
            loadRoutine = null;
        }

        private SpriteRenderer CreateSpriteRenderer(
            string resourcePath,
            Transform parent,
            int sortingOrder,
            string objectName)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                throw new InvalidOperationException("Missing image: " + resourcePath);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = objectName + "_RuntimeSprite";
            ownedObjects.Add(sprite);

            GameObject imageObject = new GameObject(objectName);
            imageObject.transform.SetParent(parent, false);
            SpriteRenderer renderer = imageObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void ApplyDisplayTransform(
            Transform target,
            float displayScale,
            float displayScaleX,
            float displayScaleY,
            float offsetX,
            float offsetY,
            float offsetZ,
            float rotationX,
            float rotationY,
            float rotationZ)
        {
            if (target == null)
                return;
            float resolvedScale = displayScale > 0f ? displayScale : 1f;
            float resolvedScaleX = Mathf.Abs(displayScaleX) > 0.00001f
                ? displayScaleX
                : resolvedScale;
            float resolvedScaleY = Mathf.Abs(displayScaleY) > 0.00001f
                ? displayScaleY
                : resolvedScale;
            target.localScale = new Vector3(resolvedScaleX, resolvedScaleY, resolvedScale);
            target.localPosition = new Vector3(offsetX, offsetY, offsetZ);
            target.localEulerAngles = new Vector3(rotationX, rotationY, rotationZ);
        }

        private void FitStage()
        {
            EdenGalleryStage stage = CurrentStage;
            Bounds spineBounds;
            bool hasSpineBounds = TryGetSpineBounds(out spineBounds);
            Bounds roleBounds;
            bool hasRoleBounds = TryGetRoleSpineBounds(out roleBounds);

            Bounds frameBounds = new Bounds(Vector3.zero, new Vector3(16.5f, 7.5f, 0f));
            if (backgroundRenderer != null && backgroundRenderer.sprite != null)
            {
                frameBounds = backgroundRenderer.bounds;
                frameBounds.center = Vector3.zero;
            }
            else if (fullscreenSprites.Count > 0 && fullscreenSprites[0] != null)
            {
                frameBounds = fullscreenSprites[0].bounds;
                frameBounds.center = Vector3.zero;
            }

            // Scene illustrations in the original game are authored as Fx_MainBack
            // prefabs. Their Spine transforms are registered against a 1652x752 frame,
            // so the frame — not the animated mesh bounds — controls camera fitting.
            if (UsesFrameComposition(stage))
            {
                frameBounds = new Bounds(Vector3.zero, new Vector3(16.52f, 7.52f, 0.2f));
                if (stage.autoFrameComposition && hasSpineBounds)
                    FitSpineToFrame(spineBounds, roleBounds, hasRoleBounds, frameBounds);
                fittedBounds = frameBounds;
                hasFittedBounds = true;
                FitCamera(fittedBounds);
                return;
            }

            // Spine determines the camera framing. Backgrounds are screen decorations and
            // must not make a large skeleton appear zoomed-in just because the source image
            // is only 1650x750.
            if (hasSpineBounds)
                fittedBounds = spineBounds;
            else
                fittedBounds = frameBounds;

            hasFittedBounds = true;
            FitCamera(fittedBounds);
        }

        private void FitSpineToFrame(
            Bounds compositionBounds,
            Bounds roleBounds,
            bool hasRoleBounds,
            Bounds frameBounds)
        {
            if (spineRoot == null ||
                compositionBounds.size.x <= 0.0001f ||
                compositionBounds.size.y <= 0.0001f)
                return;

            float compositionScale = Mathf.Min(
                frameBounds.size.x / compositionBounds.size.x,
                frameBounds.size.y / compositionBounds.size.y);
            Vector3 scaledCompositionCenter = compositionBounds.center * compositionScale;
            Vector3 scaledCompositionSize = compositionBounds.size * compositionScale;
            Vector3 compositionTargetCenter = new Vector3(
                frameBounds.center.x,
                frameBounds.min.y + scaledCompositionSize.y * 0.5f,
                scaledCompositionCenter.z);
            Vector3 compositionPosition = compositionTargetCenter - scaledCompositionCenter;

            float scale = compositionScale;
            Vector3 position = compositionPosition;
            if (hasRoleBounds &&
                roleBounds.size.x > 0.0001f &&
                roleBounds.size.y > 0.0001f)
            {
                // Auto-composed stages can have very large ribbons, flashes and other
                // secondary effects. Fitting their combined bounds makes the actual
                // character tiny. Use the authored role layer as the subject instead,
                // while retaining the subject's position from the safe full-composition
                // fit. Oversized effects are intentionally allowed to crop at the frame.
                float desiredRoleScale = Mathf.Min(
                    frameBounds.size.x * 0.72f / roleBounds.size.x,
                    frameBounds.size.y * 0.82f / roleBounds.size.y);
                scale = Mathf.Clamp(
                    desiredRoleScale,
                    compositionScale,
                    compositionScale * 2.15f);

                Vector3 preservedRoleCenter =
                    roleBounds.center * compositionScale + compositionPosition;
                Vector3 scaledRoleExtents = roleBounds.extents * scale;
                float minCenterX = frameBounds.min.x + scaledRoleExtents.x;
                float maxCenterX = frameBounds.max.x - scaledRoleExtents.x;
                float minCenterY = frameBounds.min.y + scaledRoleExtents.y;
                float maxCenterY = frameBounds.max.y - scaledRoleExtents.y;
                if (minCenterX <= maxCenterX)
                    preservedRoleCenter.x = Mathf.Clamp(
                        preservedRoleCenter.x,
                        minCenterX,
                        maxCenterX);
                if (minCenterY <= maxCenterY)
                    preservedRoleCenter.y = Mathf.Clamp(
                        preservedRoleCenter.y,
                        minCenterY,
                        maxCenterY);
                position = new Vector3(
                    preservedRoleCenter.x - roleBounds.center.x * scale,
                    preservedRoleCenter.y - roleBounds.center.y * scale,
                    0f);

                EdenGalleryCharacter character = CurrentCharacter;
                Debug.Log(
                    "EDEN_GALLERY_AUTO_FRAME cardId=" +
                    (character == null ? string.Empty : character.cardId) +
                    " stage=" + (CurrentStage == null ? string.Empty : CurrentStage.folder) +
                    " compositionBounds=" + compositionBounds.size +
                    " roleBounds=" + roleBounds.size +
                    " compositionScale=" + compositionScale.ToString("0.###") +
                    " roleScale=" + scale.ToString("0.###"));
            }

            spineRoot.localScale = new Vector3(scale, scale, scale);
            spineRoot.localPosition = position;
        }

        private bool TryGetRoleSpineBounds(out Bounds result)
        {
            result = new Bounds();
            bool found = false;
            for (int i = 0; i < roleSpineLayers.Count; i++)
            {
                Transform roleLayer = roleSpineLayers[i];
                Bounds layerBounds;
                if (roleLayer == null ||
                    !EdenGallerySpineBounds.TryCalculateAnimationBounds(
                        roleLayer,
                        out layerBounds))
                {
                    continue;
                }
                if (!found)
                {
                    result = layerBounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(layerBounds);
                }
            }
            return found;
        }

        private bool TryGetSpineBounds(out Bounds result)
        {
            if (EdenGallerySpineBounds.TryCalculateAnimationBounds(spineRoot, out result))
                return true;

            result = new Bounds();
            if (spineRoot == null)
                return false;

            MeshRenderer[] renderers = spineRoot.GetComponentsInChildren<MeshRenderer>(false);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (!found)
                {
                    result = renderer.bounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private void FitCamera(Bounds targetBounds)
        {
            SetupCamera();
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7777778f;
            float halfWidth = Mathf.Max(targetBounds.extents.x, 0.5f);
            float halfHeight = Mathf.Max(targetBounds.extents.y, 0.5f);
            EdenGalleryStage stage = CurrentStage;
            bool originalComposition = UsesOriginalPrefabComposition(stage);
            bool frameComposition = UsesFrameComposition(stage);
            if (frameComposition)
            {
                // Fx_MainBack scenes are wider than a 16:9 phone screen. Fill the
                // viewport with the entire authored composition as one unit, accepting
                // a small crop on the left and right instead of black bars above and
                // below. The slight overscan avoids one-pixel seams from source sizes
                // such as 1650x750 versus the 1652x752 authoring frame.
                galleryCamera.orthographicSize = Mathf.Min(
                    halfHeight,
                    halfWidth / Mathf.Max(aspect, 0.01f)) * 0.995f;
            }
            else
            {
                galleryCamera.orthographicSize = Mathf.Max(
                    halfHeight,
                    halfWidth / Mathf.Max(aspect, 0.01f)) * 1.12f;
            }
            Vector3 targetCenter = ResolveCameraCenter(targetBounds, stage);
            galleryCamera.transform.position = new Vector3(targetCenter.x, targetCenter.y, -10f);
            if (frameComposition)
                FitFrameSprites(aspect, targetCenter);
            else
                FitScreenSprites(aspect, targetCenter);
            FitGradientBackground(aspect, targetCenter);
            fittedScreenWidth = Screen.width;
            fittedScreenHeight = Screen.height;

            EdenGalleryCharacter character = CurrentCharacter;
            if (character != null && stage != null)
            {
                Debug.Log(
                    "EDEN_GALLERY_STAGE_READY cardId=" + character.cardId +
                    " stage=" + stage.folder +
                    " fitBounds=" + targetBounds.size +
                    " alignment=" + (frameComposition
                        ? (originalComposition ? "original-prefab" : "auto-frame")
                        : (IsSceneStage(stage) ? "bottom" : "center")) +
                    " fit=" + (frameComposition ? "cover" : "bounds") +
                    " screen=" + Screen.width + "x" + Screen.height +
                    " cameraSize=" + galleryCamera.orthographicSize.ToString("0.###"));
            }
        }

        private Vector3 ResolveCameraCenter(Bounds targetBounds, EdenGalleryStage stage)
        {
            Vector3 center = targetBounds.center;
            if (IsSceneStage(stage) && !UsesFrameComposition(stage))
            {
                // Scene illustrations are composed against a full-screen background.
                // Put all spare vertical fit space above the sampled Spine bounds so
                // the illustration cannot appear to float. The Spine transform itself
                // remains untouched; only the camera moves.
                center.y = targetBounds.min.y + galleryCamera.orthographicSize;
            }
            return center;
        }

        private static bool UsesOriginalPrefabComposition(EdenGalleryStage stage)
        {
            return stage != null && stage.originalPrefabComposition;
        }

        private static bool UsesFrameComposition(EdenGalleryStage stage)
        {
            return stage != null &&
                   (stage.originalPrefabComposition || stage.autoFrameComposition);
        }

        private static bool IsSceneStage(EdenGalleryStage stage)
        {
            return stage != null &&
                   (stage.sceneSized || !string.IsNullOrEmpty(stage.backgroundPath));
        }

        private void FitScreenSprites(float aspect, Vector3 targetCenter)
        {
            float visibleHeight = galleryCamera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * Mathf.Max(aspect, 0.01f);
            FitSpriteToCover(backgroundRenderer, visibleWidth, visibleHeight, targetCenter);
            for (int i = 0; i < fullscreenSprites.Count; i++)
                FitSpriteToCover(fullscreenSprites[i], visibleWidth, visibleHeight, targetCenter);
        }

        private void FitFrameSprites(float aspect, Vector3 targetCenter)
        {
            float visibleHeight = galleryCamera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * Mathf.Max(aspect, 0.01f);
            if (!RendererCoversViewport(
                    backgroundRenderer,
                    visibleWidth,
                    visibleHeight,
                    targetCenter))
            {
                FitSpriteToCover(backgroundRenderer, visibleWidth, visibleHeight, targetCenter);
            }
            for (int i = 0; i < fullscreenSprites.Count; i++)
                FitSpriteToCover(fullscreenSprites[i], visibleWidth, visibleHeight, targetCenter);
        }

        private static bool RendererCoversViewport(
            SpriteRenderer renderer,
            float visibleWidth,
            float visibleHeight,
            Vector3 targetCenter)
        {
            if (renderer == null || renderer.sprite == null)
                return false;
            const float epsilon = 0.001f;
            Bounds bounds = renderer.bounds;
            float halfWidth = visibleWidth * 0.5f;
            float halfHeight = visibleHeight * 0.5f;
            return bounds.min.x <= targetCenter.x - halfWidth + epsilon &&
                   bounds.max.x >= targetCenter.x + halfWidth - epsilon &&
                   bounds.min.y <= targetCenter.y - halfHeight + epsilon &&
                   bounds.max.y >= targetCenter.y + halfHeight - epsilon;
        }

        private static void FitSpriteToCover(
            SpriteRenderer renderer,
            float visibleWidth,
            float visibleHeight,
            Vector3 targetCenter)
        {
            if (renderer == null || renderer.sprite == null)
                return;
            Vector2 sourceSize = renderer.sprite.bounds.size;
            if (sourceSize.x <= 0f || sourceSize.y <= 0f)
                return;
            float coverScale = Mathf.Max(visibleWidth / sourceSize.x, visibleHeight / sourceSize.y);
            Vector3 position = renderer.transform.position;
            renderer.transform.position = new Vector3(targetCenter.x, targetCenter.y, position.z);
            renderer.transform.localScale = new Vector3(coverScale, coverScale, 1f);
        }

        private void EnsureGradientBackground()
        {
            if (gradientBackgroundRenderer != null)
                return;

            const int textureSize = 96;
            gradientBackgroundTexture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                true);
            gradientBackgroundTexture.name = "EdenGalleryThemeGradient";
            gradientBackgroundTexture.wrapMode = TextureWrapMode.Clamp;
            gradientBackgroundTexture.filterMode = FilterMode.Bilinear;
            gradientBackgroundSprite = Sprite.Create(
                gradientBackgroundTexture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize,
                0u,
                SpriteMeshType.FullRect);
            gradientBackgroundSprite.name = "EdenGalleryThemeGradientSprite";

            gradientBackgroundObject = new GameObject("ThemeGradientBackground");
            gradientBackgroundObject.transform.SetParent(transform, false);
            gradientBackgroundRenderer =
                gradientBackgroundObject.AddComponent<SpriteRenderer>();
            gradientBackgroundRenderer.sprite = gradientBackgroundSprite;
            gradientBackgroundRenderer.sortingOrder = -32760;
            RefreshGradientBackgroundTexture();
        }

        private void RefreshGradientBackgroundTexture()
        {
            if (gradientBackgroundTexture == null)
                return;

            gradientThemeColor = EdenGalleryUISettings.ThemeColor;
            gradientThemeColor.a = 1f;
            Color darkNavy = new Color(0.018f, 0.029f, 0.060f, 1f);
            Color topLeft = Color.Lerp(darkNavy, gradientThemeColor, 0.60f);
            Color bottomRight = Color.Lerp(darkNavy, gradientThemeColor, 0.34f);
            int width = gradientBackgroundTexture.width;
            int height = gradientBackgroundTexture.height;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = 1f - (float)y / Mathf.Max(height - 1, 1);
                for (int x = 0; x < width; x++)
                {
                    float horizontal = (float)x / Mathf.Max(width - 1, 1);
                    float diagonal = Mathf.Clamp01((horizontal + vertical) * 0.5f);
                    diagonal = diagonal * diagonal * (3f - 2f * diagonal);
                    pixels[y * width + x] = Color.Lerp(topLeft, bottomRight, diagonal);
                }
            }
            gradientBackgroundTexture.SetPixels32(pixels);
            gradientBackgroundTexture.Apply(false, false);
            if (galleryCamera != null)
                galleryCamera.backgroundColor = bottomRight;
        }

        private void FitGradientBackground(float aspect, Vector3 targetCenter)
        {
            EnsureGradientBackground();
            if (gradientBackgroundRenderer == null || galleryCamera == null)
                return;

            float visibleHeight = galleryCamera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * Mathf.Max(aspect, 0.01f);
            gradientBackgroundObject.transform.position = new Vector3(
                targetCenter.x,
                targetCenter.y,
                20f);
            gradientBackgroundObject.transform.localScale = new Vector3(
                visibleWidth * 1.02f,
                visibleHeight * 1.02f,
                1f);
        }

        private void SetupCamera()
        {
            if (galleryCamera == null)
                galleryCamera = Camera.main;
            if (galleryCamera == null)
            {
                GameObject cameraObject = new GameObject("EdenGalleryCamera");
                galleryCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            galleryCamera.orthographic = true;
            galleryCamera.clearFlags = CameraClearFlags.SolidColor;
            galleryCamera.nearClipPlane = 0.1f;
            galleryCamera.farClipPlane = 100f;
            galleryCamera.transform.position = new Vector3(0f, 0f, -10f);
            EnsureGradientBackground();
        }

        private void LoadVoiceCatalog()
        {
            voiceEntriesByFolder.Clear();
            TextAsset catalogAsset = Resources.Load<TextAsset>(VoiceCatalogResourcePath);
            if (catalogAsset == null)
            {
                Debug.LogWarning(
                    "EdenGallery voice catalog is missing at Resources/" +
                    VoiceCatalogResourcePath + ".json",
                    this);
                return;
            }

            try
            {
                voiceCatalog = JsonUtility.FromJson<EdenGalleryVoiceCatalog>(catalogAsset.text);
                EdenGalleryVoiceCatalogEntry[] entries =
                    voiceCatalog == null ? null : voiceCatalog.entries;
                if (entries == null)
                    return;
                for (int i = 0; i < entries.Length; i++)
                {
                    EdenGalleryVoiceCatalogEntry entry = entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.folder) ||
                        entry.lines == null || entry.lines.Length == 0)
                    {
                        continue;
                    }
                    voiceEntriesByFolder[entry.folder] = entry;
                }
            }
            catch (Exception exception)
            {
                voiceCatalog = null;
                voiceEntriesByFolder.Clear();
                Debug.LogWarning("EdenGallery voice catalog parse failed: " + exception.Message, this);
            }
        }

        private void SetupVoicePlayback()
        {
            if (voiceAudioSource != null)
                return;
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;
            voiceAudioSource.spatialBlend = 0f;
        }

        private void ClearStage()
        {
            hasFittedBounds = false;
            backgroundRenderer = null;
            spineRoot = null;
            fullscreenSprites.Clear();
            roleSpineLayers.Clear();

            if (stageRoot != null)
            {
                stageRoot.SetActive(false);
                Destroy(stageRoot);
                stageRoot = null;
            }

            for (int i = 0; i < ownedObjects.Count; i++)
            {
                if (ownedObjects[i] != null)
                    Destroy(ownedObjects[i]);
            }
            ownedObjects.Clear();
        }

        private void HandleCharacterStripDrag()
        {
            if (!uiVisible || settingsVisible ||
                manifest == null || manifest.characters == null)
                return;

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                    BeginCharacterStripDrag(touch.position);
                else if (trackingCharacterStripDrag &&
                         (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
                    UpdateCharacterStripDrag(touch.position);
                else if (trackingCharacterStripDrag &&
                         (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                    EndCharacterStripDrag(touch.position);
                return;
            }

            if (Input.GetMouseButtonDown(0))
                BeginCharacterStripDrag(Input.mousePosition);
            else if (trackingCharacterStripDrag && Input.GetMouseButton(0))
                UpdateCharacterStripDrag(Input.mousePosition);
            else if (trackingCharacterStripDrag && Input.GetMouseButtonUp(0))
                EndCharacterStripDrag(Input.mousePosition);
        }

        private void BeginCharacterStripDrag(Vector2 screenPosition)
        {
            float bottomHeight = GetBottomBarHeight(Screen.height);
            float toggleRailWidth = GetToggleRailWidth(Screen.width);
            if (screenPosition.y > bottomHeight ||
                screenPosition.x > Screen.width - toggleRailWidth)
            {
                return;
            }
            trackingCharacterStripDrag = true;
            characterStripDragged = false;
            characterStripDragStart = screenPosition;
            characterStripScrollStart = characterScroll.x;
        }

        private void UpdateCharacterStripDrag(Vector2 screenPosition)
        {
            float deltaX = screenPosition.x - characterStripDragStart.x;
            if (Mathf.Abs(deltaX) > 7f)
                characterStripDragged = true;
            characterScroll.x = Mathf.Clamp(
                characterStripScrollStart - deltaX,
                0f,
                characterStripMaxScroll);
            characterScroll.y = 0f;
            if (characterStripDragged)
                scrollTargetCharacterIndex = -1;
        }

        private void EndCharacterStripDrag(Vector2 screenPosition)
        {
            UpdateCharacterStripDrag(screenPosition);
            if (characterStripDragged)
                suppressCharacterClickUntil = Time.unscaledTime + 0.16f;
            trackingCharacterStripDrag = false;
        }

        private void HandleSwipe()
        {
            if (settingsVisible || trackingCharacterStripDrag)
            {
                trackingSwipe = false;
                return;
            }
            if (Input.touchCount != 1)
            {
                trackingSwipe = false;
                return;
            }

            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began && touch.position.y > 160f && touch.position.y < Screen.height - 140f)
            {
                touchStart = touch.position;
                trackingSwipe = true;
            }
            else if (trackingSwipe && touch.phase == TouchPhase.Ended)
            {
                float deltaX = touch.position.x - touchStart.x;
                if (Mathf.Abs(deltaX) > Mathf.Max(80f, Screen.width * 0.12f))
                {
                    if (deltaX > 0f)
                        PreviousCharacter();
                    else
                        NextCharacter();
                }
                trackingSwipe = false;
            }
        }

        private void HandleSceneVoiceTap()
        {
            if (settingsVisible || manifest == null || manifest.characters == null)
            {
                trackingSceneVoiceTap = false;
                return;
            }

            float dragThreshold = Mathf.Max(18f, Screen.width * 0.018f);
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    trackingSceneVoiceTap = IsSceneVoiceTapPosition(touch.position);
                    sceneVoiceTapStart = touch.position;
                }
                else if (trackingSceneVoiceTap &&
                         (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
                {
                    if (Vector2.Distance(sceneVoiceTapStart, touch.position) > dragThreshold)
                        trackingSceneVoiceTap = false;
                }
                else if (trackingSceneVoiceTap && touch.phase == TouchPhase.Ended)
                {
                    bool shouldPlay =
                        Vector2.Distance(sceneVoiceTapStart, touch.position) <= dragThreshold &&
                        IsSceneVoiceTapPosition(touch.position);
                    trackingSceneVoiceTap = false;
                    if (shouldPlay)
                        PlayNextCurrentStageVoice();
                }
                else if (touch.phase == TouchPhase.Canceled)
                {
                    trackingSceneVoiceTap = false;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                trackingSceneVoiceTap = IsSceneVoiceTapPosition(Input.mousePosition);
                sceneVoiceTapStart = Input.mousePosition;
            }
            else if (trackingSceneVoiceTap && Input.GetMouseButton(0) &&
                     Vector2.Distance(sceneVoiceTapStart, (Vector2)Input.mousePosition) > dragThreshold)
            {
                trackingSceneVoiceTap = false;
            }
            else if (trackingSceneVoiceTap && Input.GetMouseButtonUp(0))
            {
                Vector2 endPosition = Input.mousePosition;
                bool shouldPlay =
                    Vector2.Distance(sceneVoiceTapStart, endPosition) <= dragThreshold &&
                    IsSceneVoiceTapPosition(endPosition);
                trackingSceneVoiceTap = false;
                if (shouldPlay)
                    PlayNextCurrentStageVoice();
            }
        }

        private bool IsSceneVoiceTapPosition(Vector2 screenPosition)
        {
            if (settingsVisible)
                return false;

            float guiX = screenPosition.x;
            float guiY = Screen.height - screenPosition.y;
            if (!uiVisible)
            {
                float bottomHeight = GetBottomBarHeight(Screen.height);
                float railWidth = GetToggleRailWidth(Screen.width);
                Rect columnRect = new Rect(
                    Screen.width - railWidth - 16f,
                    Screen.height - bottomHeight + 16f,
                    railWidth,
                    bottomHeight - 32f);
                Rect settingsRect;
                Rect visibilityRect;
                GetControlColumnRects(columnRect, out settingsRect, out visibilityRect);
                return !settingsRect.Contains(new Vector2(guiX, guiY)) &&
                       !visibilityRect.Contains(new Vector2(guiX, guiY));
            }

            float bottomBarHeight = GetBottomBarHeight(Screen.height);
            if (guiY >= Screen.height - bottomBarHeight)
                return false;

            float namePlateWidth = Mathf.Clamp(Screen.width * 0.205f, 220f, 320f);
            float namePlateHeight = Mathf.Clamp(Screen.height * 0.13f, 88f, 112f);
            Rect namePlateRect = new Rect(22f, 20f, namePlateWidth, namePlateHeight);
            Vector2 guiPosition = new Vector2(guiX, guiY);
            if (namePlateRect.Contains(guiPosition))
                return false;

            EdenGalleryCharacter character = CurrentCharacter;
            if (character != null && character.stages != null)
            {
                float circleSize = Mathf.Clamp(Screen.height * 0.078f, 54f, 68f);
                float circleGap = Mathf.Clamp(circleSize * 0.22f, 11f, 15f);
                float stageY = namePlateRect.yMax + 15f;
                for (int i = 0; i < character.stages.Length; i++)
                {
                    Rect stageRect = new Rect(
                        namePlateRect.x + i * (circleSize + circleGap),
                        stageY,
                        circleSize,
                        circleSize);
                    if (stageRect.Contains(guiPosition))
                        return false;
                }
            }
            return true;
        }

        private void PlayNextCurrentStageVoice()
        {
            EdenGalleryStage stage = CurrentStage;
            EdenGalleryVoiceCatalogEntry entry;
            if (stage == null || string.IsNullOrEmpty(stage.folder) ||
                !voiceEntriesByFolder.TryGetValue(stage.folder, out entry) ||
                entry.lines == null || entry.lines.Length == 0)
            {
                StopVoicePlayback(true);
                return;
            }

            if (voiceImportService == null)
                voiceImportService = new EdenGalleryVoiceImportService();
            if (!voiceImportService.HasInstalledVoicePack || voiceImportService.IsBusy)
            {
                StopVoicePlayback(true);
                return;
            }

            int nextIndex;
            if (!nextVoiceLineByFolder.TryGetValue(stage.folder, out nextIndex))
                nextIndex = 0;
            nextIndex = Mathf.Abs(nextIndex) % entry.lines.Length;
            EdenGalleryVoiceLine line = entry.lines[nextIndex];

            string audioPath;
            bool foundAudio = voiceImportService.TryResolveAudioFile(line.audioFile, out audioPath);
            if (!foundAudio)
                foundAudio = voiceImportService.TryResolveAudioFile(line.voicePath, out audioPath);

            StopVoicePlayback(true);
            if (!foundAudio)
                return;

            nextVoiceLineByFolder[stage.folder] = (nextIndex + 1) % entry.lines.Length;
            SetupVoicePlayback();
            int playbackId = ++voicePlaybackSerial;
            voicePlaybackRoutine = StartCoroutine(
                LoadAndPlayVoice(playbackId, line, audioPath));
        }

        private IEnumerator LoadAndPlayVoice(
            int playbackId,
            EdenGalleryVoiceLine line,
            string audioPath)
        {
            AudioType audioType = GetAudioType(audioPath);
            string uri;
            try
            {
                uri = new Uri(audioPath).AbsoluteUri;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("EdenGallery voice path is invalid: " + exception.Message, this);
                voicePlaybackRoutine = null;
                yield break;
            }

            using (UnityWebRequest request =
                UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                yield return request.SendWebRequest();
                if (playbackId != voicePlaybackSerial)
                    yield break;
                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.LogWarning("EdenGallery voice load failed: " + request.error, this);
                    voicePlaybackRoutine = null;
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    Debug.LogWarning("EdenGallery voice clip could not be decoded: " + audioPath, this);
                    voicePlaybackRoutine = null;
                    yield break;
                }

                activeVoiceClip = clip;
                activeVoiceLine = line;
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();
                while (playbackId == voicePlaybackSerial && voiceAudioSource.isPlaying)
                    yield return null;
                if (playbackId != voicePlaybackSerial)
                    yield break;

                yield return new WaitForSecondsRealtime(0.18f);
                if (playbackId == voicePlaybackSerial)
                {
                    voiceAudioSource.clip = null;
                    activeVoiceLine = null;
                    activeVoiceClip = null;
                    Destroy(clip);
                    voicePlaybackRoutine = null;
                }
            }
        }

        private static AudioType GetAudioType(string audioPath)
        {
            string extension = System.IO.Path.GetExtension(audioPath);
            if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;
            if (string.Equals(extension, ".ogg", StringComparison.OrdinalIgnoreCase))
                return AudioType.OGGVORBIS;
            if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
                return AudioType.MPEG;
            return AudioType.UNKNOWN;
        }

        private void StopVoicePlayback(bool clearSubtitle)
        {
            voicePlaybackSerial++;
            if (voicePlaybackRoutine != null)
            {
                StopCoroutine(voicePlaybackRoutine);
                voicePlaybackRoutine = null;
            }
            if (voiceAudioSource != null)
            {
                voiceAudioSource.Stop();
                voiceAudioSource.clip = null;
            }
            if (activeVoiceClip != null)
            {
                Destroy(activeVoiceClip);
                activeVoiceClip = null;
            }
            if (clearSubtitle)
                activeVoiceLine = null;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = Color.white;

            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 15;
            statusStyle.alignment = TextAnchor.MiddleCenter;
            statusStyle.normal.textColor = new Color(0.82f, 0.9f, 1f);

            characterNameStyle = new GUIStyle(GUI.skin.label);
            characterNameStyle.fontSize = 17;
            characterNameStyle.fontStyle = FontStyle.Bold;
            characterNameStyle.alignment = TextAnchor.MiddleCenter;
            characterNameStyle.clipping = TextClipping.Clip;
            characterNameStyle.normal.textColor = Color.white;

            namePlateNameStyle = new GUIStyle(GUI.skin.label);
            namePlateNameStyle.fontSize = 28;
            namePlateNameStyle.fontStyle = FontStyle.Bold;
            namePlateNameStyle.alignment = TextAnchor.MiddleLeft;
            namePlateNameStyle.clipping = TextClipping.Clip;
            namePlateNameStyle.normal.textColor = Color.white;

            namePlateIdStyle = new GUIStyle(GUI.skin.label);
            namePlateIdStyle.fontSize = 17;
            namePlateIdStyle.alignment = TextAnchor.MiddleLeft;
            namePlateIdStyle.normal.textColor = new Color(0.78f, 0.84f, 0.93f, 1f);

            circleButtonStyle = new GUIStyle(GUI.skin.label);
            circleButtonStyle.fontSize = 21;
            circleButtonStyle.fontStyle = FontStyle.Bold;
            circleButtonStyle.alignment = TextAnchor.MiddleCenter;
            circleButtonStyle.normal.textColor = Color.white;

            toggleButtonStyle = new GUIStyle(circleButtonStyle);
            toggleButtonStyle.fontSize = 24;

            favoriteButtonStyle = new GUIStyle(circleButtonStyle);
            favoriteButtonStyle.fontSize = 18;

            settingsTitleStyle = new GUIStyle(namePlateNameStyle);
            settingsTitleStyle.fontSize = 30;
            settingsTitleStyle.alignment = TextAnchor.MiddleCenter;

            settingsSectionStyle = new GUIStyle(titleStyle);
            settingsSectionStyle.fontSize = 22;

            settingsBodyStyle = new GUIStyle(GUI.skin.label);
            settingsBodyStyle.fontSize = 17;
            settingsBodyStyle.alignment = TextAnchor.MiddleLeft;
            settingsBodyStyle.wordWrap = true;
            settingsBodyStyle.normal.textColor = new Color(0.78f, 0.84f, 0.93f, 1f);

            settingsButtonStyle = new GUIStyle(circleButtonStyle);
            settingsButtonStyle.fontSize = 18;

            voiceSubtitleStyle = new GUIStyle(GUI.skin.label);
            voiceSubtitleStyle.fontSize = 18;
            voiceSubtitleStyle.fontStyle = FontStyle.Normal;
            voiceSubtitleStyle.alignment = TextAnchor.UpperLeft;
            voiceSubtitleStyle.wordWrap = true;
            voiceSubtitleStyle.clipping = TextClipping.Clip;
            voiceSubtitleStyle.padding = new RectOffset(16, 16, 11, 11);
            voiceSubtitleStyle.normal.textColor = new Color(0.07f, 0.075f, 0.085f, 1f);

            roundedMaskTexture = CreateRoundedMaskTexture(48, 11);
            subtleRoundedMaskTexture = CreateRoundedMaskTexture(48, 3);
            circleMaskTexture = CreateCircleMaskTexture(64);
            settingsIconTexture = CreateSettingsIconTexture(64);
        }

        private static float GetBottomBarHeight(float screenHeight)
        {
            return Mathf.Clamp(screenHeight * 0.235f, 142f, 190f);
        }

        private static float GetToggleRailWidth(float screenWidth)
        {
            return Mathf.Clamp(screenWidth * 0.062f, 68f, 88f);
        }

        private static Texture2D CreateRoundedMaskTexture(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "EdenGalleryRoundedMask";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            float inner = radius - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cornerX = x < radius ? inner - x :
                        (x >= size - radius ? x - (size - 1 - inner) : 0f);
                    float cornerY = y < radius ? inner - y :
                        (y >= size - radius ? y - (size - 1 - inner) : 0f);
                    float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                    byte alpha = distance <= radius - 1f
                        ? (byte)255
                        : (byte)Mathf.Clamp(Mathf.RoundToInt((radius - distance) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateCircleMaskTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "EdenGalleryCircleMask";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1f) * 0.5f;
            float radius = center;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = distance <= radius - 1f
                        ? (byte)255
                        : (byte)Mathf.Clamp(Mathf.RoundToInt((radius - distance) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateSettingsIconTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "EdenGallerySettingsIcon";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            const int sampleCount = 4;
            const int totalSamples = sampleCount * sampleCount;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int coveredSamples = 0;
                    for (int sampleY = 0; sampleY < sampleCount; sampleY++)
                    {
                        for (int sampleX = 0; sampleX < sampleCount; sampleX++)
                        {
                            float normalizedX =
                                ((x + (sampleX + 0.5f) / sampleCount) / size) * 2f - 1f;
                            float normalizedY =
                                ((y + (sampleY + 0.5f) / sampleCount) / size) * 2f - 1f;
                            float radius = Mathf.Sqrt(
                                normalizedX * normalizedX + normalizedY * normalizedY);
                            float angle = Mathf.Atan2(normalizedY, normalizedX);
                            float tooth = Mathf.Pow(
                                Mathf.Max(0f, Mathf.Cos(angle * 8f)),
                                8f);
                            float outerRadius = 0.62f + tooth * 0.18f;
                            if (radius >= 0.25f && radius <= outerRadius)
                                coveredSamples++;
                        }
                    }
                    byte alpha = (byte)Mathf.RoundToInt(
                        coveredSamples * 255f / totalSamples);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawRoundedRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, roundedMaskTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawSubtleRoundedRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, subtleRoundedMaskTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawCrispRoundedRect(Rect rect, Color color, float radius)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            radius = Mathf.Clamp(
                radius,
                0f,
                Mathf.Min(rect.width, rect.height) * 0.5f);
            if (radius < 0.75f)
            {
                DrawSolidRect(rect, color);
                return;
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2f),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.DrawTexture(
                new Rect(rect.x + radius, rect.y, rect.width - radius * 2f, radius),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.DrawTexture(
                new Rect(rect.x + radius, rect.yMax - radius, rect.width - radius * 2f, radius),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.DrawTextureWithTexCoords(
                new Rect(rect.x, rect.y, radius, radius),
                circleMaskTexture,
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                true);
            GUI.DrawTextureWithTexCoords(
                new Rect(rect.xMax - radius, rect.y, radius, radius),
                circleMaskTexture,
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                true);
            GUI.DrawTextureWithTexCoords(
                new Rect(rect.x, rect.yMax - radius, radius, radius),
                circleMaskTexture,
                new Rect(0f, 0f, 0.5f, 0.5f),
                true);
            GUI.DrawTextureWithTexCoords(
                new Rect(rect.xMax - radius, rect.yMax - radius, radius, radius),
                circleMaskTexture,
                new Rect(0.5f, 0f, 0.5f, 0.5f),
                true);
            GUI.color = previous;
        }

        private void DrawCircle(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, circleMaskTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawCircularControlBackground(Rect buttonRect)
        {
            DrawCircle(buttonRect, EdenGalleryUISettings.ThemeColor);
            Rect innerRect = new Rect(
                buttonRect.x + 3f,
                buttonRect.y + 3f,
                buttonRect.width - 6f,
                buttonRect.height - 6f);
            DrawCircle(innerRect, new Color(0.035f, 0.06f, 0.095f, 0.97f));
        }

        private void DrawSettingsShortcut(Rect buttonRect)
        {
            DrawCircularControlBackground(buttonRect);
            float iconInset = buttonRect.width * 0.22f;
            Rect iconRect = new Rect(
                buttonRect.x + iconInset,
                buttonRect.y + iconInset,
                buttonRect.width - iconInset * 2f,
                buttonRect.height - iconInset * 2f);
            GUI.DrawTexture(iconRect, settingsIconTexture, ScaleMode.ScaleToFit, true);
            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                SetSettingsVisible(true);
        }

        private void DrawVisibilityShortcut(Rect buttonRect, string label, bool visible)
        {
            DrawCircularControlBackground(buttonRect);
            GUI.Label(buttonRect, label, toggleButtonStyle);
            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                SetUIVisible(visible);
        }

        private void DrawVoiceSubtitle(float width, float height, float bottomHeight)
        {
            string subtitle = string.Empty;
            float bubbleX = 0f;
            float bubbleY = 0f;
            if (activeVoiceLine != null)
            {
                subtitle = EdenGalleryUISettings.NameLanguage ==
                    EdenGalleryNameLanguage.Japanese
                        ? activeVoiceLine.text
                        : activeVoiceLine.textCn;
                if (string.IsNullOrEmpty(subtitle))
                {
                    subtitle = EdenGalleryUISettings.NameLanguage ==
                        EdenGalleryNameLanguage.Japanese
                            ? activeVoiceLine.textCn
                            : activeVoiceLine.text;
                }
                bubbleX = activeVoiceLine.bubbleX;
                bubbleY = activeVoiceLine.bubbleY;
            }

            if (string.IsNullOrEmpty(subtitle))
                return;

            float boxWidth = Mathf.Clamp(width * 0.34f, 320f, 560f);
            float contentHeight = voiceSubtitleStyle.CalcHeight(
                new GUIContent(subtitle),
                boxWidth);
            float boxHeight = Mathf.Clamp(contentHeight, 48f, height * 0.42f);
            float defaultX = width * 0.50f;
            float defaultY = height * 0.28f;
            float anchorX = bubbleX > 0f ? bubbleX / 960f * width : defaultX;
            float anchorY = bubbleY > 0f ? bubbleY / 540f * height : defaultY;
            float maxY = uiVisible
                ? height - bottomHeight - boxHeight - 18f
                : height - boxHeight - 22f;
            Rect boxRect = new Rect(
                Mathf.Clamp(anchorX, 24f, Mathf.Max(24f, width - boxWidth - 24f)),
                Mathf.Clamp(anchorY - boxHeight * 0.5f, 18f, Mathf.Max(18f, maxY)),
                boxWidth,
                boxHeight);

            Color borderColor = new Color(0.025f, 0.025f, 0.03f, 0.98f);
            Color fillColor = new Color(0.98f, 0.98f, 0.95f, 0.94f);
            DrawCrispRoundedRect(boxRect, borderColor, 4f);
            DrawCrispRoundedRect(
                new Rect(boxRect.x + 2f, boxRect.y + 2f, boxRect.width - 4f, boxRect.height - 4f),
                fillColor,
                2f);
            GUI.Label(boxRect, subtitle, voiceSubtitleStyle);
        }

        private static void GetControlColumnRects(
            Rect columnRect,
            out Rect settingsRect,
            out Rect visibilityRect)
        {
            float gap = Mathf.Clamp(columnRect.height * 0.075f, 8f, 12f);
            float buttonSize = Mathf.Min(
                columnRect.width,
                Mathf.Max(1f, (columnRect.height - gap) * 0.5f));
            float groupHeight = buttonSize * 2f + gap;
            float x = columnRect.x + (columnRect.width - buttonSize) * 0.5f;
            float y = columnRect.y + (columnRect.height - groupHeight) * 0.5f;
            settingsRect = new Rect(x, y, buttonSize, buttonSize);
            visibilityRect = new Rect(x, y + buttonSize + gap, buttonSize, buttonSize);
        }

        private void DrawSettingsPage(float width, float height)
        {
            DrawSolidRect(
                new Rect(0f, 0f, width, height),
                new Color(0.018f, 0.031f, 0.052f, 0.985f));

            float headerHeight = Mathf.Clamp(height * 0.105f, 68f, 92f);
            DrawSolidRect(
                new Rect(0f, 0f, width, headerHeight),
                new Color(0.028f, 0.052f, 0.083f, 1f));
            float backSize = Mathf.Clamp(headerHeight - 22f, 48f, 66f);
            Rect backRect = new Rect(18f, (headerHeight - backSize) * 0.5f, backSize, backSize);
            DrawCrispRoundedRect(
                backRect,
                new Color(0.08f, 0.12f, 0.18f, 1f),
                5f);
            GUI.Label(backRect, "‹", toggleButtonStyle);
            GUI.Label(
                new Rect(backRect.xMax + 16f, 0f, width - backRect.xMax * 2f - 32f, headerHeight),
                "设置",
                settingsTitleStyle);
            if (GUI.Button(backRect, GUIContent.none, GUIStyle.none))
                SetSettingsVisible(false);

            float panelWidth = Mathf.Clamp(width * 0.60f, 620f, 900f);
            float panelHeight = Mathf.Clamp(height * 0.70f, 440f, 620f);
            Rect panelRect = new Rect(
                (width - panelWidth) * 0.5f,
                headerHeight + Mathf.Max(18f, (height - headerHeight - panelHeight) * 0.5f),
                panelWidth,
                panelHeight);
            DrawCrispRoundedRect(
                panelRect,
                new Color(0.035f, 0.060f, 0.095f, 0.98f),
                8f);

            float padding = Mathf.Clamp(panelWidth * 0.055f, 30f, 48f);
            float contentX = panelRect.x + padding;
            float contentWidth = panelRect.width - padding * 2f;
            float rowY = panelRect.y + 28f;
            GUI.Label(
                new Rect(contentX, rowY, contentWidth, 42f),
                "显示语言",
                settingsSectionStyle);
            rowY += 52f;

            float languageGap = 14f;
            float languageButtonWidth = Mathf.Min(180f, (contentWidth - languageGap) * 0.5f);
            Rect chineseRect = new Rect(contentX, rowY, languageButtonWidth, 54f);
            Rect japaneseRect = new Rect(
                chineseRect.xMax + languageGap,
                rowY,
                languageButtonWidth,
                54f);
            DrawLanguageOption(
                chineseRect,
                "中文",
                EdenGalleryNameLanguage.Chinese);
            DrawLanguageOption(
                japaneseRect,
                "日本語",
                EdenGalleryNameLanguage.Japanese);

            rowY += 88f;
            DrawSolidRect(
                new Rect(contentX, rowY, contentWidth, 1f),
                new Color(0.25f, 0.31f, 0.42f, 0.55f));
            rowY += 22f;
            GUI.Label(
                new Rect(contentX, rowY, contentWidth, 42f),
                "语音资源",
                settingsSectionStyle);
            rowY += 46f;

            string installedSummary = voiceImportService != null &&
                voiceImportService.HasInstalledVoicePack
                ? "已导入 " + voiceImportService.InstalledFileCount +
                  " 个语音文件（" +
                  FormatByteCount(voiceImportService.InstalledBytes) + "）"
                : "尚未导入语音资源";
            GUI.Label(
                new Rect(contentX, rowY, contentWidth, 34f),
                installedSummary,
                settingsBodyStyle);
            rowY += 42f;

            Rect importButtonRect = new Rect(contentX, rowY, 210f, 56f);
            bool importBusy = IsVoiceImportBusy();
            DrawCrispRoundedRect(
                importButtonRect,
                importBusy
                    ? new Color(0.22f, 0.25f, 0.32f, 1f)
                    : EdenGalleryUISettings.ThemeColor,
                5f);
            GUI.Label(
                importButtonRect,
                importBusy ? "正在导入…" : "导入语音",
                settingsButtonStyle);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !importBusy;
            if (GUI.Button(importButtonRect, GUIContent.none, GUIStyle.none))
                BeginVoiceArchiveSelection();
            GUI.enabled = previousEnabled;

            GUI.Label(
                new Rect(importButtonRect.xMax + 18f, rowY, contentWidth - importButtonRect.width - 18f, 56f),
                "从手机存储中选择 ZIP 压缩包，导入后将解压到应用私有目录。",
                settingsBodyStyle);
            rowY += 72f;

            float importProgress = GetVoiceImportProgress();
            if (importProgress >= 0f)
            {
                Rect progressBackground = new Rect(contentX, rowY, contentWidth, 12f);
                DrawCrispRoundedRect(
                    progressBackground,
                    new Color(0.10f, 0.14f, 0.20f, 1f),
                    6f);
                DrawCrispRoundedRect(
                    new Rect(
                        progressBackground.x,
                        progressBackground.y,
                        progressBackground.width * Mathf.Clamp01(importProgress),
                        progressBackground.height),
                    EdenGalleryUISettings.ThemeColor,
                    6f);
                rowY += 20f;
            }
            GUI.Label(
                new Rect(contentX, rowY, contentWidth, 56f),
                GetVoiceImportStatus(),
                settingsBodyStyle);
        }

        private void DrawLanguageOption(
            Rect rect,
            string label,
            EdenGalleryNameLanguage language)
        {
            bool selected = EdenGalleryUISettings.NameLanguage == language;
            DrawCrispRoundedRect(
                rect,
                selected
                    ? EdenGalleryUISettings.ThemeColor
                    : new Color(0.08f, 0.12f, 0.18f, 1f),
                5f);
            GUI.Label(rect, label, settingsButtonStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                SetNameLanguage(language);
        }

        private Texture2D GetPortraitTexture(int index)
        {
            if (portraitTextures == null || index < 0 || index >= portraitTextures.Length)
                return null;
            if (portraitTextures[index] == null)
            {
                EdenGalleryCharacter character = manifest.characters[index];
                if (character != null && !string.IsNullOrEmpty(character.portraitPath))
                    portraitTextures[index] = Resources.Load<Texture2D>(character.portraitPath);
            }
            return portraitTextures[index];
        }

        private void EnsureSelectedCharacterVisible(float viewportWidth, float cardWidth, float gap)
        {
            if (scrollTargetCharacterIndex < 0)
                return;
            int displayPosition = GetCharacterDisplayPosition(scrollTargetCharacterIndex);
            float left = displayPosition * (cardWidth + gap);
            float right = left + cardWidth;
            if (left < characterScroll.x + gap)
                characterScroll.x = Mathf.Max(0f, left - gap);
            else if (right > characterScroll.x + viewportWidth - gap)
                characterScroll.x = Mathf.Min(
                    characterStripMaxScroll,
                    right - viewportWidth + gap);
            characterScroll.y = 0f;
            scrollTargetCharacterIndex = -1;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float width = Screen.width;
            float height = Screen.height;
            float bottomHeight = GetBottomBarHeight(height);

            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
            {
                GUI.Label(new Rect(20f, 20f, width - 40f, 50f), errorMessage, titleStyle);
                return;
            }

            if (settingsVisible)
            {
                DrawSettingsPage(width, height);
                return;
            }

            if (!uiVisible)
            {
                float hiddenRailWidth = GetToggleRailWidth(width);
                Rect hiddenControlColumn = new Rect(
                    width - hiddenRailWidth - 16f,
                    height - bottomHeight + 16f,
                    hiddenRailWidth,
                    bottomHeight - 32f);
                Rect hiddenSettingsRect;
                Rect restoreRect;
                GetControlColumnRects(
                    hiddenControlColumn,
                    out hiddenSettingsRect,
                    out restoreRect);
                DrawVoiceSubtitle(width, height, bottomHeight);
                DrawSettingsShortcut(hiddenSettingsRect);
                DrawVisibilityShortcut(restoreRect, "▲", true);
                return;
            }

            EdenGalleryCharacter character = CurrentCharacter;
            float namePlateWidth = Mathf.Clamp(width * 0.205f, 220f, 320f);
            float namePlateHeight = Mathf.Clamp(height * 0.13f, 88f, 112f);
            Rect namePlate = new Rect(22f, 20f, namePlateWidth, namePlateHeight);
            DrawCrispRoundedRect(
                namePlate,
                new Color(0.025f, 0.055f, 0.09f, 0.94f),
                6f);
            if (character != null)
            {
                float textPadding = 18f;
                GUI.Label(
                    new Rect(
                        namePlate.x + textPadding,
                        namePlate.y + 8f,
                        namePlate.width - textPadding * 2f,
                        namePlate.height * 0.58f),
                    EdenGalleryUISettings.GetDisplayName(character),
                    namePlateNameStyle);
                GUI.Label(
                    new Rect(
                        namePlate.x + textPadding,
                        namePlate.y + namePlate.height * 0.57f,
                        namePlate.width - textPadding * 2f,
                        namePlate.height * 0.32f),
                    character.cardId,
                    namePlateIdStyle);
            }

            if (character != null && character.stages != null)
            {
                float circleSize = Mathf.Clamp(height * 0.078f, 54f, 68f);
                float circleGap = Mathf.Clamp(circleSize * 0.22f, 11f, 15f);
                float stageY = namePlate.yMax + 15f;
                for (int i = 0; i < character.stages.Length; i++)
                {
                    Rect stageRect = new Rect(
                        namePlate.x + i * (circleSize + circleGap),
                        stageY,
                        circleSize,
                        circleSize);
                    DrawCircle(stageRect, i == stageIndex
                        ? EdenGalleryUISettings.ThemeColor
                        : new Color(0.025f, 0.06f, 0.10f, 0.92f));
                    GUI.Label(stageRect, (i + 1).ToString(), circleButtonStyle);
                    if (GUI.Button(stageRect, GUIContent.none, GUIStyle.none))
                    {
                        LoadStage(i);
                    }
                }
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Color previous = statusStyle.normal.textColor;
                statusStyle.normal.textColor = new Color(1f, 0.42f, 0.38f);
                GUI.Label(new Rect(width * 0.25f, 20f, width * 0.5f, 38f), errorMessage, statusStyle);
                statusStyle.normal.textColor = previous;
            }
            else if (!string.IsNullOrEmpty(loadingMessage))
            {
                GUI.Label(new Rect(width * 0.25f, 20f, width * 0.5f, 38f), loadingMessage, statusStyle);
            }

            Rect stripPanel = new Rect(7f, height - bottomHeight + 7f, width - 14f, bottomHeight - 14f);
            DrawRoundedRect(stripPanel, new Color(0.025f, 0.045f, 0.075f, 0.92f));

            float panelPadding = 9f;
            float toggleRailWidth = GetToggleRailWidth(width);
            float viewportX = stripPanel.x + panelPadding;
            float viewportWidth = Mathf.Max(
                stripPanel.width - toggleRailWidth - panelPadding * 3f,
                100f);
            float viewportHeight = stripPanel.height - panelPadding * 2f;
            float cardWidth = Mathf.Clamp(viewportHeight * 0.78f, 108f, 142f);
            float gap = Mathf.Clamp(cardWidth * 0.085f, 9f, 12f);
            float contentWidth = Mathf.Max(
                viewportWidth,
                characterDisplayOrder.Length * (cardWidth + gap) - gap);
            characterStripMaxScroll = Mathf.Max(0f, contentWidth - viewportWidth);
            characterScroll.x = Mathf.Clamp(characterScroll.x, 0f, characterStripMaxScroll);
            EnsureSelectedCharacterVisible(viewportWidth, cardWidth, gap);

            Rect viewport = new Rect(
                viewportX,
                stripPanel.y + panelPadding,
                viewportWidth,
                viewportHeight);
            Rect scrollContent = new Rect(0f, 0f, contentWidth, viewportHeight);
            characterScroll = GUI.BeginScrollView(
                viewport,
                characterScroll,
                scrollContent,
                false,
                false,
                GUIStyle.none,
                GUIStyle.none);
            int pendingFavoriteCharacterIndex = -1;
            for (int displayIndex = 0;
                 displayIndex < characterDisplayOrder.Length;
                 displayIndex++)
            {
                float cardX = displayIndex * (cardWidth + gap);
                if (cardX + cardWidth < characterScroll.x - gap ||
                    cardX > characterScroll.x + viewportWidth + gap)
                {
                    continue;
                }

                int manifestCharacterIndex = characterDisplayOrder[displayIndex];
                EdenGalleryCharacter item = manifest.characters[manifestCharacterIndex];
                bool selected = manifestCharacterIndex == characterIndex;
                bool favorite = IsFavoriteCharacter(manifestCharacterIndex);
                Rect cardRect = new Rect(cardX, 1f, cardWidth, viewportHeight - 2f);
                DrawSubtleRoundedRect(cardRect, selected
                    ? EdenGalleryUISettings.ThemeColor
                    : new Color(0.25f, 0.30f, 0.39f, 0.72f));
                float border = selected ? 3f : 1f;
                Rect cardInner = new Rect(
                    cardRect.x + border,
                    cardRect.y + border,
                    cardRect.width - border * 2f,
                    cardRect.height - border * 2f);
                DrawSubtleRoundedRect(cardInner, new Color(0.045f, 0.065f, 0.10f, 0.97f));

                float nameHeight = Mathf.Clamp(viewportHeight * 0.23f, 32f, 40f);
                Rect nameBackground = new Rect(
                    cardInner.x,
                    cardInner.yMax - nameHeight,
                    cardInner.width,
                    nameHeight);
                DrawSubtleRoundedRect(nameBackground, selected
                    ? new Color(
                        EdenGalleryUISettings.ThemeColor.r * 0.42f,
                        EdenGalleryUISettings.ThemeColor.g * 0.42f,
                        EdenGalleryUISettings.ThemeColor.b * 0.42f,
                        0.97f)
                    : new Color(0.025f, 0.035f, 0.055f, 0.96f));

                Texture2D portrait = GetPortraitTexture(manifestCharacterIndex);
                if (portrait != null)
                {
                    Rect imageRect = new Rect(
                        cardInner.x + 5f,
                        cardInner.y + 4f,
                        cardInner.width - 10f,
                        Mathf.Max(10f, cardInner.height - nameHeight - 5f));
                    GUI.DrawTexture(imageRect, portrait, ScaleMode.ScaleToFit, true);
                }

                float favoriteSize = Mathf.Clamp(nameHeight - 6f, 26f, 34f);
                Rect favoriteRect = new Rect(
                    nameBackground.xMax - favoriteSize - 3f,
                    nameBackground.y + (nameBackground.height - favoriteSize) * 0.5f,
                    favoriteSize,
                    favoriteSize);
                Rect nameLabelRect = new Rect(
                    nameBackground.x + 4f,
                    nameBackground.y,
                    Mathf.Max(20f, favoriteRect.x - nameBackground.x - 6f),
                    nameBackground.height);
                GUI.Label(
                    nameLabelRect,
                    EdenGalleryUISettings.GetDisplayName(item),
                    characterNameStyle);
                DrawCircle(favoriteRect, favorite
                    ? EdenGalleryUISettings.ThemeColor
                    : new Color(0.12f, 0.16f, 0.23f, 0.96f));
                GUI.Label(favoriteRect, favorite ? "★" : "☆", favoriteButtonStyle);
                if (GUI.Button(favoriteRect, GUIContent.none, GUIStyle.none) &&
                    Time.unscaledTime >= suppressCharacterClickUntil)
                {
                    pendingFavoriteCharacterIndex = manifestCharacterIndex;
                }
                if (pendingFavoriteCharacterIndex < 0 &&
                    GUI.Button(cardRect, GUIContent.none, GUIStyle.none) &&
                    Time.unscaledTime >= suppressCharacterClickUntil)
                {
                    LoadCharacter(manifestCharacterIndex, 0);
                }
            }
            GUI.EndScrollView();
            if (pendingFavoriteCharacterIndex >= 0)
                ToggleFavoriteCharacter(pendingFavoriteCharacterIndex);

            float toggleX = stripPanel.xMax - toggleRailWidth - panelPadding;
            Rect togglePanel = new Rect(
                toggleX,
                stripPanel.y + panelPadding,
                toggleRailWidth,
                viewportHeight);
            Rect settingsShortcutRect;
            Rect hideShortcutRect;
            GetControlColumnRects(
                togglePanel,
                out settingsShortcutRect,
                out hideShortcutRect);
            DrawVoiceSubtitle(width, height, bottomHeight);
            DrawSettingsShortcut(settingsShortcutRect);
            DrawVisibilityShortcut(hideShortcutRect, "▼", false);
        }
    }
}
