using System;

namespace EdenGallery
{
    [Serializable]
    public sealed class EdenGalleryManifest
    {
        public string version;
        public string generatedAt;
        public EdenGalleryCharacter[] characters;
    }

    [Serializable]
    public sealed class EdenGalleryCharacter
    {
        public string cardId;
        public string name;
        public string nameCn;
        public string portraitPath;
        public EdenGalleryStage[] stages;
    }

    [Serializable]
    public sealed class EdenGalleryStage
    {
        public string folder;
        public string label;
        public bool sceneSized;
        public bool originalPrefabComposition;
        public bool autoFrameComposition;
        public string backgroundPath;
        public float backgroundScale = 1f;
        public float backgroundScaleX;
        public float backgroundScaleY;
        public float backgroundOffsetX;
        public float backgroundOffsetY;
        public float backgroundOffsetZ;
        public float backgroundRotationX;
        public float backgroundRotationY;
        public float backgroundRotationZ;
        public bool useCustomBackgroundSortingOrder;
        public int backgroundSortingOrder;
        public EdenGalleryLayer[] layers;
    }

    [Serializable]
    public sealed class EdenGalleryLayer
    {
        public string type;
        public string name;
        public string atlasPath;
        public string skeletonPath;
        public string[] texturePaths;
        public string imagePath;
        public string skinName;
        public string animationName;
        public bool roleLayer;
        public bool backgroundLayer;
        public bool fullscreen;
        public float displayScale = 1f;
        public float displayScaleX;
        public float displayScaleY;
        public float offsetX;
        public float offsetY;
        public float offsetZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public bool useCustomSortingOrder;
        public int sortingOrder;
        public string slotName;
        public string[] slotNames;
        public string[] excludeSlotNames;
    }

    [Serializable]
    public sealed class EdenGalleryVoiceCatalog
    {
        public int version;
        public string generatedAt;
        public EdenGalleryVoiceCatalogEntry[] entries;
    }

    [Serializable]
    public sealed class EdenGalleryVoiceCatalogEntry
    {
        public string folder;
        public string cardId;
        public EdenGalleryVoiceLine[] lines;
    }

    [Serializable]
    public sealed class EdenGalleryVoiceLine
    {
        public string voicePath;
        public string audioFile;
        public string text;
        public string textCn;
        public float bubbleX;
        public float bubbleY;
    }
}
