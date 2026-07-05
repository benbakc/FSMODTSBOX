using System.Collections.Generic;

namespace GameHanBox.Models
{
    public static class UILocalizer
    {
        public static string CurrentLang { get; set; } = "zh";

        private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
        {
            // ===== Navigation =====
            ["nav_home"] = new() { ["zh"] = "首页", ["en"] = "Home" },
            ["nav_settings"] = new() { ["zh"] = "设置", ["en"] = "Settings" },
            ["nav_sponsor"] = new() { ["zh"] = "赞助支持", ["en"] = "Sponsor" },

            // ===== Status bar =====
            ["status_ok"] = new() { ["zh"] = "程序运行正常", ["en"] = "Running normally" },
            ["status_ready"] = new() { ["zh"] = "就绪", ["en"] = "Ready" },
            ["status_version"] = new() { ["zh"] = "当前版本: v1.0.0", ["en"] = "Version: v1.0.0" },

            // ===== Window buttons =====
            ["tooltip_minimize"] = new() { ["zh"] = "最小化", ["en"] = "Minimize" },
            ["tooltip_maximize"] = new() { ["zh"] = "最大化", ["en"] = "Maximize" },
            ["tooltip_close"] = new() { ["zh"] = "关闭", ["en"] = "Close" },

            // ===== Welcome page =====
            ["tip_title"] = new() { ["zh"] = "贴心提示", ["en"] = "Tips" },
            ["tip_body"] = new() { ["zh"] = "翻译前请备份原文件以防万一！\n支持 FromSoftware 游戏 MOD 翻译。", ["en"] = "Back up your files before translating!\nSupports FromSoftware game MOD translation." },
            ["feature_safe_title"] = new() { ["zh"] = "安全可靠", ["en"] = "Safe & Reliable" },
            ["feature_safe_sub"] = new() { ["zh"] = "本地处理", ["en"] = "Local Processing" },
            ["feature_easy_title"] = new() { ["zh"] = "简单易用", ["en"] = "Easy to Use" },
            ["feature_easy_sub"] = new() { ["zh"] = "一键翻译", ["en"] = "One-Click Translate" },
            ["feature_accurate_title"] = new() { ["zh"] = "高效精准", ["en"] = "Fast & Accurate" },
            ["feature_accurate_sub"] = new() { ["zh"] = "智能识别", ["en"] = "Smart Detection" },
            ["feature_updating_title"] = new() { ["zh"] = "持续更新", ["en"] = "Continuous Updates" },
            ["feature_updating_sub"] = new() { ["zh"] = "功能迭代", ["en"] = "Feature Iteration" },

            // ===== Game Card (Red) =====
            ["mod_translate_title"] = new() { ["zh"] = "MOD 翻译", ["en"] = "MOD Translate" },
            ["mod_translate_subtitle"] = new() { ["zh"] = "FromSoftware 游戏", ["en"] = "FromSoftware Games" },
            ["badge_recommended"] = new() { ["zh"] = "推荐", ["en"] = "Recommended" },
            ["step1_title"] = new() { ["zh"] = "选择 MOD 文件夹", ["en"] = "Select MOD Folder" },
            ["step1_desc"] = new() { ["zh"] = "支持 FromSoftware 游戏 MOD", ["en"] = "Supports FromSoftware Game MODs" },
            ["step2_title"] = new() { ["zh"] = "自动分析文本", ["en"] = "Auto Analyze Text" },
            ["step2_desc"] = new() { ["zh"] = "智能识别 MOD 文本差异", ["en"] = "Smart MOD text diff detection" },
            ["step3_title"] = new() { ["zh"] = "AI 自动翻译", ["en"] = "AI Auto Translate" },
            ["step3_desc"] = new() { ["zh"] = "支持多语言·高效准确", ["en"] = "Multi-language · Fast & Accurate" },
            ["step4_title"] = new() { ["zh"] = "自动打包回文件", ["en"] = "Auto Pack Files" },
            ["step4_desc"] = new() { ["zh"] = "直接生成 .msgbnd.dcx 文件", ["en"] = "Generate .msgbnd.dcx directly" },
            ["info_support"] = new() { ["zh"] = "支持 FromSoftware MOD 翻译", ["en"] = "Supports FromSoftware MOD Translation" },
            ["warning_terms"] = new() { ["zh"] = "单机 MOD 翻译，请遵守游戏使用条款", ["en"] = "MOD translation, respect game ToS" },
            ["select_mod_btn"] = new() { ["zh"] = "选择 MOD", ["en"] = "Select MOD" },
            ["drop_zone_mod"] = new() { ["zh"] = "选择 MOD 的 msg/engus 文件夹", ["en"] = "Select MOD msg/engus folder" },

            // ===== MOD Card (Blue) =====
            ["tool_translate_title"] = new() { ["zh"] = "MOD 翻译", ["en"] = "MOD Translation" },
            ["tool_translate_subtitle"] = new() { ["zh"] = "FromSoftware MOD", ["en"] = "FromSoftware MOD" },
            ["bstep1_title"] = new() { ["zh"] = "选择 msg/engUS 文件夹", ["en"] = "Select msg/engUS Folder" },
            ["bstep1_desc"] = new() { ["zh"] = "定位游戏语言文件", ["en"] = "Locate game language files" },
            ["bstep2_title"] = new() { ["zh"] = "自动解包对比文本", ["en"] = "Auto Extract & Compare" },
            ["bstep2_desc"] = new() { ["zh"] = "深度对比·精准匹配", ["en"] = "Deep compare · Precise match" },
            ["bstep3_title"] = new() { ["zh"] = "翻译修改的条目", ["en"] = "Translate Modified Items" },
            ["bstep3_desc"] = new() { ["zh"] = "支持批量编辑翻译", ["en"] = "Batch edit translations" },
            ["bstep4_title"] = new() { ["zh"] = "打包回 .msgbnd.dcx", ["en"] = "Pack to .msgbnd.dcx" },
            ["bstep4_desc"] = new() { ["zh"] = "安全打包·完美兼容", ["en"] = "Safe pack · Perfect compatibility" },
            ["select_game_label"] = new() { ["zh"] = "选择你需要翻译的游戏", ["en"] = "Select game to translate" },
            ["target_lang_label"] = new() { ["zh"] = "目标语言", ["en"] = "Target language" },
            ["warning_mod_size"] = new() { ["zh"] = "MOD 文件可能较大，处理需耐心等待", ["en"] = "MOD files may be large, please be patient" },
            ["select_mod_btn2"] = new() { ["zh"] = "选择 MOD", ["en"] = "Select MOD" },
            ["drop_zone_tool"] = new() { ["zh"] = "将 msg/engUS 文件夹拖拽到此处", ["en"] = "Drag msg/engUS folder here" },

            // ===== Footer =====
            ["footer_text"] = new() { ["zh"] = "© 2026 FSMODTSBOX  |  让游戏无语言障碍  ", ["en"] = "© 2026 FSMODTSBOX  |  Breaking language barriers  " },

            // ===== Scanning Page =====
            ["btn_back"] = new() { ["zh"] = "← 返回", ["en"] = "← Back" },
            ["btn_back_home"] = new() { ["zh"] = "← 返回首页", ["en"] = "← Home" },

            // ===== Editor Page =====
            ["editor_translate"] = new() { ["zh"] = "一键翻译", ["en"] = "One-Click Translate" },
            ["editor_apply"] = new() { ["zh"] = "写入翻译", ["en"] = "Apply Translation" },
            ["editor_save"] = new() { ["zh"] = "保存", ["en"] = "Save" },
            ["editor_original"] = new() { ["zh"] = "原文", ["en"] = "Original" },
            ["editor_translated"] = new() { ["zh"] = "翻译", ["en"] = "Translation" },
            ["editor_hint"] = new() { ["zh"] = "双击或点击翻译列输入中文翻译", ["en"] = "Double-click translation cell to edit" },

            // ===== File Select Page =====
            ["file_oneclick"] = new() { ["zh"] = "一键翻译", ["en"] = "One-Click Translate" },
            ["file_select_folder"] = new() { ["zh"] = "选择文件夹", ["en"] = "Select Folder" },
            ["file_start_scan"] = new() { ["zh"] = "开始扫描", ["en"] = "Start Scan" },
            ["file_select_all"] = new() { ["zh"] = "全选", ["en"] = "Select All" },
            ["file_deselect_all"] = new() { ["zh"] = "取消全选", ["en"] = "Deselect All" },
            ["file_translate_selected"] = new() { ["zh"] = "翻译选中", ["en"] = "Translate Selected" },
            ["file_pack"] = new() { ["zh"] = "打包", ["en"] = "Pack" },

            // ===== Applying Page =====
            ["applying_title"] = new() { ["zh"] = "正在翻译…", ["en"] = "Translating…" },

            // ===== Sponsor Page =====
            ["sponsor_title"] = new() { ["zh"] = "赞助支持", ["en"] = "Sponsor" },
            ["sponsor_desc1"] = new() { ["zh"] = "你好，我是 FSMODTSBOX 的开发者 Forget909。", ["en"] = "Hi, I'm Forget909, developer of FSMODTSBOX." },
            ["sponsor_desc2"] = new() { ["zh"] = "从最初的灵感迸发，到无数个深夜的代码调试，再到如今能够稳定运行的多语言 MOD 翻译工具——这款软件的每一步成长都离不开对游戏 Modding 的热爱。", ["en"] = "From initial inspiration to countless late-night coding sessions - every step of this software's growth comes from a love of game modding." },
            ["sponsor_desc3"] = new() { ["zh"] = "开发维护一款免费开源的工具并不容易，服务器、API、时间和精力都是实实在在的投入。如果你觉得这个工具帮到了你，或者单纯想请我喝杯咖啡☕，欢迎扫码赞助支持。", ["en"] = "Maintaining a free open-source tool isn't easy - servers, APIs, time, and energy all cost. If this tool helped you, or you just want to buy me a coffee ☕, scan the QR code to sponsor." },
            ["sponsor_desc4"] = new() { ["zh"] = "你的每一份支持，都是让我继续改进的动力 🙏", ["en"] = "Every bit of support is motivation to keep improving 🙏" },
            ["sponsor_desc5"] = new() { ["zh"] = "本工具使用了 Yabber 进行文件解包与打包。感谢 Yabber 及其开发者为 FromSoftware MOD 社区做出的卓越贡献 🙏", ["en"] = "This tool uses Yabber for file unpacking and packing. Thanks to Yabber and its developers for their outstanding contributions to the FromSoftware MOD community 🙏" },
            ["sponsor_desc6"] = new() { ["zh"] = "你也可以通过 Ko-fi 赞助支持：https://ko-fi.com/forget909", ["en"] = "You can also sponsor via Ko-fi: https://ko-fi.com/forget909" },
            ["sponsor_desc7"] = new() { ["zh"] = "如果在使用中遇到任何问题，或者想一起交流 MOD 翻译心得，欢迎加入 QQ 群（1033816983）找我，我会在群里为大家解答 🙌", ["en"] = "If you have any questions or want to discuss MOD translation, join our QQ group (1033816983) and I'll help you there 🙌" },
            ["sponsor_qr_tip"] = new() { ["zh"] = "扫描上方二维码赞助支持", ["en"] = "Scan QR code above to sponsor" },

            // ===== Settings Page =====
            ["settings_title"] = new() { ["zh"] = "翻译服务", ["en"] = "Translation Service" },
            ["settings_engine_hint"] = new() { ["zh"] = "选择翻译引擎", ["en"] = "Select translation engine" },
            ["settings_api_title"] = new() { ["zh"] = "🔑 API 密钥配置", ["en"] = "🔑 API Key Configuration" },
            ["settings_api_hint"] = new() { ["zh"] = "配置你的翻译 API 密钥（留空则使用内置 Agnes 2.0 Flash）", ["en"] = "Configure API key (leave empty for built-in Agnes 2.0 Flash)" },
            ["settings_api_key"] = new() { ["zh"] = "API 密钥", ["en"] = "API Key" },
            ["settings_custom_url"] = new() { ["zh"] = "自定义 URL", ["en"] = "Custom URL" },
            ["settings_custom_model"] = new() { ["zh"] = "模型名称", ["en"] = "Model Name" },
            ["settings_custom_key"] = new() { ["zh"] = "自定义 API 密钥", ["en"] = "Custom API Key" },
            ["settings_save"] = new() { ["zh"] = "保存设置", ["en"] = "Save Settings" },
            ["settings_tip_title"] = new() { ["zh"] = "💡 提示", ["en"] = "💡 Tip" },
            ["settings_tip_body"] = new() { ["zh"] = "内置 Agnes 2.0 Flash 为免费共享模型，随着使用人数增多可能会变慢。不同模型的能力和翻译速度也不一样，如果觉得内置模型不够理想，建议使用自己的 API 密钥选择其他模型。", ["en"] = "Built-in Agnes 2.0 Flash is a free shared model; may slow down with more users. Different models vary in capability and speed. For better results, use your own API key." },

            // ===== UI Language Selection =====
            ["ui_language_label"] = new() { ["zh"] = "界面语言", ["en"] = "UI Language" },
            ["lang_zh"] = new() { ["zh"] = "中文", ["en"] = "Chinese" },
            ["lang_en"] = new() { ["zh"] = "English", ["en"] = "English" },

            // ===== Translation Workflow =====
            ["status_translating"] = new() { ["zh"] = "正在翻译…", ["en"] = "Translating…" },
            ["status_writing_xml"] = new() { ["zh"] = "正在写入翻译到 XML...", ["en"] = "Writing translations to XML..." },
            ["status_writing"] = new() { ["zh"] = "正在写入翻译...", ["en"] = "Writing translations..." },
            ["status_packing"] = new() { ["zh"] = "正在调用 Yabber 打包...", ["en"] = "Packing with Yabber..." },
            ["status_packed"] = new() { ["zh"] = "翻译已写入 {0} 条，正在调用 Yabber 打包...", ["en"] = "Written {0} entries, packing with Yabber..." },
            ["status_done"] = new() { ["zh"] = "✅ 翻译完成", ["en"] = "✅ Translation complete" },
            ["status_done_packed"] = new() { ["zh"] = "\n✅ 翻译完成！所有文件已打包回 .msgbnd.dcx\n", ["en"] = "\n✅ Translation complete! All files packed to .msgbnd.dcx\n" },
            ["status_pack_failed"] = new() { ["zh"] = "❌ 打包失败", ["en"] = "❌ Pack failed" },
            ["status_trans_failed"] = new() { ["zh"] = "❌ 翻译失败", ["en"] = "❌ Translation failed" },
            ["status_unpacking"] = new() { ["zh"] = "解包中", ["en"] = "Unpacking" },
            ["status_unpack_ref"] = new() { ["zh"] = "📦 解包参考文件中", ["en"] = "📦 Unpacking reference files" },
            ["status_unpack_mod"] = new() { ["zh"] = "📦 解包中", ["en"] = "📦 Unpacking" },
            ["status_no_api"] = new() { ["zh"] = "❌ 缺少 API 密钥", ["en"] = "❌ Missing API key" },
            ["status_api_needed"] = new() { ["zh"] = "请先在设置中配置 API 密钥！", ["en"] = "Please configure an API key in Settings!" },
            ["status_no_text"] = new() { ["zh"] = "❌ 未找到文本", ["en"] = "❌ No text found" },
            ["status_no_translate"] = new() { ["zh"] = "✅ 无需翻译", ["en"] = "✅ Nothing to translate" },
            ["status_no_files"] = new() { ["zh"] = "还没有翻译任何文件！", ["en"] = "No files have been translated yet!" },
            ["status_mod_ready"] = new() { ["zh"] = "📦 MOD 模式 - {0}", ["en"] = "📦 MOD mode - {0}" },
            ["status_no_ref"] = new() { ["zh"] = "❌ 缺少游戏模板", ["en"] = "❌ Missing game template" },
            ["status_unpack_failed"] = new() { ["zh"] = "❌ 解包失败", ["en"] = "❌ Unpack failed" },
            ["status_ref_incomplete"] = new() { ["zh"] = "❌ 参考文件不完整", ["en"] = "❌ Reference files incomplete" },
            ["status_no_mod_files"] = new() { ["zh"] = "❌ 未找到 MOD 文件", ["en"] = "❌ No MOD files found" },
            ["status_mod_unpack_failed"] = new() { ["zh"] = "❌ MOD 解包失败", ["en"] = "❌ MOD unpack failed" },
            ["status_all_done_packing"] = new() { ["zh"] = "✅ 全部翻译完成，正在自动打包...", ["en"] = "✅ All translated, auto-packing..." },
            ["status_gamedetected"] = new() { ["zh"] = "✅ {0}  (Unity Mono)", ["en"] = "✅ {0} (Unity Mono)" },
            ["status_engine_unsupported"] = new() { ["zh"] = "❌ 不支持的引擎: {0}", ["en"] = "❌ Unsupported engine: {0}" },
            ["status_scan_done_no_strings"] = new() { ["zh"] = "⚠️ 扫描完成，但未找到字符串", ["en"] = "⚠️ Scan complete, no strings found" },
                        ["status_strings_found"] = new() { ["zh"] = "✅ 找到 {0} 个字符串，可开始翻译", ["en"] = "✅ Found {0} strings, ready to translate" },

            // ===== MOD Translation Steps =====
            ["mod_step0"] = new() { ["zh"] = "Step 0/6: 自动解包 engus 参考文件（首次较慢，后续自动跳过）...", ["en"] = "Step 0/6: Auto-unpacking engus reference (first time slow)..." },
            ["mod_step1"] = new() { ["zh"] = "Step 1/6: 解包 MOD 到 {0} 文件夹...", ["en"] = "Step 1/6: Unpacking MOD to {0} folder..." },
            ["mod_step2"] = new() { ["zh"] = "Step 2/6: 按 dcx 包路径对比 XML 文件...", ["en"] = "Step 2/6: Comparing XML by dcx package path..." },
            ["mod_step3"] = new() { ["zh"] = "Step 3/6: 扫描需要翻译的文本...", ["en"] = "Step 3/6: Scanning text to translate..." },
            ["mod_step4"] = new() { ["zh"] = "Step 4/6: 解包并复制官方语言文件...", ["en"] = "Step 4/6: Unpacking & copying official language files..." },
            ["mod_step5"] = new() { ["zh"] = "Step 5/6: 翻译 {0} 个被 MOD 修改的文件...", ["en"] = "Step 5/6: Translating {0} MOD-modified files..." },
            ["mod_scanning_mod"] = new() { ["zh"] = "正在扫描 MOD: {0}", ["en"] = "Scanning MOD: {0}" },
            ["mod_scanning_file"] = new() { ["zh"] = "正在分析: {0}", ["en"] = "Analyzing: {0}" },

            ["batch_processing"] = new() { ["zh"] = "正在处理第 {0}/{1} 批（{2} 个文件，~{3}KB）...", ["en"] = "Processing batch {0}/{1} ({2} files, ~{3}KB)..." },
            ["batch_progress"] = new() { ["zh"] = "第 {0}/{1} 批 翻译进度: {2}%（整体 {3}%）", ["en"] = "Batch {0}/{1} progress: {2}% (overall {3}%)" },
            ["batch_failed"] = new() { ["zh"] = "❌ 第 {0} 批翻译失败: {1}", ["en"] = "❌ Batch {0} failed: {1}" },
            ["batch_error"] = new() { ["zh"] = "❌ 第 {0} 批出错: {1}", ["en"] = "❌ Batch {0} error: {1}" },
            ["file_count"] = new() { ["zh"] = "共 {0} 个文件 | 已翻译: {1} | 待翻译: {2}", ["en"] = "{0} files | Translated: {1} | Pending: {2}" },
            ["trans_progress"] = new() { ["zh"] = "翻译进度: {0}% ({1}/{2} 条)", ["en"] = "Progress: {0}% ({1}/{2} entries)" },
            ["trans_done"] = new() { ["zh"] = "翻译进度: 100% ({0}/{1} 条)", ["en"] = "Progress: 100% ({0}/{1} entries)" },
        };

        public static string Tr(string key)
        {
            if (_strings.TryGetValue(key, out var langs) && langs.TryGetValue(CurrentLang, out var val))
                return val;
            return key;
        }
    }
}
