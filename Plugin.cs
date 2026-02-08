using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using YAPYAP;

namespace VoiceInputFix
{
    [BepInPlugin("Mhz.voiceinputfix", "VoiceInputFix", "1.0.9")]
    public class Plugin : BaseUnityPlugin
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public static ManualLogSource LogSource;
        private static string _initError;
        private static string _pluginFolder;
        internal static VoiceManager _voiceManager;
        internal static string[] _currentGrammar;
        internal static HashSet<string> _grammarSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Independent folders for each DLL
        private static string _apiFolder;
        private static string _onnxFolder;
        private static string _managedFolder;

        private static ConfigEntry<bool> _enableDebugLog;
        private static ConfigEntry<float> _speechThreshold;
        private static ConfigEntry<string> _language;

        void Awake()
        {
            LogSource = Logger;
            _pluginFolder = Path.GetDirectoryName(Info.Location);
            
            // --- 1. Global Assembly Resolver ---
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // --- 2. Dependency Check ---
            bool depsOk = LocateDependencies();

            // --- 3. Model Check ---
            CheckAndRepairModelEnvironment();

            // If runtime dependencies are missing, stop initialization
            if (!depsOk) return;

            // --- 4. Configuration Setup ---
            _enableDebugLog = Config.Bind("General", "EnableDebugLog", false, 
                "Whether to show detailed recognition logs in the console. | 是否在控制台中显示详细的识别日志。");
            
            _speechThreshold = Config.Bind("General", "SpeechThreshold", 0.015f, 
                "Minimum amplitude threshold for speech detection (VAD). | 语音检测的最低振幅阈值（VAD）。建议范围 0.01-0.05。");

            _language = Config.Bind("General", "Language", "auto",
                "Specified language for recognition. This PRIORITIZES the selected language to improve accuracy, but may still detect others if confidence is high. Options: auto, zh, en, ja, ko, yue. Invalid values will fallback to auto. | 指定识别语言。这会【优先】识别所选语言以提高准确率，但在置信度极高时仍可能识别出其他语言。可选：auto (自动), zh (中文), en (英文), ja (日文), ko (韩文), yue (粤语)。无效值将回退到自动。");

            new Harmony("Mhz.voiceinputfix").PatchAll();
            LogSource.LogInfo("=== [VoiceInputFix] Initialized ===");
        }

        private bool LocateDependencies()
        {
            const string apiDll = "sherpa-onnx-c-api.dll";
            const string onnxDll = "onnxruntime.dll";
            const string managedDll = "sherpa-onnx.dll";
            
            // 1. Independent global searches for ALL THREE DLLs
            _apiFolder = FindFileRecursively(_pluginFolder, apiDll, 4);
            _onnxFolder = FindFileRecursively(_pluginFolder, onnxDll, 4);
            _managedFolder = FindFileRecursively(_pluginFolder, managedDll, 4);

            // 2. Identify missing components
            List<string> missing = new List<string>();
            if (string.IsNullOrEmpty(_apiFolder)) missing.Add($" - {apiDll}");
            if (string.IsNullOrEmpty(_onnxFolder)) missing.Add($" - {onnxDll}");
            if (string.IsNullOrEmpty(_managedFolder)) missing.Add($" - {managedDll}");

            if (missing.Count > 0)
            {
                _initError = "Dependency Missing";
                string nexusUrl = "https://www.nexusmods.com/yapyap/mods/5?tab=files";
                string thunderstoreUrl = "https://thunderstore.io/c/yapyap/p/Mhz/SherpaOnnxRuntime/versions/";

                string errorMsg = "【语音识别运行库缺失】\n\n" +
                                  "检测到以下核心组件缺失，插件将无法加载：\n" +
                                  $"{string.Join("\n", missing)}\n\n" +
                                  "点击【确定】将为您随机复制一个下载链接到剪贴板，您可以直接在浏览器地址栏粘贴（Ctrl+V）访问。\n\n" +
                                  "--------------------------------------------------\n\n" +
                                  "[Runtime Dependency Missing]\n\n" +
                                  "The following core components are missing, the plugin will not load:\n" +
                                  $"{string.Join("\n", missing)}\n\n" +
                                  "Click [OK] to copy a download link to your clipboard, then you can paste it into your browser.";

                int result = MessageBox(IntPtr.Zero, errorMsg, "VoiceInputFix Diagnostic", 0x00050011);
                if (result == 1) 
                {
                    string[] urls = { nexusUrl, thunderstoreUrl };
                    GUIUtility.systemCopyBuffer = urls[new System.Random().Next(urls.Length)];
                }
                return false;
            }

            // 3. Load libraries using discovered full paths
            // We set DLL directory to onnx folder first as API might depend on it
            SetDllDirectory(_onnxFolder); 
            LoadLibrary(Path.Combine(_onnxFolder, onnxDll));
            LoadLibrary(Path.Combine(_apiFolder, apiDll));
            SetDllDirectory(null); // Restore default search path
            
            LogSource.LogInfo($"[VoiceInputFix] Dependencies loaded: API @ {_apiFolder}, ONNX @ {_onnxFolder}, Managed @ {_managedFolder}");
            return true;
        }

        private string FindFileRecursively(string startDir, string fileName, int maxLevels)
        {
            string currentScan = startDir;
            for (int i = 0; i < maxLevels; i++)
            {
                if (string.IsNullOrEmpty(currentScan)) break;
                // Priority check: local folder
                if (File.Exists(Path.Combine(currentScan, fileName))) return currentScan;
                // Recursive check: all subfolders
                try
                {
                    string[] files = Directory.GetFiles(currentScan, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0) return Path.GetDirectoryName(files[0]);
                }
                catch
                {
                    // ignored
                }

                currentScan = Path.GetDirectoryName(currentScan);
            }
            return null;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.Contains("sherpa-onnx") || args.Name.Contains("SherpaOnnx"))
            {
                string folder = _managedFolder ?? _apiFolder ?? _pluginFolder;
                string path = Path.Combine(folder, "sherpa-onnx.dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }

        private void CheckAndRepairModelEnvironment()
        {
            var modelDir = Path.Combine(_pluginFolder, "models");
            if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);

            var modelFile = Path.Combine(modelDir, "model.onnx");
            var tokensFile = Path.Combine(modelDir, "tokens.txt");
            var readmeFile = Path.Combine(modelDir, "README_DOWNLOAD.md");

            if (!File.Exists(readmeFile))
            {
                string readmeContent = "# VoiceInputFix - Model Download Guide / 模型下载指南\n\n" +
                                       "Please download the following files and place them in THIS directory (the 'models' folder):\n" +
                                       "请下载以下文件并将它们放入【当前】目录（即 models 文件夹）中：\n\n" +
                                       "1. **model.onnx** (1.03GB)\n" +
                                       "   URL: https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx\n\n" +
                                       "2. **tokens.txt** (940KB)\n" +
                                       "   URL: https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt\n\n" +
                                       "--------------------------------------------------\n\n" +
                                       "### Optional: Runtime Dependencies / 运行库依赖 (If mod fails to load)\n\n" +
                                       "If the mod fails to load due to missing native DLLs, please download from:\n" +
                                       "如果模组因缺少运行库 DLL 无法加载，请从以下地址获取：\n\n" +
                                       "- NexusMods: https://www.nexusmods.com/yapyap/mods/5?tab=files\n" +
                                       "- Thunderstore: https://thunderstore.io/c/yapyap/p/Mhz/SherpaOnnxRuntime/versions/\n\n" +
                                       "Please start or restart the game after all files are placed correctly. / 所有文件放置正确后，请重新启动游戏。";
                File.WriteAllText(readmeFile, readmeContent);
            }

            List<string> missing = new List<string>();
            if (!File.Exists(modelFile)) missing.Add(" - model.onnx");
            if (!File.Exists(tokensFile)) missing.Add(" - tokens.txt");

            if (missing.Count > 0)
            {
                if (string.IsNullOrEmpty(_initError)) _initError = "Models Missing";
                string missingFilesStr = string.Join("\n", missing);
                
                string errorMsg = "【检测到语音识别模型文件缺失】\n\n" +
                                  "检测到以下模型权重文件缺失：\n" +
                                  $"{missingFilesStr}\n\n" +
                                  "存放目录：\n" +
                                  $"{modelDir}\n\n" +
                                  "状态说明：\n" +
                                  "由于模型文件缺失，本模组及原版语音识别功能将无法生效，但游戏仍可正常进行。\n" +
                                  "点击确认后将为您打开目标目录，请查阅 README_DOWNLOAD.md 获取下载指引。\n\n" +
                                  "--------------------------------------------------\n\n" +
                                  "[Model Files Missing]\n\n" +
                                  "Missing Files:\n" +
                                  $"{missingFilesStr}\n\n" +
                                  "Directory:\n" +
                                  $"{modelDir}\n\n" +
                                  "Notice:\n" +
                                  "The mod and original voice recognition will be disabled without these files, but the game will run normally. \n" +
                                  "Click [OK] to open the folder and check README_DOWNLOAD.md.";

                int result = MessageBox(IntPtr.Zero, errorMsg, "VoiceInputFix Diagnostic", 0x00050031);
                if (result == 1) try { Application.OpenURL(Path.GetFullPath(modelDir)); }catch { /* ignored */ }
                LogError($"[Diagnostic] Missing required files in {modelDir}");
            }
        }

        void OnDestroy()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            SherpaEngine.Cleanup();
        }

        public static void Log(string m) { if (_enableDebugLog != null && _enableDebugLog.Value) LogSource?.LogInfo(m); }
        public static void LogError(string m) => LogSource?.LogError(m);

        internal static void EnsureRecognizer() => SherpaEngine.Init(_pluginFolder, _initError, _language.Value);
        public static string InternalDecode(float[] samples) => SherpaEngine.Decode(samples);
        public static async Task RecognitionLoop(VoskSpeechToText instance) => await SherpaEngine.RunLoop(instance, _speechThreshold.Value);
    }

    internal static class SherpaEngine
    {
        private static SherpaOnnx.OfflineRecognizer _recognizer;
        private static readonly object Lock = new object();
        private static readonly Regex CleanRegex = new Regex(@"<\|.*?\|>|[。，？！]", RegexOptions.Compiled);

        public static void Init(string folder, string initError, string language = "auto")
        {
            if (_recognizer != null || initError != null) return;
            lock (Lock)
            {
                if (_recognizer != null) return;
                try
                {
                    var modelDir = Path.Combine(folder, "models");
                    var config = new SherpaOnnx.OfflineRecognizerConfig();
                    config.ModelConfig.SenseVoice.Model = Path.Combine(modelDir, "model.onnx");
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;

                    // Apply language setting with validation
                    string lang = language?.ToLower().Trim() ?? "";
                    switch (lang)
                    {
                        case "zh":
                        case "en":
                        case "ja":
                        case "ko":
                        case "yue":
                            // Valid codes are kept as is
                            break;
                        default:
                            // All other cases (including "auto", null, or invalid) fallback to ""
                            lang = ""; 
                            break;
                    }
                    config.ModelConfig.SenseVoice.Language = lang;

                    config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
                    config.ModelConfig.NumThreads = 4;
                    config.DecodingMethod = "greedy_search";
                    _recognizer = new SherpaOnnx.OfflineRecognizer(config);
                    Plugin.Log($"Fun-ASR Engine Ready. Language: {(string.IsNullOrEmpty(lang) ? "auto" : lang)}");
                }
                catch (Exception ex) { Plugin.LogError($"Init Fail: {ex.Message}"); }
            }
        }

        public static string Decode(float[] samples, string[] grammar = null)
        {
            if (samples == null || samples.Length == 0 || _recognizer == null) return string.Empty;
            lock (Lock)
            {
                using var stream = _recognizer.CreateStream();
                stream.AcceptWaveform(16000, samples);
                _recognizer.Decode(stream);
                var rawText = stream.Result.Text;
                Plugin.Log($"[Raw] {rawText}");

                // Optimized cleaning: one regex pass instead of multiple Replace calls
                var cleaned = CleanRegex.Replace(rawText, "").Trim();

                // Filter-and-Keep: extract whitelist keywords using cached HashSet
                if (Plugin._grammarSet.Count > 0)
                {
                    var result = new List<string>();

                    // Scan text in original order, extract characters/words from grammar
                    for (int i = 0; i < cleaned.Length; i++)
                    {
                        // Try to match longer keywords first (multi-char phrases)
                        for (int len = Math.Min(10, cleaned.Length - i); len >= 1; len--)
                        {
                            var candidate = cleaned.Substring(i, len);
                            if (Plugin._grammarSet.Contains(candidate))
                            {
                                result.Add(candidate);
                                i += len - 1;  // Skip matched characters
                                break;
                            }
                        }
                    }

                    return result.Count > 0 ? string.Join(" ", result) : string.Empty;
                }

                return cleaned;
            }
        }

        public static void Cleanup()
        {
            lock (Lock) { _recognizer?.Dispose(); _recognizer = null; }
        }

        public static async Task RunLoop(VoskSpeechToText instance, float threshold)
        {
            if (_recognizer == null) return;
            var audioAccumulator = new List<float>();
            var silenceTimer = new Stopwatch();
            var partialTimer = Stopwatch.StartNew();
            string lastSentPartial = string.Empty;

            try
            {
                var bufferQueue = instance._threadedBufferQueue;
                var resultQueue = instance._threadedResultQueue;
                while (instance._running)
                {
                    bool isFrameLoud = false;
                    bool hasData = false;
                    while (bufferQueue.TryDequeue(out var pcmData))
                    {
                        hasData = true;
                        float[] frameSamples = new float[pcmData.Length];
                        float frameMax = 0;
                        for (int i = 0; i < pcmData.Length; i++)
                        {
                            frameSamples[i] = pcmData[i] / 32768f;
                            float abs = Math.Abs(frameSamples[i]);
                            if (abs > frameMax) frameMax = abs;
                        }
                        if (frameMax > threshold) { isFrameLoud = true; audioAccumulator.AddRange(frameSamples); }
                        else if (audioAccumulator.Count > 0) audioAccumulator.AddRange(frameSamples);
                    }
                    if (isFrameLoud) silenceTimer.Restart();
                    if (hasData && audioAccumulator.Count > 0)
                    {
                        if (partialTimer.ElapsedMilliseconds > 120 && audioAccumulator.Count > 1600)
                        {
                            var text = Decode(audioAccumulator.ToArray(), Plugin._currentGrammar);
                            // Only send if the recognized text has actually changed
                            if (!string.IsNullOrEmpty(text) && text != lastSentPartial)
                            {
                                resultQueue.Enqueue($"{{\"partial\":\"{text.Replace("\"", "\\\"")}\"}}");
                                lastSentPartial = text;
                            }
                            partialTimer.Restart();
                        }
                    }
                    if (audioAccumulator.Count > 0 && (silenceTimer.ElapsedMilliseconds > 350 || audioAccumulator.Count > 160000))
                    {
                        var text = Decode(audioAccumulator.ToArray(), Plugin._currentGrammar);
                        if (!string.IsNullOrEmpty(text))
                        {
                            Plugin.Log($"Final: {text}");
                            resultQueue.Enqueue($"{{\"alternatives\":[{{\"conf\":1.0,\"text\":\"{text.Replace("\"", "\\\"")}\"}}],\"partial\":false}}");
                        }
                        resultQueue.Enqueue("{\"partial\":\"[unk]\"}");
                        audioAccumulator.Clear();
                        lastSentPartial = string.Empty; // Reset for next utterance
                        silenceTimer.Reset();
                        await Task.Delay(100);
                    }
                    await Task.Delay(15);
                }
            }
            catch (Exception ex) { Plugin.LogError($"Loop Error: {ex.Message}"); }
        }
    }

    [HarmonyPatch(typeof(VoskSpeechToText), "StartupRoutine")]
    class PatchStartupRoutine
    {
        static bool Prefix(VoskSpeechToText __instance, ref IEnumerator __result, ref bool ____didInit)
        {
            ____didInit = true; 
            if (__instance.VoiceProcessor != null)
            {
                __instance.VoiceProcessor.OnFrameCaptured -= __instance.VoiceProcessorOnOnFrameCaptured;
                __instance.VoiceProcessor.OnFrameCaptured += __instance.VoiceProcessorOnOnFrameCaptured;
            }
            __result = EmptyEnumerator();
            return false; 
        }
        static IEnumerator EmptyEnumerator() { yield break; }
    }

    [HarmonyPatch(typeof(VoskSpeechToText), "StartRecording")]
    class PatchStartRecording
    {
        static bool Prefix(VoskSpeechToText __instance)
        {
            if (__instance._running) return false;
            Plugin.EnsureRecognizer();
            __instance._running = true;
            __instance._sampleRate = __instance.VoiceProcessor.StartRecording(__instance._sampleRate);
            __instance._recognizerReady = true;
            Traverse.Create(__instance).Field("_threadedWorkTask").SetValue(Task.Run(() => Plugin.RecognitionLoop(__instance)));
            return false;
        }
    }


    [HarmonyPatch(typeof(VoskSpeechToText), "StartVosk")]
    class PatchStartVosk
    {
        static bool Prefix(ref bool ____didInit)
        {
            // Block re-initialization triggered by language switching.
            // Once initialized via our mod, ignore any StartVosk calls with different parameters.
            if (____didInit) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(VoiceManager), "SetLanguage")]
    class PatchSetLanguage
    {
        static void Postfix(VoiceManager __instance)
        {
            if (Plugin._voiceManager == null) Service.Get(out Plugin._voiceManager);
            Plugin._currentGrammar = Plugin._voiceManager._currentVoskTranslator?.Grammar;

            // Pre-calculate HashSet to save performance in Decode loop
            Plugin._grammarSet.Clear();
            if (Plugin._currentGrammar != null)
            {
                foreach (var g in Plugin._currentGrammar)
                {
                    if (!string.IsNullOrEmpty(g)) Plugin._grammarSet.Add(g);
                }
            }

            Plugin.Log($"[Grammar] Updated to language: {__instance._currentVoskLocalisation.Language}, count: {Plugin._currentGrammar?.Length ?? 0}");
        }
    }
}
