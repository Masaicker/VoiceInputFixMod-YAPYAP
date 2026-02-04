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
using SherpaOnnx;
using UnityEngine;

namespace VoiceInputFix
{
    [BepInPlugin("Mhz.voiceinputfix", "VoiceInputFix", "1.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public static ManualLogSource LogSource;
        private static OfflineRecognizer _recognizer;
        private static readonly object _recognizerLock = new object();
        
        private static ConfigEntry<bool> _enableDebugLog;
        private static ConfigEntry<float> _speechThreshold;
        private static string _initError;
        private static string _pluginFolder;
        private static string _dependencyFolder;

        void Awake()
        {
            LogSource = Logger;
            _pluginFolder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            
            // --- 1. Global Assembly Resolver ---
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // --- 2. Intelligent Dependency Localization ---
            LocateDependencies();

            // --- 3. Diagnostic & Repair System ---
            CheckAndRepairModelEnvironment();

            // --- Configuration Setup (Bilingual) ---
            _enableDebugLog = Config.Bind("General", "EnableDebugLog", false, 
                "Whether to show detailed recognition logs in the console. | 是否在控制台中显示详细的识别日志。");
            
            _speechThreshold = Config.Bind("General", "SpeechThreshold", 0.015f, 
                "Minimum amplitude threshold for speech detection (VAD). | 语音检测的最低振幅阈值（VAD）。建议范围 0.01-0.05。");

            new Harmony("Mhz.voiceinputfix").PatchAll();
            LogSource.LogInfo("=== [VoiceInputFix] Initialized ===");
        }

        private void LocateDependencies()
        {
            string currentScan = _pluginFolder;
            string targetDll = "sherpa-onnx-c-api.dll";
            string foundPath = null;

            // Scan up to 2 levels above plugin folder to cover BepInEx/plugins roots
            for (int i = 0; i < 3; i++)
            {
                if (string.IsNullOrEmpty(currentScan)) break;

                // Priority 1: Direct hit in current folder
                if (File.Exists(Path.Combine(currentScan, targetDll)))
                {
                    foundPath = currentScan;
                    break;
                }

                // Priority 2: Search all subdirectories (covers different mod folders)
                try
                {
                    string[] files = Directory.GetFiles(currentScan, targetDll, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        foundPath = Path.GetDirectoryName(files[0]);
                        break;
                    }
                }
                catch { /* Ignore restricted folders */ }

                currentScan = Path.GetDirectoryName(currentScan);
            }

            _dependencyFolder = foundPath;

            if (!string.IsNullOrEmpty(_dependencyFolder))
            {
                Log($"Native dependencies located at: {_dependencyFolder}");
                SetDllDirectory(_dependencyFolder);
                LoadLibrary(Path.Combine(_dependencyFolder, "onnxruntime.dll"));
                LoadLibrary(Path.Combine(_dependencyFolder, "sherpa-onnx-c-api.dll"));
            }
            else
            {
                _initError = "Runtime Missing";
                LogError("CRITICAL ERROR: Could not find Sherpa-ONNX runtime DLLs! Please install the required dependency package.");
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.Contains("SherpaOnnx"))
            {
                string path = Path.Combine(_dependencyFolder ?? _pluginFolder, "SherpaOnnx.dll");
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        }

        private void CheckAndRepairModelEnvironment()
        {
            var modelDir = Path.Combine(_pluginFolder, "models");
            
            // Ensure directory exists
            if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);

            var modelFile = Path.Combine(modelDir, "model.onnx");
            var tokensFile = Path.Combine(modelDir, "tokens.txt");
            var readmeFile = Path.Combine(modelDir, "README_DOWNLOAD.md");

            // Ensure download guide exists
            if (!File.Exists(readmeFile))
            {
                string readmeContent = "# VoiceInputFix - Model Download Guide / 模型下载指南\n\n" +
                                       "Please download the following files and place them in this directory:\n" +
                                       "请下载以下文件并将它们放入该目录中：\n\n" +
                                       "1. **model.onnx** (1.03GB)\n" +
                                       "   URL: https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx\n\n" +
                                       "2. **tokens.txt** (940KB)\n" +
                                       "   URL: https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt\n\n" +
                                       "Please start or restart the game after the files are placed correctly. / 文件放置正确后，请启动或重启游戏。";
                File.WriteAllText(readmeFile, readmeContent);
            }

            // Diagnostic check
            List<string> missing = new List<string>();
            if (!File.Exists(modelFile)) missing.Add(" - model.onnx");
            if (!File.Exists(tokensFile)) missing.Add(" - tokens.txt");

            if (missing.Count > 0)
            {
                _initError = "Files Missing";
                string missingFilesStr = string.Join("\n", missing);
                
                string errorMsg = "【检测到语音识别模型文件缺失】\n" +
                                  "缺失文件：\n" +
                                  $"{missingFilesStr}\n\n" +
                                  "存放目录：\n" +
                                  $"{modelDir}\n\n" +
                                  "状态说明：\n" +
                                  "由于模型文件缺失，本模组及原版语音识别功能将无法生效，但游戏仍可正常进行。\n" +
                                  "点击确认后将自动为您打开目标目录，请查阅 README_DOWNLOAD.md 获取下载指引。\n\n" +
                                  "--------------------------------------------------\n\n" +
                                  "[Model Files Missing]\n" +
                                  "Missing Files:\n" +
                                  $"{missingFilesStr}\n\n" +
                                  "Directory:\n" +
                                  $"{modelDir}\n\n" +
                                  "Notice:\n" +
                                  "The mod and original voice recognition will be disabled without these files, but the game will run normally. \n" +
                                  "Click OK to open the folder and check README_DOWNLOAD.md.";

                MessageBox(IntPtr.Zero, errorMsg, "VoiceInputFix Diagnostic", 0x00000030); // MB_ICONWARNING
                
                try { Process.Start("explorer.exe", modelDir); } catch { }
                LogError($"[Diagnostic] Missing required files in {modelDir}");
            }
        }

        void OnDestroy()
        {
            lock (_recognizerLock)
            {
                _recognizer?.Dispose();
                _recognizer = null;
            }
        }

        internal static void EnsureRecognizer()
        {
            if (_recognizer != null || _initError != null) return;
            lock (_recognizerLock)
            {
                if (_recognizer != null) return;
                try
                {
                    var modelDir = Path.Combine(_pluginFolder, "models");
                    var config = new OfflineRecognizerConfig();
                    config.ModelConfig.SenseVoice.Model = Path.Combine(modelDir, "model.onnx");
                    config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                    config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
                    config.ModelConfig.NumThreads = 4;
                    config.DecodingMethod = "greedy_search";

                    _recognizer = new OfflineRecognizer(config);
                    Log("Fun-ASR Engine Ready.");
                }
                catch (Exception ex)
                {
                    _initError = ex.ToString();
                    LogError($"Init Fail: {ex.Message}");
                }
            }
        }

        public static async Task RecognitionLoop(VoskSpeechToText instance)
        {
            EnsureRecognizer();
            if (_recognizer == null) return;

            var audioAccumulator = new List<float>();
            var silenceTimer = new Stopwatch();
            var partialTimer = Stopwatch.StartNew();

            try
            {
                var bufferQueue = instance._threadedBufferQueue;
                var resultQueue = instance._threadedResultQueue;
                int sampleRate = instance._sampleRate;

                while (instance._running)
                {
                    bool isFrameLoud = false;
                    bool hasData = false;
                    float currentThreshold = _speechThreshold.Value;

                    while (bufferQueue.TryDequeue(out var pcmData))
                    {
                        hasData = true;
                        float frameMax = 0;
                        float[] frameSamples = new float[pcmData.Length];

                        for (int i = 0; i < pcmData.Length; i++)
                        {
                            frameSamples[i] = pcmData[i] / 32768f;
                            float abs = Math.Abs(frameSamples[i]);
                            if (abs > frameMax) frameMax = abs;
                        }

                        if (frameMax > currentThreshold)
                        {
                            isFrameLoud = true;
                            audioAccumulator.AddRange(frameSamples);
                        }
                        else if (audioAccumulator.Count > 0)
                        {
                            audioAccumulator.AddRange(frameSamples);
                        }
                    }

                    if (isFrameLoud) silenceTimer.Restart();

                    if (hasData && audioAccumulator.Count > 0)
                    {
                        if (partialTimer.ElapsedMilliseconds > 120 && audioAccumulator.Count > 1600)
                        {
                            var text = InternalDecode(audioAccumulator.ToArray());
                            if (!string.IsNullOrEmpty(text))
                            {
                                resultQueue.Enqueue($"{{\"partial\":\"{text.Replace("\"", "\\\"")}\"}}");
                            }
                            partialTimer.Restart();
                        }
                    }

                    if (audioAccumulator.Count > 0 && (silenceTimer.ElapsedMilliseconds > 350 || audioAccumulator.Count > 160000))
                    {
                        var text = InternalDecode(audioAccumulator.ToArray());
                        if (!string.IsNullOrEmpty(text))
                        {
                            Log($"Final: {text}");
                            resultQueue.Enqueue($"{{\"alternatives\":[{{\"conf\":1.0,\"text\":\"{text.Replace("\"", "\\\"")}\"}}],\"partial\":false}}");
                        }
                        resultQueue.Enqueue("{\"partial\":\"[unk]\"}");
                        audioAccumulator.Clear();
                        silenceTimer.Reset();
                        await Task.Delay(100);
                    }
                    
                    await Task.Delay(15);
                }
            }
            catch (Exception ex)
            {
                LogError($"Loop Error: {ex.Message}");
            }
        }

        private static string InternalDecode(float[] samples)
        {
            if (samples == null || samples.Length == 0) return string.Empty;
            lock (_recognizerLock)
            {
                if (_recognizer == null) return string.Empty;
                using var stream = _recognizer.CreateStream();
                stream.AcceptWaveform(16000, samples);
                _recognizer.Decode(stream);
                var rawText = stream.Result.Text;
                var cleaned = Regex.Replace(rawText, @"<\|.*?\|>", "").Trim();
                return cleaned.Replace("。", "").Replace("，", "").Replace("？", "").Replace("！", "");
            }
        }

        public static void Log(string m) 
        { 
            if (_enableDebugLog != null && _enableDebugLog.Value) 
                LogSource?.LogInfo(m); 
        }
        
        public static void LogError(string m) => LogSource?.LogError(m);
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

        static IEnumerator EmptyEnumerator()
        {
            yield break;
        }
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
}