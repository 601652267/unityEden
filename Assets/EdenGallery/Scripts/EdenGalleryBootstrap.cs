using UnityEngine;
using UnityEngine.SceneManagement;

namespace EdenGallery
{
    public static class EdenGalleryBootstrap
    {
        private static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            if (registered)
                return;
            registered = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGallery()
        {
            CreateGalleryForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadSceneMode)
        {
            CreateGalleryForScene(scene);
        }

        private static void CreateGalleryForScene(Scene scene)
        {
            if (scene.IsValid() &&
                scene.name ==
                EdenGallerySceneNavigation.CharacterBattleSceneName)
            {
                EdenBattleController[] battleControllers =
                    Object.FindObjectsOfType<EdenBattleController>();
                for (int battleIndex = 0;
                     battleIndex < battleControllers.Length;
                     battleIndex++)
                {
                    EdenBattleController battleController =
                        battleControllers[battleIndex];
                    if (battleController != null &&
                        battleController.gameObject.scene == scene)
                    {
                        return;
                    }
                }

                GameObject battleHost = new GameObject("EdenBattle");
                SceneManager.MoveGameObjectToScene(battleHost, scene);
                battleHost.AddComponent<EdenBattleController>();
                return;
            }

            if (!scene.IsValid() ||
                (scene.name != EdenGallerySceneNavigation.GallerySceneName &&
                 scene.name !=
                 EdenGallerySceneNavigation.CharacterDetailsSceneName))
            {
                return;
            }
            EdenGalleryController[] controllers =
                Object.FindObjectsOfType<EdenGalleryController>();
            for (int controllerIndex = 0;
                 controllerIndex < controllers.Length;
                 controllerIndex++)
            {
                EdenGalleryController controller =
                    controllers[controllerIndex];
                if (controller != null &&
                    controller.gameObject.scene == scene)
                {
                    return;
                }
            }

            GameObject host = new GameObject("EdenGallery");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<EdenGalleryController>();
        }
    }
}
