using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>统一约束 Inkspell 音频的移动端导入体积与运行时加载方式。</summary>
    internal sealed class InkspellAudioImportProcessor : AssetPostprocessor
    {
        private const string AudioRoot = "Assets/Game/Res/Audio/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AudioRoot, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            var isBgm = assetPath.Contains("/BGM/");
            importer.forceToMono = !isBgm;
            importer.loadInBackground = isBgm;

            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = isBgm
                    ? AudioClipLoadType.Streaming
                    : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = isBgm ? 0.32f : 0.42f,
                sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                sampleRateOverride = (uint)(isBgm ? 24000 : 32000),
                preloadAudioData = !isBgm,
            };
        }
    }
}
