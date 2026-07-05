# FSMODTSBOX - FromSoftware MOD Translation Studio Box

![GitHub Release](https://img.shields.io/github/v/release/benbakc/FSMODTSBOX)
![License](https://img.shields.io/badge/license-GPLv3-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

**FSMODTSBOX** 是一款基于 WPF .NET8 的 FromSoftware 游戏 MOD 翻译工具，支持一键汉化游戏 MOD 文本。前身为 FSMODBOX（闭源版），现已全面开源免费。

## 支持的游戏

- **艾尔登法环** (Elden Ring) ✓
- **艾尔登法环 黑夜君临** (Elden Ring Nightreign) ✓
- **黑暗之魂 3** (Dark Souls III) ✓
- **黑暗之魂 2** (Dark Souls II) ✓
- **黑暗之魂** (Dark Souls / Remastered) ✓
- **只狼** (Sekiro: Shadows Die Twice) ✓
- **装甲核心 6** (Armored Core VI) ✓
- 更多游戏持续添加中...

## 核心特性

### 🚀 自动翻译
- 内置 **Agnes 2.0 Flash** AI 翻译引擎，无需额外配置 API Key
- 支持 14+ 种语言，精准翻译游戏术语
- 智能缓存机制：已翻译内容自动跳过，二次运行极速完成
- 实时进度显示：每条翻译可视化展示，百分比进度一目了然

### 🔧 强大的 MOD 解析
- 自动解析 **DCX 加密** 的 MSGBND 文件
- 支持 **Yabber** 解包和打包
- 智能恢复英文 Fallback：未翻译文本自动回退英文，确保游戏不报错
- 语言文件夹自动识别（engus, enguk, chs, cht, jpn, kor, fre, ger, ita, spa, pol, rus, tha, bra）

### 🎨 现代化界面
- 简洁美观的 WPF 界面，深色主题
- **中英文双语 UI** 完整支持，一键切换
- 实时日志窗口，翻译过程一目了然
- 翻译进度条 + 百分比 + 条目数三重显示

### 📁 项目结构
```
FSMODTSBOX_Release/
├── FSMODTSBOX.exe      # 主程序
├── FSMODTSBOX.dll      # 核心库
├── FSMODTSBOX.pdb      # 调试符号
├── tools/Yabber/       # Yabber 解包工具
└── i18n/               # 多语言资源文件
```

## 快速开始

### 下载
从 [Releases](https://github.com/benbakc/FSMODTSBOX/releases) 下载最新版本的 `FSMODTSBOX_Release.zip`，解压即可使用。

### 使用方法
1. 解压 `FSMODTSBOX_Release.zip`
2. 运行 `FSMODTSBOX.exe`
3. 选择你的游戏类型（Elden Ring / Dark Souls / Sekiro 等）
4. 选择要翻译的 MOD 文件或文件夹
5. 设置源语言和目标语言
6. 点击开始翻译

## 系统要求

- **操作系统**: Windows 10 / 11（64位）
- **运行环境**: .NET 8.0 Runtime（如未安装首次运行会自动提示下载）
- **硬盘空间**: 约 200 MB（用于 Yabber 解包缓存）
- **网络**: 需要互联网连接（用于 AI 翻译 API 调用）

### 首次使用
1. 确保已安装 [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 解压后直接运行 `FSMODTSBOX.exe`
3. 内置的 Agnes AI 引擎无需额外配置，即开即用

## 配置文件

程序会自动生成 `config.yaml` 配置文件，位于程序同目录下：

```yaml
# 翻译引擎配置
translation:
  provider: agnes  # 内置引擎，无需额外配置
  source_lang: engus
  target_lang: chs

# 界面配置
ui:
  language: zh-CN  # zh-CN 或 en-US
```

## 从源码构建

### 前置要求
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### 构建步骤
```bash
git clone https://github.com/benbakc/FSMODTSBOX.git
cd FSMODTSBOX
dotnet publish -c Release -r win-x64 --self-contained true
```

发布产物位于 `bin/Release/net8.0-windows/win-x64/publish/`。

## 开源协议

本项目基于 **GNU General Public License v3.0** 开源协议发布。

## 致谢

- [Yabber](https://github.com/JKAnderson/Yabber) - FromSoftware 游戏资源解包工具
- [Agnes AI](https://agnes-ai.com) - 内置 AI 翻译引擎

---

**FSMODTSBOX** - 让 MOD 翻译变得简单。Made with ❤️ by forget909
