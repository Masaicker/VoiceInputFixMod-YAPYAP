# VoiceInputFix (Fun-ASR / SenseVoice Edition)

---

## Description
VoiceInputFix replaces the default YAPYAP speech recognition engine (Vosk) with **Fun-ASR (SenseVoice)**, providing a modern and more reliable voice input experience:

### Key Features
- **Much Better Chinese Recognition** — The original Vosk engine has poor Chinese support. This mod significantly outperforms Vosk for Chinese voice commands.
- **Multi-language Support** — Automatically detects **Mandarin/Cantonese, English, Japanese, Korean**, and allows specifying a recognition language.
- **Lower Latency** — Voice commands respond faster than the original engine.

### Note
- The **Fun-ASR model supports multilingual speech recognition**.
- However, the **original Vosk engine shows significantly more stable recognition for English commands in this game**  
  *(other non-Chinese languages may behave similarly, but have not been tested)*, possibly due to its predefined command setup or potential pre-training.
- **Players who primarily use English voice commands are still recommended to use the original Vosk engine**.

---

## Dependencies
This mod requires the **SherpaOnnxRuntime** package to function. It provides the following core libraries:

- `onnxruntime.dll`
- `sherpa-onnx-c-api.dll`
- `sherpa-onnx.dll`

---

## Download Models
You must download the following files for the mod to work:

- **model.onnx** (1.03GB) — [Download](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx)
- **tokens.txt** (940KB) — [Download](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt)  
  *(If the link opens in your browser, press Ctrl+S to save, or right-click and select "Save Link As...")*

**Alternative Models**
- Must be from the **sherpa-onnx-sense-voice** series (`csukuangfj/sherpa-onnx-sense-voice` on Hugging Face).
- Model package must include `model.onnx` and `tokens.txt` and be placed in the `models` folder.

---

## Installation
1. **Critical:** `VoiceInputFix.dll` and the **models** folder **must** be in the same directory.
2. Place them directly into `BepInEx/plugins/`, or a subfolder like `BepInEx/plugins/VoiceInputFix/`.  
   *(Avoid Chinese characters in folder paths.)*
3. Place `model.onnx` and `tokens.txt` inside the **models** folder.
  - Example path: `BepInEx/plugins/VoiceInputFix/models/model.onnx`

---

## Performance
- **Initial Loading** — First-time engine initialization may cause a **significant screen freeze** while loading the 1GB model. This is normal.
- **Menu Reloading** — Returning to the main menu and re-entering the game introduces a short loading delay, but no freeze occurs.
- **Gameplay** — Once a level is entered, recognition works immediately, smoothly, and with no performance impact.

---

## Advanced Configuration
Config file: `BepInEx/config/Mhz.voiceinputfix.cfg`

- **SpeechThreshold** (Default: 0.015) — Noise gate for triggering recognition.
  - Increase if background noise causes text to linger (e.g., 0.025)
  - Decrease if you need to shout to be recognized (e.g., 0.010)

- **Language** (Default: auto) — Specify recognition language:
  - `auto`: Detects all supported languages (**Mandarin/Cantonese, English, Japanese, Korean**) automatically.
  - `zh, en, ja, ko, yue`: Prioritize recognition for the selected language.  
    *(This only **prioritizes** matching, not strictly locking. Extremely similar pronunciations may still be recognized as another language.)*

- **EnableDebugLog** (Default: false) — Enable to view detailed recognition logs in the BepInEx console.

---

## 中文说明

### 模组简介
VoiceInputFix 将《YAPYAP》默认 Vosk 引擎替换为 **Fun-ASR (SenseVoice)**，显著提升语音输入体验：

- **中文识别大幅提升** — 原版 Vosk 对中文支持较差，本模组在中文指令识别上**显著优于**原版
- **多语言支持** — 默认自动识别 **普通话/粤语/英语/日语/韩语**，也可指定识别语言
- **更低延迟** — 语音指令响应速度明显快于原版

**注意**
- Fun-ASR 模型本身支持多语言语音识别
- 但原版 Vosk 引擎在**游戏内英文指令上的识别表现明显更稳定**  
  *(其他非中文语言可能也类似，但尚未进行测试)*，可能与其对游戏内预设指令词的配置或潜在预训练有关
- **对于主要使用英文指令的玩家，仍建议继续使用原版 Vosk 引擎**

---

### 必需依赖
本模组依赖 **SherpaOnnxRuntime**，提供以下核心组件：

- `onnxruntime.dll`
- `sherpa-onnx-c-api.dll`
- `sherpa-onnx.dll`

---

### 模型下载
必须下载以下文件：

- **model.onnx** (1.03GB) — [点击下载](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/model.onnx)
- **tokens.txt** (940KB) — [点击下载](https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-funasr-nano-2025-12-17/resolve/main/tokens.txt)  
  *(若点击后直接打开，请按 Ctrl+S 保存，或右键“链接另存为”)*

**更换其他模型**
- 必须来自 **sherpa-onnx-sense-voice** 系列
- 文件需包含 `model.onnx` 和 `tokens.txt`，放入 `models` 文件夹

---

### 安装步骤
1. **核心原则**：`VoiceInputFix.dll` 与 **models** 文件夹必须在同一目录
2. 可放入 `BepInEx/plugins/` 或子目录（路径请勿含中文字符）
3. 将 `model.onnx` 与 `tokens.txt` 放入 **models** 文件夹
  - 示例路径：`BepInEx/plugins/VoiceInputFix/models/model.onnx`

---

### 运行特性
- **首次加载** — 首次启动时可能出现**明显卡顿**（加载 1GB 模型到内存，属于正常现象）
- **重连加载** — 返回主菜单再进入游戏，仅短暂等待，无卡顿
- **关卡表现** — 进入关卡后立即可用，运行流畅，无额外帧率占用

---

### 进阶配置
配置文件：`BepInEx/config/Mhz.voiceinputfix.cfg`

- **SpeechThreshold**（默认 0.015）— 触发识别的噪声门限
  - 环境吵闹或文字不消失：调高（如 0.025）
  - 必须大声才能识别：调低（如 0.010）

- **Language**（默认 auto）— 指定识别语言
  - `auto`：自动识别中（普通话/粤语）、英、日、韩语
  - `zh/en/ja/ko/yue`：优先匹配指定语言，极端相似发音仍可能识别为其他语言

- **EnableDebugLog**（默认 false）— 打开后在 BepInEx 控制台查看详细日志

---