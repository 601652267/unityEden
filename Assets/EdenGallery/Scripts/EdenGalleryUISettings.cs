using UnityEngine;

namespace EdenGallery
{
    public enum EdenGalleryNameLanguage
    {
        Chinese,
        Japanese
    }

    public static class EdenGalleryUISettings
    {
        // Global theme color used by selected character and stage controls.
        public static Color ThemeColor = new Color32(145, 132, 255, 255);

        // Global language switch. Other callers may change this at runtime.
        public static EdenGalleryNameLanguage NameLanguage = EdenGalleryNameLanguage.Chinese;

        public static string GetDisplayName(EdenGalleryCharacter character)
        {
            if (character == null)
                return string.Empty;
            if (NameLanguage == EdenGalleryNameLanguage.Chinese &&
                !string.IsNullOrEmpty(character.nameCn))
            {
                return character.nameCn;
            }
            return string.IsNullOrEmpty(character.name) ? character.cardId : character.name;
        }
    }
}
