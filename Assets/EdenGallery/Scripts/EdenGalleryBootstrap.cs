using UnityEngine;

namespace EdenGallery
{
    public static class EdenGalleryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGallery()
        {
            if (Object.FindObjectOfType<EdenGalleryController>() != null)
                return;

            GameObject host = new GameObject("EdenGallery");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<EdenGalleryController>();
        }
    }
}
