using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EdenApkCharacterExtractor
{
    private const string BundleRoot = "/private/tmp/eden_apk_newchars/bundles";
    private const string ConfigRoot = "/private/tmp/eden_apk_newchars/config";
    private const string VoiceBundleRoot =
        "/private/tmp/eden_apk_newchars/voice_bundles";
    private const string OutputRoot = "/private/tmp/eden_apk_newchars/export";

    private static readonly string[] CharacterIds =
    {
        "11300056",
        "11300057"
    };

    public static void Export()
    {
        if (Directory.Exists(OutputRoot))
            Directory.Delete(OutputRoot, true);
        Directory.CreateDirectory(OutputRoot);

        List<string> report = new List<string>();
        foreach (string id in CharacterIds)
        {
            string outputDirectory = Path.Combine(OutputRoot, id);
            Directory.CreateDirectory(outputDirectory);
            foreach (string bundlePath in Directory.GetFiles(
                BundleRoot,
                "*" + id + "*.aab",
                SearchOption.TopDirectoryOnly))
            {
                ExportBundle(bundlePath, outputDirectory, report);
            }
        }

        string voiceOutput = Path.Combine(OutputRoot, "voice");
        Directory.CreateDirectory(voiceOutput);
        foreach (string bundlePath in Directory.GetFiles(
            VoiceBundleRoot,
            "*.aab",
            SearchOption.TopDirectoryOnly))
        {
            ExportBundle(bundlePath, voiceOutput, report);
        }

        string configOutput = Path.Combine(OutputRoot, "config");
        Directory.CreateDirectory(configOutput);
        foreach (string bundlePath in Directory.GetFiles(
            ConfigRoot,
            "*.aab",
            SearchOption.TopDirectoryOnly))
        {
            ExportBundle(bundlePath, configOutput, report);
        }

        File.WriteAllLines(
            Path.Combine(OutputRoot, "report.txt"),
            report.ToArray());
        AssetDatabase.Refresh();
        Debug.Log("EDEN_APK_CHARACTER_EXPORT_OK lines=" + report.Count);
    }

    private static void ExportBundle(
        string bundlePath,
        string parentOutputDirectory,
        List<string> report)
    {
        string bundleName = Path.GetFileNameWithoutExtension(bundlePath);
        string outputDirectory = Path.Combine(parentOutputDirectory, bundleName);
        Directory.CreateDirectory(outputDirectory);
        report.Add("BUNDLE " + bundleName);

        AssetBundle bundle = LoadWrappedBundle(bundlePath, report);
        if (bundle == null)
        {
            report.Add("  ERROR load failed");
            return;
        }

        try
        {
            string[] assetNames = bundle.GetAllAssetNames();
            Array.Sort(assetNames, StringComparer.OrdinalIgnoreCase);
            foreach (string assetName in assetNames)
            {
                UnityEngine.Object[] assets;
                try
                {
                    assets = bundle.LoadAssetWithSubAssets(assetName);
                }
                catch (Exception exception)
                {
                    report.Add(
                        "  ERROR " + assetName + " " +
                        exception.GetType().Name + ": " + exception.Message);
                    continue;
                }

                report.Add(
                    "  ASSET " + assetName +
                    " objects=" + (assets == null ? 0 : assets.Length));
                if (assets == null)
                    continue;
                for (int i = 0; i < assets.Length; i++)
                {
                    UnityEngine.Object asset = assets[i];
                    if (asset == null)
                    {
                        report.Add("    NULL");
                        continue;
                    }
                    report.Add(
                        "    " + asset.GetType().FullName +
                        " name=" + asset.name);
                    ExportObject(asset, outputDirectory, i, report);
                }

                UnityEngine.Object[] dependencies =
                    EditorUtility.CollectDependencies(assets);
                report.Add(
                    "    DEPENDENCIES " +
                    (dependencies == null ? 0 : dependencies.Length));
                if (dependencies == null)
                    continue;
                for (int dependencyIndex = 0;
                     dependencyIndex < dependencies.Length;
                     dependencyIndex++)
                {
                    UnityEngine.Object dependency =
                        dependencies[dependencyIndex];
                    if (dependency == null)
                        continue;
                    report.Add(
                        "      " + dependency.GetType().FullName +
                        " name=" + dependency.name);
                    ExportObject(
                        dependency,
                        outputDirectory,
                        10000 + dependencyIndex,
                        report);
                    GameObject dependencyObject = dependency as GameObject;
                    if (dependencyObject != null)
                        ReportHierarchy(dependencyObject, report);
                }
            }
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    private static void ReportHierarchy(
        GameObject root,
        List<string> report)
    {
        foreach (Transform item in
            root.GetComponentsInChildren<Transform>(true))
        {
            string components = string.Empty;
            foreach (Component component in item.GetComponents<Component>())
            {
                if (components.Length != 0)
                    components += ",";
                components += component == null
                    ? "<missing>"
                    : component.GetType().FullName;
            }
            report.Add(
                "        OBJECT " + GetPath(item, root.transform) +
                " active=" + item.gameObject.activeSelf +
                " position=" + item.localPosition +
                " rotation=" + item.localEulerAngles +
                " scale=" + item.localScale +
                " components=" + components);
            Renderer renderer = item.GetComponent<Renderer>();
            if (renderer != null)
            {
                report.Add(
                    "          RENDERER type=" + renderer.GetType().Name +
                    " enabled=" + renderer.enabled +
                    " sortingLayer=" + renderer.sortingLayerName +
                    " sortingOrder=" + renderer.sortingOrder);
            }
        }
    }

    private static string GetPath(Transform item, Transform root)
    {
        string result = item.name;
        Transform current = item.parent;
        while (current != null && current != root)
        {
            result = current.name + "/" + result;
            current = current.parent;
        }
        return result;
    }

    private static AssetBundle LoadWrappedBundle(
        string bundlePath,
        List<string> report)
    {
        byte[] source = File.ReadAllBytes(bundlePath);
        byte[] signature =
        {
            (byte)'U',
            (byte)'n',
            (byte)'i',
            (byte)'t',
            (byte)'y',
            (byte)'F',
            (byte)'S'
        };
        int offset = FindSequence(source, signature);
        if (offset < 0)
            return null;
        if (offset == 0)
            return AssetBundle.LoadFromMemory(source);

        byte[] bundleBytes = new byte[source.Length - offset];
        Buffer.BlockCopy(
            source,
            offset,
            bundleBytes,
            0,
            bundleBytes.Length);
        report.Add("  WRAPPER_BYTES " + offset);
        return AssetBundle.LoadFromMemory(bundleBytes);
    }

    private static int FindSequence(byte[] source, byte[] sequence)
    {
        if (source == null || sequence == null ||
            source.Length < sequence.Length || sequence.Length == 0)
        {
            return -1;
        }
        for (int sourceIndex = 0;
             sourceIndex <= source.Length - sequence.Length;
             sourceIndex++)
        {
            bool matches = true;
            for (int sequenceIndex = 0;
                 sequenceIndex < sequence.Length;
                 sequenceIndex++)
            {
                if (source[sourceIndex + sequenceIndex] ==
                    sequence[sequenceIndex])
                {
                    continue;
                }
                matches = false;
                break;
            }
            if (matches)
                return sourceIndex;
        }
        return -1;
    }

    private static void ExportObject(
        UnityEngine.Object asset,
        string outputDirectory,
        int index,
        List<string> report)
    {
        string cleanName = CleanFileName(asset.name);
        TextAsset textAsset = asset as TextAsset;
        if (textAsset != null)
        {
            string extension = GuessTextAssetExtension(textAsset);
            string path = UniquePath(
                outputDirectory,
                cleanName,
                extension,
                index);
            File.WriteAllBytes(path, textAsset.bytes);
            report.Add("      WRITE " + path + " bytes=" + textAsset.bytes.Length);
            return;
        }

        Texture2D texture = asset as Texture2D;
        if (texture != null)
        {
            string path = UniquePath(
                outputDirectory,
                cleanName,
                ".png",
                index);
            byte[] png = EncodeTexture(texture);
            File.WriteAllBytes(path, png);
            report.Add(
                "      WRITE " + path +
                " size=" + texture.width + "x" + texture.height +
                " format=" + texture.format +
                " readable=" + texture.isReadable +
                " bytes=" + png.Length);
            return;
        }

        Sprite sprite = asset as Sprite;
        if (sprite != null && sprite.texture != null)
        {
            string path = UniquePath(
                outputDirectory,
                cleanName + "_sprite",
                ".png",
                index);
            byte[] png = EncodeTexture(sprite.texture);
            File.WriteAllBytes(path, png);
            report.Add(
                "      WRITE " + path +
                " spriteTexture=" + sprite.texture.name);
            return;
        }

        AudioClip audioClip = asset as AudioClip;
        if (audioClip != null)
        {
            string path = UniquePath(
                outputDirectory,
                cleanName,
                ".wav",
                index);
            WriteWave(audioClip, path);
            report.Add(
                "      WRITE " + path +
                " channels=" + audioClip.channels +
                " frequency=" + audioClip.frequency +
                " samples=" + audioClip.samples);
        }
    }

    private static void WriteWave(AudioClip clip, string path)
    {
        clip.LoadAudioData();
        int sampleCount = clip.samples * clip.channels;
        float[] source = new float[sampleCount];
        if (!clip.GetData(source, 0))
            throw new InvalidOperationException(
                "Could not read audio samples from " + clip.name);

        const int bytesPerSample = 2;
        int dataSize = sampleCount * bytesPerSample;
        using (FileStream stream = File.Create(path))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * bytesPerSample);
            writer.Write((short)(clip.channels * bytesPerSample));
            writer.Write((short)(bytesPerSample * 8));
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataSize);
            foreach (float value in source)
            {
                float clamped = Mathf.Clamp(value, -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(clamped * 32767f));
            }
        }
    }

    private static string GuessTextAssetExtension(TextAsset asset)
    {
        string name = asset.name.ToLowerInvariant();
        if (name.Contains(".atlas") || LooksLikeAtlas(asset.bytes))
            return ".atlas.txt";
        if (name.Contains(".skel") || LooksLikeSpineSkeleton(asset.bytes))
            return ".skel.bytes";
        if (name.EndsWith(".json"))
            return ".json";
        if (name.EndsWith(".lua") || name.EndsWith(".lua.bytes"))
            return ".lua.bytes";
        return ".bytes";
    }

    private static bool LooksLikeAtlas(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 20)
            return false;
        int printable = 0;
        int sampleLength = Math.Min(bytes.Length, 300);
        for (int i = 0; i < sampleLength; i++)
        {
            byte value = bytes[i];
            if (value == 9 || value == 10 || value == 13 ||
                (value >= 32 && value <= 126))
            {
                printable++;
            }
        }
        if (printable < sampleLength * 0.9f)
            return false;
        string prefix = System.Text.Encoding.UTF8.GetString(
            bytes,
            0,
            sampleLength);
        return prefix.Contains(".png") &&
               (prefix.Contains("size:") || prefix.Contains("filter:"));
    }

    private static bool LooksLikeSpineSkeleton(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 16)
            return false;
        int zeroes = 0;
        int sampleLength = Math.Min(bytes.Length, 100);
        for (int i = 0; i < sampleLength; i++)
            if (bytes[i] == 0)
                zeroes++;
        return zeroes > 1;
    }

    private static byte[] EncodeTexture(Texture2D source)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        Texture2D readable = new Texture2D(
            source.width,
            source.height,
            TextureFormat.RGBA32,
            false,
            false);
        readable.ReadPixels(
            new Rect(0, 0, source.width, source.height),
            0,
            0,
            false);
        readable.Apply(false, false);
        byte[] png = readable.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(readable);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return png;
    }

    private static string UniquePath(
        string directory,
        string name,
        string extension,
        int index)
    {
        string baseName = name;
        if (baseName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            baseName = baseName.Substring(0, baseName.Length - extension.Length);
        string path = Path.Combine(directory, baseName + extension);
        if (!File.Exists(path))
            return path;
        return Path.Combine(
            directory,
            baseName + "_" + index + extension);
    }

    private static string CleanFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "unnamed";
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
