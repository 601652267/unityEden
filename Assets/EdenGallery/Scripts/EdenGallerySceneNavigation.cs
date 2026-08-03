namespace EdenGallery
{
    public static class EdenGallerySceneNavigation
    {
        public const string GallerySceneName = "SampleScene";
        public const string CharacterDetailsSceneName = "CharacterDetailsScene";
        public const string CharacterBattleSceneName = "CharacterBattleScene";

        public static int CharacterIndex { get; private set; } = -1;
        public static int GalleryStageIndex { get; private set; }
        public static string CardId { get; private set; } = string.Empty;
        public static string BattleCardId { get; private set; } = string.Empty;
        public static string BattleDisplayName { get; private set; } = string.Empty;
        public static bool HasCharacterRequest
        {
            get { return CharacterIndex >= 0 || !string.IsNullOrEmpty(CardId); }
        }

        public static void OpenCharacterDetails(
            int characterIndex,
            int galleryStageIndex,
            string cardId)
        {
            CharacterIndex = characterIndex;
            GalleryStageIndex = galleryStageIndex;
            CardId = cardId ?? string.Empty;
        }

        public static void OpenCharacterBattle(
            int characterIndex,
            int galleryStageIndex,
            string cardId,
            string displayName)
        {
            CharacterIndex = characterIndex;
            GalleryStageIndex = galleryStageIndex;
            CardId = cardId ?? string.Empty;
            BattleCardId = CardId;
            BattleDisplayName = displayName ?? string.Empty;
        }

        public static void Clear()
        {
            CharacterIndex = -1;
            GalleryStageIndex = 0;
            CardId = string.Empty;
            BattleCardId = string.Empty;
            BattleDisplayName = string.Empty;
        }
    }
}
