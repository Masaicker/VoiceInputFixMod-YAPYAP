# VoiceInputFix (Fun-ASR / SenseVoice Edition)

### Description
VoiceInputFix is a replacement for the default speech recognition system in YAPYAP. It swaps the original Vosk engine with **Fun-ASR (SenseVoice)** to provide:
- **Higher Accuracy**: More reliable command recognition.
- **Multi-language Support**: Automatically recognizes Chinese, English, Japanese, Korean, and Cantonese.
- **Lower Latency**: Faster response times for voice commands.

### Download Models
You must download the following files for the mod to work:
- **model.onnx** (1.03GB): [Download](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx)
- **tokens.txt** (940KB): [Download](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt) (If the link opens in your browser, press Ctrl+S to save, or right-click the link and select "Save Link As...")

### Installation
1. **Critical**: All DLL files (**VoiceInputFix.dll**, **SherpaOnnx.dll**, **onnxruntime.dll**, **sherpa-onnx-c-api.dll**) and the **models** folder **MUST** be in the same directory.
2. You can place them directly into `BepInEx/plugins/`, or within a subfolder like `BepInEx/plugins/VoiceInputFix/`.
3. Place the downloaded `model.onnx` and `tokens.txt` inside the **models** folder.
   - Example path: `BepInEx/plugins/VoiceInputFix/models/model.onnx`

### Performance Characteristics
1. **Initial Loading**: The game will experience a **significant screen freeze** when the engine initializes for the very first time. This is normal behavior while the 1GB model is being loaded into memory.
2. **Menu Reloading**: When returning to the main menu and re-entering the game, there is a loading delay for the model, but it will not freeze the screen again.
3. **Gameplay**: Once you enter a level, the recognition is smooth and available immediately with no impact on game performance.

### Advanced Configuration
Config file path: `BepInEx/config/Mhz.voiceinputfix.cfg`

- **SpeechThreshold** (Default: 0.015)
  This is the "Noise Gate". It determines the volume required to trigger recognition.
  - If the mod captures background noise or text stays on screen too long: **Increase** this value (e.g., 0.025).
  - If you have to shout to be heard: **Decrease** this value (e.g., 0.010).
- **EnableDebugLog** (Default: false)
  Set to `true` if you need to see the recognition details in the BepInEx console.

---

# VoiceInputFix (Fun-ASR / SenseVoice 版)

### 模组简介
VoiceInputFix 是一款改进《YAPYAP》语音识别体验的模组。它将游戏原有的 Vosk 引擎替换为 **Fun-ASR (SenseVoice)**，主要改进包括：
- **识别更精准**：有效减少指令识别错误的情况。
- **多语言支持**：自动识别中、英、日、韩、粤语，无需手动配置。
- **更低延迟**：语音指令的响应速度比原版更快。

### 模型下载
必须下载以下两个文件，模组才能运行：
- **model.onnx** (1.03GB): [点击下载](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx)
- **tokens.txt** (940KB): [点击下载](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt) (若点击后直接打开，请在网页中按 Ctrl+S 保存，或右键点击下载链接选择“链接另存为”)

### 安装步骤
1. **核心原则**：所有的 DLL 文件（**VoiceInputFix.dll**, **SherpaOnnx.dll**, **onnxruntime.dll**, **sherpa-onnx-c-api.dll**）与 **models** 文件夹**必须**位于同一目录下。
2. 你可以将它们直接放入 `BepInEx/plugins` 目录，也可以放入 `plugins` 下的任意子文件夹内（**注意：文件夹路径请勿包含中文字符**。例如 `BepInEx/plugins/VoiceInputFix/`）。
3. 将下载好的 `model.onnx` 和 `tokens.txt` 放入 **models** 文件夹内。
   - 示例路径：`BepInEx/plugins/VoiceInputFix/models/model.onnx`

### 运行特性
1. **首次加载**：第一次启动语音引擎时，会出现一次**较长时间的屏幕卡顿**（画面静止）。这是正在将大型模型载入内存，属于正常现象，请耐心等待加载完成。
2. **重连加载**：返回主菜单并重新进入游戏时，会有短暂的模型重载等待时间，但不会再次导致画面卡死。
3. **关卡表现**：正式进入关卡后，功能可立即使用，运行流畅且不占用额外的游戏帧率。

### 进阶配置
配置文件路径：`BepInEx/config/Mhz.voiceinputfix.cfg`

- **SpeechThreshold** (默认值: 0.015)
  这是语音检测的“分贝门槛”。
  - **如果环境吵闹导致文字不消失**：请**调高**此值（例如 0.025 或 0.03）。
  - **如果说话必须很大声才能识别**：请**调低**此值（例如 0.010）。
- **EnableDebugLog** (默认值: false)
  设为 `true` 后可以在 BepInEx 控制台中查看详细的识别文本和调试信息。