using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameHanBox.Models;
using GameHanBox.Services;
using Microsoft.Win32;

namespace GameHanBox
{
	public partial class MainWindow : Window
	{
		private readonly UnityDetector _detector = new UnityDetector();

		private UnityAssetScanner? _scanner;

		private StringPatcher? _patcher;

		private TranslationService? _translator;

		private GameInfo? _gameInfo;

		private List<FoundString>? _foundStrings;

		private CancellationTokenSource? _cts;

		private static string? _cachedApiKey;

		private static AppSettings _appSettings = new AppSettings();

		private List<FileTranslateItem>? _fileItems;

		private const int SIZE_WARN_KB = 100;

		private static readonly Dictionary<string, string> FmgNameCache = new Dictionary<string, string>();

		private static readonly Dictionary<string, string> FmgChineseNames = new Dictionary<string, string>
		{
			["WeaponName"] = "武器名称",
			["ProtectName"] = "防具名称",
			["ItemName"] = "物品名称",
			["MagicName"] = "魔法名称",
			["SkillName"] = "战技名称",
			["GemName"] = "宝石名称",
			["GoodsName"] = "道具名称",
			["NpcName"] = "NPC名称",
			["PlaceName"] = "地点名称",
			["MenuText"] = "菜单文本",
			["MenuHelp"] = "菜单帮助",
			["SystemMessage"] = "系统消息",
			["Dialogue"] = "对话文本",
			["EventText"] = "事件文本",
			["Tutorial"] = "教程文本",
			["SummonMessage"] = "召唤信息",
			["MultiPlayMessage"] = "多人消息",
			["LoadingMessage"] = "加载提示",
			["DeathMessage"] = "死亡信息",
			["BloodMessage"] = "血迹消息",
			["GestureName"] = "手势名称",
			["ActionName"] = "动作名称",
			["MapName"] = "地图名称",
			["AreaName"] = "区域名称",
			["Talk"] = "对话",
			["ShopName"] = "商店名称",
			["WeaponCateName"] = "武器分类",
			["ArmorCateName"] = "防具分类",
			["SpellName"] = "法术名称",
			["IncantationName"] = "祷告名称",
			["AshOfWarName"] = "战灰名称",
			["SpiritName"] = "骨灰名称",
			["TalismanName"] = "护符名称",
			["AmmoName"] = "弹药名称",
			["RingName"] = "戒指名称",
			["KeyItemName"] = "关键道具",
			["EtcItemName"] = "其他道具",
			["MagicDescription"] = "魔法说明",
			["SkillDescription"] = "技能说明",
			["GemDescription"] = "宝石说明",
			["WeaponDescription"] = "武器说明",
			["ProtectDescription"] = "防具说明",
			["GoodsDescription"] = "道具说明",
			["RingDescription"] = "戒指说明",
			["TalismanDescription"] = "护符说明",
			["SpiritDescription"] = "骨灰说明",
			["Menu"] = "菜单",
			["MenuOther"] = "其他菜单",
			["MenuLine"] = "菜单选项",
			["NetworkMessage"] = "网络消息",
			["CharaInitParam"] = "角色初始参数",
			["ItemSweat"] = "物品说明",
			["WhiteMessage"] = "白色信息",
			["BossName"] = "BOSS名称",
			["BossDescription"] = "BOSS说明",
			["LocationName"] = "地名",
			["Weather"] = "天气",
			["EventName"] = "事件名",
			["GameTitle"] = "游戏标题",
			["Credit"] = "制作人员",
			["HelpText"] = "帮助文本",
			["KnowledgeName"] = "知识名称",
			["KnowledgeDescription"] = "知识说明"
		};

		private string? _yabberDir;

		private string? _fsModDir;

		private string? _fsTransPath;

		private bool _uiLoaded;

		private bool _loadingSettings;

		private readonly Dictionary<object, string> _originalTextCache = new Dictionary<object, string>();

		private static readonly Dictionary<string, string> _uiTextMap;

		private static Dictionary<string, string> _keyToChinese;

		private readonly List<Process> _runningYabberProcesses = new List<Process>();

		private readonly object _yabberLock = new object();



































































		private CancellationToken PrepareCancellation()
		{
			_cts?.Cancel();
			_cts?.Dispose();
			_cts = new CancellationTokenSource();
			CleanupTranslationResources();
			return _cts.Token;
		}

		private void CleanupTranslationResources()
		{
			// 1. 杀掉所有正在运行的 Yabber 进程
			lock (_yabberLock)
			{
				foreach (var proc in _runningYabberProcesses.ToArray())
				{
					try
					{
						if (!proc.HasExited)
						{
							proc.Kill(entireProcessTree: true);
							proc.WaitForExit(5000);
						}
					}
					catch
					{
					}
					finally
					{
						proc.Dispose();
					}
				}
				_runningYabberProcesses.Clear();
			}

			// 2. 释放翻译服务的 HttpClient
			if (_translator != null)
			{
				try { _translator.Dispose(); } catch { }
				_translator = null;
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			base.Loaded += delegate
			{
				_uiLoaded = true;
				LangUISwitch.Items.Clear();
				LangUISwitch.Items.Add(UILocalizer.Tr("lang_zh"));
				LangUISwitch.Items.Add(UILocalizer.Tr("lang_en"));
				LoadUILanguage();
				UpdateNavActive("welcome");
			};
			_appSettings = SettingsService.Load();
			_cachedApiKey = _appSettings.ResolvedApiKey;

			// Auto-detect UI language from system timezone on first run
			if (string.IsNullOrEmpty(_appSettings.UILanguage))
			{
				string tzId = TimeZoneInfo.Local.Id;
				_appSettings.UILanguage = (tzId == "China Standard Time" || tzId == "Taipei Standard Time") ? "zh" : "en";
				SettingsService.Save(_appSettings);
			}

			LoadSettingsUI();
			PopulateGameCombo();
			PopulateLangCombo();
		}

		private void SelectModBtn_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "选择游戏的可执行文件 (.exe)",
				Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
				RestoreDirectory = true
			};
			if (openFileDialog.ShowDialog().GetValueOrDefault())
			{
				ShowPage("scanning");
				ScanDetailText.Text = "正在分析: " + openFileDialog.FileName;
				StartDetection(openFileDialog.FileName);
			}
		}

		private async void StartDetection(string exePath)
		{
			string exePath2 = exePath;
			_cts?.Cancel();
			_cts = new CancellationTokenSource();
			try
			{
				GameInfo gameInfo = (_gameInfo = await Task.Run(() => _detector.Detect(exePath2), _cts.Token));
				if (!gameInfo.IsUnityGame)
				{
					ShowPage("notunity");
					StatusText.Text = "❌ 不支持的引擎: " + gameInfo.EngineType;
					return;
				}
				StatusText.Text = "✅ " + gameInfo.GameName + "  (Unity Mono)";
				ShowPage("scanning");
				ScanDetailText.Text = $"游戏: {gameInfo.GameName}\n数据目录: {gameInfo.DataDir}\n资源文件数: {gameInfo.AssetFiles.Count}";
				await StartScanning(gameInfo);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				MessageBox.Show("检测出错: " + ex2.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
				ShowPage("welcome");
			}
		}

		private async Task StartScanning(GameInfo info)
		{
			_scanner = new UnityAssetScanner();
			_patcher = new StringPatcher(info.DataDir);
			Progress<int> progress = new Progress<int>(delegate(int percent)
			{
				base.Dispatcher.Invoke(delegate
				{
					ScanProgressBar.Value = percent;
					ScanFileText.Text = $"扫描中... {percent}%";
				});
			});
			try
			{
				_foundStrings = await _scanner.ScanAllAsync(info, progress);
				if (_foundStrings.Count == 0)
				{
					ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_scan_done_no_strings");
					StatusText.Text = UILocalizer.Tr("status_scan_done_no_strings");
					return;
				}
				ShowPage("editor");
				StatusText.Text = $"✅ 找到 {_foundStrings.Count} 个字符串，可开始翻译";
				LoadStringsGrid();
			}
			catch (Exception ex)
			{
				MessageBox.Show("扫描出错: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
				ShowPage("welcome");
			}
		}

		private void LoadStringsGrid()
		{
			if (_foundStrings != null)
			{
				StringsGrid.ItemsSource = _foundStrings;
				UpdateStringCount();
				EditorStatusText.Text = "双击或点击翻译列输入中文翻译";
			}
		}

		private void UpdateStringCount()
		{
			if (_foundStrings != null)
			{
				int value = _foundStrings.Count((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText));
				StringCountText.Text = $"共 {_foundStrings.Count} 个字符串  |  已翻译: {value}";
			}
		}

		private static string GetFmgChineseName(string fileName)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
			fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameWithoutExtension);
			if (FmgChineseNames.TryGetValue(fileNameWithoutExtension, out string value))
			{
				return value;
			}
			lock (FmgNameCache)
			{
				if (FmgNameCache.TryGetValue(fileNameWithoutExtension, out string value2))
				{
					return value2;
				}
			}
			if (FmgNameCache.TryGetValue(fileName, out string value3))
			{
				return value3;
			}
			return Regex.Replace(Regex.Replace(fileNameWithoutExtension, "([a-z])([A-Z])", "$1 $2"), "([A-Z]+)([A-Z][a-z])", "$1 $2");
		}

		private async void TranslateSelectedBtn_Click(object sender, RoutedEventArgs e)
		{
			if (_foundStrings == null || _fileItems == null)
			{
				return;
			}
			_appSettings = SettingsService.Load();
			_cachedApiKey = _appSettings.ResolvedApiKey;
			if (string.IsNullOrEmpty(_cachedApiKey))
			{
				MessageBox.Show("请先在设置中配置 API 密钥！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			List<FileTranslateItem> list = (from f in _fileItems
				where f.Status == FileTransStatus.Pending
				orderby f.SizeKB descending
				select f).ToList();
			if (list.Count == 0)
			{
				await AutoPackAsync();
				return;
			}
			List<List<FileTranslateItem>> batches = new List<List<FileTranslateItem>>();
			List<FileTranslateItem> list2 = new List<FileTranslateItem>();
			int num = 0;
			foreach (FileTranslateItem item in list)
			{
				if (num + item.SizeKB > 900 && list2.Count > 0)
				{
					batches.Add(list2);
					list2 = new List<FileTranslateItem>();
					num = 0;
				}
				list2.Add(item);
				num += item.SizeKB;
			}
			if (list2.Count > 0)
			{
				batches.Add(list2);
			}
			string valueOrDefault = AppSettings.LanguagePrompts.GetValueOrDefault(_appSettings.TargetLanguage, "简体中文");
			_translator = new TranslationService(_appSettings, valueOrDefault);
			FileSelectProgress.Visibility = Visibility.Visible;
			for (int batchIdx = 0; batchIdx < batches.Count; batchIdx++)
			{
				List<FileTranslateItem> batch = batches[batchIdx];
				FileSelectProgress.Value = batchIdx * 100 / batches.Count;
				FileSelectStatus.Text = string.Format(UILocalizer.Tr("batch_processing"), batchIdx + 1, batches.Count, batch.Count, batch.Sum((FileTranslateItem f) => f.SizeKB));
				HashSet<string> batchPaths = new HashSet<string>(batch.Select((FileTranslateItem f) => f.FilePath));
				foreach (FileTranslateItem fileItem in _fileItems)
				{
					fileItem.IsSelected = batchPaths.Contains(fileItem.FilePath);
				}
				List<FoundString> toTranslate = _foundStrings.Where((FoundString s) => batchPaths.Contains(s.XmlFilePath) && string.IsNullOrEmpty(s.TranslatedText)).ToList();
				if (toTranslate.Count == 0)
				{
					foreach (FileTranslateItem item2 in batch)
					{
						item2.IsSelected = false;
						item2.Status = FileTransStatus.Translated;
					}
					FileListBox.Items.Refresh();
					UpdateFileSelectStatus();
					continue;
				}
				Progress<int> progress = new Progress<int>(delegate(int pct)
				{
					int num2 = (batchIdx * 100 + pct) / batches.Count;
					FileSelectProgress.Value = num2;
					FileSelectStatus.Text = string.Format(UILocalizer.Tr("batch_progress"), batchIdx + 1, batches.Count, pct, num2);
				});
				try
				{
					await Task.Run(() => _translator.TranslateBatchAsync(toTranslate, progress, _cts?.Token ?? CancellationToken.None), _cts?.Token ?? CancellationToken.None);
					if (toTranslate.Count((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText) && s.TranslatedText != s.OriginalText) == 0)
					{
						string value = _translator.LastError ?? "未知错误";
						FileSelectStatus.Text = string.Format(UILocalizer.Tr("batch_failed"), batchIdx + 1, value);
						foreach (FileTranslateItem item3 in batch)
						{
							item3.IsSelected = false;
						}
						FileListBox.Items.Refresh();
						UpdateFileSelectStatus();
						FileSelectProgress.Visibility = Visibility.Collapsed;
						return;
					}
					foreach (FileTranslateItem item4 in batch)
					{
						item4.IsSelected = false;
						item4.Status = FileTransStatus.Translated;
					}
					FileListBox.Items.Refresh();
					UpdateFileSelectStatus();
					UpdateTranslationsJson();
				}
				catch (Exception ex)
				{
					FileSelectStatus.Text = string.Format(UILocalizer.Tr("batch_error"), batchIdx + 1, ex.Message);
					foreach (FileTranslateItem item5 in batch)
					{
						item5.IsSelected = false;
					}
					FileListBox.Items.Refresh();
					UpdateFileSelectStatus();
					FileSelectProgress.Visibility = Visibility.Collapsed;
					return;
				}
			}
			FileSelectProgress.Value = 100.0;
			FileSelectProgress.Visibility = Visibility.Collapsed;
			FileSelectStatus.Text = UILocalizer.Tr("status_all_done_packing");
			await AutoPackAsync();
		}

		private void UpdateFileSelectStatus()
		{
			if (_fileItems != null)
			{
				int count = _fileItems.Count;
				int num = _fileItems.Count((FileTranslateItem f) => f.Status == FileTransStatus.Translated);
				int value = count - num;
				FileSelectStatus.Text = string.Format(UILocalizer.Tr("file_count"), count, num, value);
				PackFilesBtn.IsEnabled = num > 0;
			}
		}

		private async Task AutoPackAsync()
		{
			if (_fsModDir == null || _yabberDir == null || _foundStrings == null)
			{
				return;
			}
			ShowPage("applying");
			ApplyResultText.Text = UILocalizer.Tr("status_writing_xml");
			Dictionary<string, List<FoundString>> dictionary = new Dictionary<string, List<FoundString>>();
			foreach (FoundString item2 in _foundStrings.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)))
			{
				if (!string.IsNullOrEmpty(item2.XmlFilePath) && !string.IsNullOrEmpty(item2.TextId))
				{
					if (!dictionary.ContainsKey(item2.XmlFilePath))
					{
						dictionary[item2.XmlFilePath] = new List<FoundString>();
					}
					dictionary[item2.XmlFilePath].Add(item2);
				}
			}
			int num = 0;
			foreach (KeyValuePair<string, List<FoundString>> item3 in dictionary)
			{
				if (!File.Exists(item3.Key))
				{
					continue;
				}
				try
				{
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.Load(item3.Key);
					foreach (FoundString item4 in item3.Value)
					{
						XmlNodeList xmlNodeList = xmlDocument.SelectNodes("//text[@id='" + item4.TextId + "']");
						if (xmlNodeList != null && xmlNodeList.Count > 0)
						{
							xmlNodeList[0].InnerText = item4.TranslatedText;
							num++;
						}
					}
					xmlDocument.Save(item3.Key);
				}
				catch
				{
				}
			}
			ApplyResultText.Text = string.Format(UILocalizer.Tr("status_packed"), num);
			bool item = (await RunYabberPackAsync()).Item1;
			ApplyProgressBar.Visibility = Visibility.Collapsed;
			if (item)
			{
				ApplyResultText.Text += UILocalizer.Tr("status_done_packed");
				ApplyResultText.Text += "✅ 翻译流程全部结束\n";
				ApplyResultText.ScrollToEnd();
				StatusText.Text = UILocalizer.Tr("status_done");
			}
			else
			{
				StatusText.Text = UILocalizer.Tr("status_pack_failed");
			}
		}

		private void UpdateTranslationsJson()
		{
			if (_fsTransPath != null && _foundStrings != null)
			{
				string contents = JsonSerializer.Serialize(_foundStrings.Select(delegate(FoundString s)
				{
					Dictionary<string, object> dictionary = new Dictionary<string, object>();
					string sourceFile = s.SourceFile;
					dictionary["fmg_file"] = ((sourceFile != null) ? sourceFile.Split('[')[0] : null) ?? "";
					dictionary["xml_path"] = s.XmlFilePath ?? "";
					dictionary["fmg_dir"] = Path.GetDirectoryName(s.XmlFilePath ?? "") ?? "";
					dictionary["dcx_file"] = "";
					dictionary["text_id"] = s.TextId ?? "";
					dictionary["original_text"] = s.OriginalText ?? "";
					dictionary["translated_text"] = s.TranslatedText ?? "";
					return dictionary;
				}).ToList(), new JsonSerializerOptions
				{
					WriteIndented = true
				});
				File.WriteAllText(_fsTransPath, contents, Encoding.UTF8);
			}
		}

		private async void PackFilesBtn_Click(object sender, RoutedEventArgs e)
		{
			if (_fsModDir == null || _yabberDir == null)
			{
				return;
			}
			List<FoundString> list = _foundStrings?.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)).ToList();
			if (list == null || list.Count == 0)
			{
				MessageBox.Show("还没有翻译任何文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				if (MessageBox.Show($"将打包 {_fileItems?.Count((FileTranslateItem f) => f.Status == FileTransStatus.Translated) ?? 0} 个已翻译的文件回 msgbnd.dcx。\n未翻译的文件将被跳过。\n是否继续？", "打包翻译文件", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				{
					return;
				}
				ShowPage("applying");
				ApplyResultText.Text = UILocalizer.Tr("status_writing");
				Dictionary<string, List<FoundString>> dictionary = new Dictionary<string, List<FoundString>>();
				foreach (FoundString item2 in _foundStrings.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)))
				{
					if (!string.IsNullOrEmpty(item2.XmlFilePath) && !string.IsNullOrEmpty(item2.TextId))
					{
						if (!dictionary.ContainsKey(item2.XmlFilePath))
						{
							dictionary[item2.XmlFilePath] = new List<FoundString>();
						}
						dictionary[item2.XmlFilePath].Add(item2);
					}
				}
				int num = 0;
				foreach (KeyValuePair<string, List<FoundString>> item3 in dictionary)
				{
					if (!File.Exists(item3.Key))
					{
						continue;
					}
					try
					{
						XmlDocument xmlDocument = new XmlDocument();
						xmlDocument.Load(item3.Key);
						foreach (FoundString item4 in item3.Value)
						{
							XmlNodeList xmlNodeList = xmlDocument.SelectNodes("//text[@id='" + item4.TextId + "']");
							if (xmlNodeList != null && xmlNodeList.Count > 0)
							{
								xmlNodeList[0].InnerText = item4.TranslatedText;
								num++;
							}
						}
						xmlDocument.Save(item3.Key);
					}
					catch
					{
					}
				}
				ApplyResultText.Text = string.Format(UILocalizer.Tr("status_packed"), num);
				bool item = (await RunYabberPackAsync()).Item1;
				ApplyProgressBar.Visibility = Visibility.Collapsed;
				if (item)
				{
					ApplyResultText.Text += UILocalizer.Tr("status_done_packed");
					ApplyResultText.Text += "✅ 翻译流程全部结束\n";
					ApplyResultText.ScrollToEnd();
					StatusText.Text = UILocalizer.Tr("status_done");
				}
				else
				{
					StatusText.Text = UILocalizer.Tr("status_pack_failed");
				}
			}
		}

		private async Task ApplyFromSoftwareMod()
		{
			if (_foundStrings == null || _fsModDir == null || _yabberDir == null || _fsTransPath == null)
			{
				return;
			}
			List<FoundString> list = _foundStrings.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)).ToList();
			if (list.Count == 0)
			{
				MessageBox.Show("还没有翻译任何字符串！", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				if (MessageBox.Show($"将翻译 {list.Count} 个字符串并打包回 msgbnd.dcx。\n是否继续？", "确认翻译", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
				{
					return;
				}
				ShowPage("applying");
				ApplyResultText.Text = UILocalizer.Tr("status_writing") + " " + UILocalizer.Tr("status_packing");
				try
				{
					Dictionary<string, List<FoundString>> dictionary = new Dictionary<string, List<FoundString>>();
					foreach (FoundString item2 in _foundStrings.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)))
					{
						if (!string.IsNullOrEmpty(item2.XmlFilePath) && !string.IsNullOrEmpty(item2.TextId))
						{
							if (!dictionary.ContainsKey(item2.XmlFilePath))
							{
								dictionary[item2.XmlFilePath] = new List<FoundString>();
							}
							dictionary[item2.XmlFilePath].Add(item2);
						}
					}
					int num = 0;
					foreach (KeyValuePair<string, List<FoundString>> item3 in dictionary)
					{
						string key = item3.Key;
						if (!File.Exists(key))
						{
							continue;
						}
						try
						{
							XmlDocument xmlDocument = new XmlDocument();
							xmlDocument.Load(key);
							foreach (FoundString item4 in item3.Value)
							{
								XmlNodeList xmlNodeList = xmlDocument.SelectNodes("//text[@id='" + item4.TextId + "']");
								if (xmlNodeList != null && xmlNodeList.Count > 0)
								{
									xmlNodeList[0].InnerText = item4.TranslatedText;
									num++;
								}
							}
							xmlDocument.Save(key);
						}
						catch
						{
						}
					}
					ApplyResultText.Text = string.Format(UILocalizer.Tr("status_packed"), num);
					bool item = (await RunYabberPackAsync()).Item1;
					ApplyProgressBar.Visibility = Visibility.Collapsed;
					if (item)
					{
						ApplyResultText.Text += UILocalizer.Tr("status_done_packed");
						ApplyResultText.Text += "✅ 翻译流程全部结束\n";
						ApplyResultText.ScrollToEnd();
						StatusText.Text = UILocalizer.Tr("status_done");
					}
					else
					{
						StatusText.Text = UILocalizer.Tr("status_pack_failed");
					}
				}
				catch (Exception ex)
				{
					ApplyResultText.Text = "❌ 出错: " + ex.Message;
					StatusText.Text = UILocalizer.Tr("status_trans_failed");
				}
			}
		}

		private async void ModBtn_Click(object sender, RoutedEventArgs e)
		{
			int selectedIndex = GameCombo.SelectedIndex;
			if (selectedIndex < 0 || selectedIndex >= AppSettings.Games.Length)
			{
				MessageBox.Show("请先选择一个游戏模板", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			_appSettings.CurrentGame = AppSettings.Games[selectedIndex];
			int selectedIndex2 = LangCombo.SelectedIndex;
			List<string> list = AppSettings.Languages.Keys.ToList();
			if (selectedIndex2 >= 0 && selectedIndex2 < list.Count)
			{
				_appSettings.TargetLanguage = list[selectedIndex2];
			}
			SettingsService.Save(_appSettings);
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = "选择 MOD 文件夹（FromSoftware 请选 msg/engus 目录）"
			};
			if (!openFolderDialog.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			string folderName = openFolderDialog.FolderName;
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Yabber");
			if (!Directory.Exists(text))
				text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "Yabber");
			bool flag = false;
			string text2 = Path.GetFileName(folderName).ToLower();
			string text3 = Directory.GetParent(folderName)?.Name?.ToLower() ?? "";
			bool flag2 = false;
			DirectoryInfo directoryInfo = new DirectoryInfo(folderName);
			for (int i = 0; i < 5; i++)
			{
				if (directoryInfo == null)
				{
					break;
				}
				if (directoryInfo.GetFiles("*.msgbnd.dcx").Length != 0)
				{
					flag2 = true;
					break;
				}
				directoryInfo = directoryInfo.Parent;
			}
			int num;
			switch (text2)
			{
			default:
				num = ((text3 == "msg") ? 1 : 0);
				break;
			case "engus":
			case "eng_us":
			case "english":
				num = 1;
				break;
			}
			string text4;
			if (((uint)num | (flag2 ? 1u : 0u)) != 0)
			{
				flag = true;
				DirectoryInfo parent = Directory.GetParent(folderName);
				text4 = ((parent == null || parent.Parent == null) ? folderName : parent.Parent.FullName);
			}
			else
			{
				text4 = folderName;
			}
			ShowPage("scanning");
			ScanDetailText.Text = "正在扫描 MOD: " + text4;
			StatusText.Text = "\ud83d\udce6 MOD 模式 - " + Path.GetFileName(text4);
			try
			{
				if (!flag || !File.Exists(Path.Combine(text, "Yabber.exe")))
				{
					await HandleGenericMod(text4);
				}
				else
				{
					await HandleFromSoftwareMod(text4, text);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("扫描出错: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
				ShowPage("welcome");
			}
		}

		private async Task HandleFromSoftwareMod(string modDir, string yabberDir)
		{
			string modDir2 = modDir;
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n", _appSettings.CurrentGame);
			string officialEngDir = Path.Combine(path, "engus");
			string officialZhocnDir = Path.Combine(path, _appSettings.TargetLanguage);
			_fsModDir = modDir2;
			_yabberDir = yabberDir;
			bool flag = Directory.Exists(officialEngDir) && Directory.GetFiles(officialEngDir, "*.msgbnd.dcx").Length != 0;
			bool flag2 = Directory.Exists(officialEngDir) && Directory.GetDirectories(officialEngDir, "*-msgbnd-dcx").Length != 0;
			if ((!flag && !flag2) || !Directory.Exists(officialZhocnDir))
			{
				ScanFileText.Text = "⚠\ufe0f 未找到游戏模板文件\n请将原始 .msgbnd.dcx 文件放在 " + officialEngDir + " 目录下";
				StatusText.Text = UILocalizer.Tr("status_no_ref");
				return;
			}
			_fsTransPath = Path.Combine(modDir2, "_translations.json");
			if (File.Exists(_fsTransPath))
			{
				File.Delete(_fsTransPath);
			}
			string targetLang = _appSettings.TargetLanguage;
			string workDir = Path.Combine(modDir2, "msg", targetLang);
			FindFsModBat();
			string yabberExe = Path.Combine(yabberDir, "Yabber.exe");
			if (flag && File.Exists(yabberExe))
			{
				ShowPage("scanning");
				ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step0"));
				StatusText.Text = UILocalizer.Tr("status_unpack_ref");
				await EnsureReferenceUnpackedAsync(officialEngDir, yabberExe);
				if (Directory.GetFiles(officialEngDir, "*.fmg.xml", SearchOption.AllDirectories).Length == 0)
				{
					ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_unpack_failed") + "\nCheck Yabber.exe";
					StatusText.Text = UILocalizer.Tr("status_unpack_failed");
					return;
				}
			}
			else if (flag2 && Directory.GetFiles(officialEngDir, "*.fmg.xml", SearchOption.AllDirectories).Length == 0)
			{
				ScanFileText.Text = "⚠\ufe0f engus 目录存在 dcx 文件夹但缺少 .fmg.xml 文件\n请删除 engus 目录内的 dcx 文件夹，重新运行";
				StatusText.Text = UILocalizer.Tr("status_ref_incomplete");
				return;
			}
			ShowPage("scanning");
			ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step1"), targetLang);
			StatusText.Text = UILocalizer.Tr("status_unpack_mod");
			ScanDetailText.Text = "";
			Directory.CreateDirectory(workDir);
			string[] files = Directory.GetFiles(Path.Combine(modDir2, "msg", "engus"), "*.msgbnd.dcx");
			ScanDetailText.Text = $"在 engus 中找到 {files.Length} 个 .dcx 文件";
			if (files.Length == 0)
			{
				ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_no_mod_files");
				StatusText.Text = UILocalizer.Tr("status_no_mod_files");
				return;
			}
			string[] array = files;
			foreach (string text in array)
			{
				string destFileName = Path.Combine(workDir, Path.GetFileName(text));
				File.Copy(text, destFileName, overwrite: true);
				ScanDetailText.Text += $"\n  复制: {Path.GetFileName(text)} → {targetLang}/";
			}
			string[] files2 = Directory.GetFiles(workDir, "*.msgbnd.dcx");
			foreach (string dcx2 in files2)
			{
				TextBlock scanDetailText = ScanDetailText;
				scanDetailText.Text = scanDetailText.Text + "\n  解包: " + Path.GetFileName(dcx2) + "...";
				(int, string) tuple = await RunYabberAsync(yabberExe, dcx2);
				if (tuple.Item1 != 0)
				{
					ScanDetailText.Text += $" exit={tuple.Item1}";
					ScanFileText.Text = $"⚠\ufe0f 解包失败: {Path.GetFileName(dcx2)} (exit={tuple.Item1})";
					StatusText.Text = UILocalizer.Tr("status_unpack_failed");
					return;
				}
				ScanDetailText.Text += " OK";
			}
			string[] unpackedDirs = Directory.GetDirectories(workDir, "*-msgbnd-dcx");
			TextBlock scanDetailText2 = ScanDetailText;
			scanDetailText2.Text = scanDetailText2.Text + "\n  解包后文件夹: " + string.Join(", ", unpackedDirs.Select(Path.GetFileName));
			files2 = unpackedDirs;
			foreach (string text2 in files2)
			{
				string dcx2 = new string[2]
				{
					Path.Combine(text2, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(text2, "GR", "data", "INTERROOT_win64", "msg", "engUS")
				}.FirstOrDefault(Directory.Exists);
				if (dcx2 == null)
				{
					TextBlock scanDetailText3 = ScanDetailText;
					scanDetailText3.Text = scanDetailText3.Text + "\n  [WARN] " + Path.GetFileName(text2) + ": 未找到 FMG 目录";
					continue;
				}
				string[] files3 = Directory.GetFiles(dcx2, "*.fmg");
				ScanDetailText.Text += $"\n  {Path.GetFileName(text2)}: 找到 {files3.Length} 个 .fmg";
				string[] array2 = files3;
				foreach (string fmg2 in array2)
				{
					if (!File.Exists(fmg2 + ".xml"))
					{
						(int, string) tuple2 = await RunYabberAsync(yabberExe, fmg2, dcx2);
						if (tuple2.Item1 != 0)
						{
							ScanDetailText.Text += $"\n    [WARN] {Path.GetFileName(fmg2)} 解包失败 (exit={tuple2.Item1})";
						}
					}
				}
				int value = Directory.GetFiles(dcx2, "*.fmg.xml").Length;
				ScanDetailText.Text += $" → {value} 个 .fmg.xml";
			}
			if (unpackedDirs.Length == 0)
			{
				ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_mod_unpack_failed");
				StatusText.Text = UILocalizer.Tr("status_mod_unpack_failed");
				return;
			}
			ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step2"));
			List<(string, string)> list = new List<(string, string)>();
			array = Directory.GetDirectories(workDir, "*-msgbnd-dcx");
			foreach (string text3 in array)
			{
				string fileName = Path.GetFileName(text3);
				string text4 = new string[2]
				{
					Path.Combine(text3, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(text3, "GR", "data", "INTERROOT_win64", "msg", "engUS")
				}.FirstOrDefault(Directory.Exists);
				if (text4 != null)
				{
					string path2 = new string[4]
					{
						Path.Combine(officialEngDir, fileName, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
						Path.Combine(officialEngDir, fileName, "GR", "data", "INTERROOT_win64", "msg", "engUS"),
						Path.Combine(officialEngDir, fileName, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhoCN"),
						Path.Combine(officialEngDir, fileName, "GR", "data", "INTERROOT_win64", "msg", "zhoCN")
					}.FirstOrDefault(Directory.Exists) ?? "";
					string[] files4 = Directory.GetFiles(text4, "*.fmg.xml");
					foreach (string text5 in files4)
					{
						string fileName2 = Path.GetFileName(text5);
						string item = Path.Combine(path2, fileName2);
						list.Add((text5, item));
					}
				}
			}
			if (list.Count == 0)
			{
				ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_no_text");
				StatusText.Text = UILocalizer.Tr("status_no_text");
				return;
			}
			List<(string modPath, string refPath)> diffEntries = new List<(string, string)>();
			foreach (var item5 in list)
			{
				if (!File.Exists(item5.Item2))
				{
					diffEntries.Add(item5);
				}
				else if (new FileInfo(item5.Item1).Length != new FileInfo(item5.Item2).Length)
				{
					diffEntries.Add(item5);
				}
			}
			ScanDetailText.Text = $"共 {list.Count} 个 XML 文件（来自 {Directory.GetDirectories(workDir, "*-msgbnd-dcx").Length} 个 dcx 包），其中 {diffEntries.Count} 个被 MOD 修改过";
			foreach (var item6 in diffEntries)
			{
				string item2 = item6.modPath;
				ScanDetailText.Text += $"\n  [差异] {Path.GetFileName(item2)} (dcx: {new DirectoryInfo(item2).Parent?.Parent?.Parent?.Parent?.Parent?.Parent?.Parent?.Name ?? "?"})";
			}
			ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step3"));
			_foundStrings = new List<FoundString>();
			int num = 0;
			foreach (var (text6, text7) in diffEntries)
			{
				try
				{
					XmlDocument xmlDocument = new XmlDocument();
					xmlDocument.Load(text6);
					XmlNode xmlNode = xmlDocument.SelectSingleNode("//entries");
					if (xmlNode == null)
					{
						TextBlock scanDetailText4 = ScanDetailText;
						scanDetailText4.Text = scanDetailText4.Text + "\n  [SKIP] " + Path.GetFileName(text6) + ": no //entries node";
						continue;
					}
					XmlNode xmlNode2 = null;
					if (File.Exists(text7))
					{
						XmlDocument xmlDocument2 = new XmlDocument();
						xmlDocument2.Load(text7);
						xmlNode2 = xmlDocument2.SelectSingleNode("//entries");
					}
					int num2 = 0;
					int num3 = 0;
					foreach (XmlElement item7 in xmlNode.SelectNodes("text"))
					{
						string attribute = item7.GetAttribute("id");
						string text8 = (item7.InnerText ?? "").Trim();
						if (string.IsNullOrEmpty(text8) || text8 == "%null%")
						{
							continue;
						}
						num3++;
						if (xmlNode2 != null)
						{
							string text9 = (xmlNode2.SelectSingleNode("text[@id='" + attribute + "']")?.InnerText ?? "").Trim();
							if (text8 == text9)
							{
								continue;
							}
						}
						_foundStrings.Add(new FoundString
						{
							SourceFile = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(text6)) + "[" + attribute + "]",
							OriginalText = text8,
							XmlFilePath = text6,
							TextId = attribute
						});
						num2++;
					}
					ScanDetailText.Text += $"\n  {Path.GetFileName(text6)}: 总共 {num3} 条非空文本, {num2} 条与官方不同";
					num += num2;
				}
				catch (Exception ex)
				{
					TextBlock scanDetailText5 = ScanDetailText;
					scanDetailText5.Text = scanDetailText5.Text + "\n  [ERR] " + Path.GetFileName(text6) + ": " + ex.Message;
				}
			}
			if (_foundStrings.Count == 0)
			{
				ShowPage("applying");
				ApplyResultText.Text = "✅ 所有文件与官方无差异，无需翻译";
				ApplyResultText.Text = "无需翻译，直接使用官方中文文件";
				StatusText.Text = UILocalizer.Tr("status_no_translate");
				return;
			}
			ScanDetailText.Text = $"需要翻译 {_foundStrings.Count} 个字符串（仅限 MOD 实际修改的条目，非整文件）";
			ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step4"));
			string text10 = targetLang;
			string text11 = ((text10 == "zhocn") ? "zhoCN" : ((!(text10 == "zhotw")) ? targetLang : "zhoTW"));
			string langFolder = text11;
			string[] files5 = Directory.GetFiles(officialZhocnDir, "*.msgbnd.dcx");
			if (files5.Length != 0 && File.Exists(yabberExe))
			{
				ScanDetailText.Text = "\n解包官方 zhocn 参考文件...";
				files2 = files5;
				foreach (string dcx2 in files2)
				{
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dcx2));
					string fmg2 = Path.Combine(officialZhocnDir, fileNameWithoutExtension + "-msgbnd-dcx");
					bool flag3 = false;
					if (Directory.Exists(fmg2))
					{
						string text12 = new string[8]
						{
							Path.Combine(fmg2, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
							Path.Combine(fmg2, "GR", "data", "INTERROOT_win64", "msg", "engUS"),
							Path.Combine(fmg2, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhoCN"),
							Path.Combine(fmg2, "GR", "data", "INTERROOT_win64", "msg", "zhoCN"),
							Path.Combine(fmg2, "CL", "data", "Target", "INTERROOT_win64", "msg", langFolder),
							Path.Combine(fmg2, "GR", "data", "INTERROOT_win64", "msg", langFolder),
							Path.Combine(fmg2, "CL", "data", "Target", "INTERROOT_win64", "msg", targetLang),
							Path.Combine(fmg2, "GR", "data", "INTERROOT_win64", "msg", targetLang)
						}.FirstOrDefault(Directory.Exists);
						if (text12 != null)
						{
							int num4 = Directory.GetFiles(text12, "*.fmg").Length;
							int num5 = Directory.GetFiles(text12, "*.fmg.xml").Length;
							if (num4 > 0 && num5 == 0)
							{
								flag3 = true;
							}
						}
						else
						{
							flag3 = true;
						}
					}
					if (!flag3 && Directory.Exists(fmg2))
					{
						continue;
					}
					if (flag3)
					{
						try
						{
							Directory.Delete(fmg2, recursive: true);
						}
						catch
						{
						}
						TextBlock scanDetailText6 = ScanDetailText;
						scanDetailText6.Text = scanDetailText6.Text + "\n  [重新解包] " + Path.GetFileName(dcx2);
					}
					TextBlock scanDetailText7 = ScanDetailText;
					scanDetailText7.Text = scanDetailText7.Text + "\n  [AUTO] 解包 zhocn: " + Path.GetFileName(dcx2);
					(int, string) tuple4 = await RunYabberAsync(yabberExe, dcx2);
					if (tuple4.Item1 != 0)
					{
						ScanDetailText.Text += $"\n  [WARN] 解包失败: {Path.GetFileName(dcx2)} (exit={tuple4.Item1})";
					}
					string fmgDir = findFmgDir(fmg2);
					if (fmgDir == null)
					{
						continue;
					}
					string[] array2 = Directory.GetFiles(fmgDir, "*.fmg");
					foreach (string text13 in array2)
					{
						if (!File.Exists(text13 + ".xml"))
						{
							await RunYabberAsync(yabberExe, text13, fmgDir);
						}
					}
				}
			}
			array = Directory.GetDirectories(workDir);
			foreach (string path3 in array)
			{
				try
				{
					Directory.Delete(path3, recursive: true);
				}
				catch
				{
				}
			}
			array = Directory.GetFiles(workDir);
			foreach (string path4 in array)
			{
				try
				{
					File.Delete(path4);
				}
				catch
				{
				}
			}
			string[] files6 = Directory.GetFiles(Path.Combine(modDir2, "msg", "engus"), "*.msgbnd.dcx");
			HashSet<string> hashSet = files6.Select((string f) => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f))).ToHashSet();
			array = files6;
			for (int i = 0; i < array.Length; i++)
			{
				string fileNameWithoutExtension2 = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(array[i]));
				string text14 = Path.Combine(officialZhocnDir, fileNameWithoutExtension2 + "-msgbnd-dcx");
				if (Directory.Exists(text14))
				{
					CopyDirectory(text14, Path.Combine(workDir, fileNameWithoutExtension2 + "-msgbnd-dcx"));
					ScanDetailText.Text = "\n  已复制: " + fileNameWithoutExtension2;
				}
			}
			array = Directory.GetDirectories(workDir, "*-msgbnd-dcx");
			foreach (string path5 in array)
			{
				string item3 = Path.GetFileName(path5).Replace("-msgbnd-dcx", "");
				if (!hashSet.Contains(item3))
				{
					try
					{
						Directory.Delete(path5, recursive: true);
					}
					catch
					{
					}
				}
			}
			ScanFileText.Text = string.Format(UILocalizer.Tr("mod_step5"), diffEntries.Count);
			_appSettings = SettingsService.Load();
			_cachedApiKey = _appSettings.ResolvedApiKey;
			if (string.IsNullOrEmpty(_cachedApiKey))
			{
				ShowPage("applying");
				ApplyResultText.Text = "⚠\ufe0f 未设置 API 密钥，无法翻译修改的文本";
				StatusText.Text = UILocalizer.Tr("status_no_api");
				return;
			}
			string valueOrDefault = AppSettings.LanguagePrompts.GetValueOrDefault(_appSettings.TargetLanguage, "简体中文");
			_translator = new TranslationService(_appSettings, valueOrDefault);
			ShowPage("applying");
			ApplyResultText.Text = "";
			int totalStrings = _foundStrings.Count;
			Dictionary<string, List<FoundString>> stringsByFile = (from s in _foundStrings
				where !string.IsNullOrEmpty(s.XmlFilePath)
				group s by Path.GetFileName(s.XmlFilePath)).ToDictionary<IGrouping<string, FoundString>, string, List<FoundString>>((IGrouping<string, FoundString> g) => g.Key, (IGrouping<string, FoundString> g) => g.ToList(), StringComparer.OrdinalIgnoreCase);
			HashSet<string> completedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Progress<int> translateProgress = new Progress<int>(delegate(int pct)
			{
				ApplyProgressBar.Value = pct;
				int value3 = pct * totalStrings / 100;
				ApplyPercentageText.Text = string.Format(UILocalizer.Tr("trans_progress"), pct, value3, totalStrings);
				foreach (KeyValuePair<string, List<FoundString>> item8 in stringsByFile)
				{
					if (!completedFiles.Contains(item8.Key) && item8.Value.All((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)))
					{
						completedFiles.Add(item8.Key);
						TextBox applyResultText2 = ApplyResultText;
						applyResultText2.Text = applyResultText2.Text + "  " + item8.Key + " " + UILocalizer.Tr("status_done") + "\n";
					}
				}
			});
			try
			{
				await Task.Run(() => _translator.TranslateBatchAsync(_foundStrings, translateProgress, _cts?.Token ?? CancellationToken.None), _cts?.Token ?? CancellationToken.None);
				int translatedCount = _foundStrings.Count((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText) && s.TranslatedText != s.OriginalText);
				if (translatedCount == 0)
				{
					string err = _translator.LastError ?? "未知错误";
					base.Dispatcher.Invoke(delegate
					{
						ApplyResultText.Text = "❌ 翻译失败: " + err;
						StatusText.Text = UILocalizer.Tr("status_trans_failed");
					});
					return;
				}
				ApplyPercentageText.Text = string.Format(UILocalizer.Tr("trans_done"), translatedCount, _foundStrings.Count);
				ApplyProgressBar.Value = 100.0;
				List<string> values = await Task.Run(delegate
				{
					Dictionary<string, List<FoundString>> dictionary = new Dictionary<string, List<FoundString>>();
					string text15 = targetLang;
					if (!(text15 == "zhocn"))
					{
						if (text15 == "zhotw")
						{
							string text16 = "zhoTW";
						}
						else
						{
							string text16 = targetLang;
						}
					}
					else
					{
						string text16 = "zhoCN";
					}
					Dictionary<string, string> dictionary2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					string path6 = Path.Combine(modDir2, "msg", "engus");
					List<string> list2 = new List<string>();
					foreach (FoundString item9 in _foundStrings.Where((FoundString s) => !string.IsNullOrEmpty(s.TranslatedText)))
					{
						if (!string.IsNullOrEmpty(item9.XmlFilePath) && !string.IsNullOrEmpty(item9.TextId))
						{
							string fileName3 = Path.GetFileName(item9.XmlFilePath);
							if (!dictionary2.TryGetValue(fileName3, out var value2))
							{
								string[] files7 = Directory.GetFiles(workDir, fileName3, SearchOption.AllDirectories);
								if (files7.Length != 0)
								{
									value2 = files7[0];
								}
								else
								{
									if (Directory.Exists(path6))
									{
										files7 = Directory.GetFiles(path6, fileName3, SearchOption.AllDirectories);
										if (files7.Length != 0)
										{
											value2 = files7[0];
										}
									}
									if (value2 == null && Directory.Exists(officialEngDir))
									{
										files7 = Directory.GetFiles(officialEngDir, fileName3, SearchOption.AllDirectories);
										if (files7.Length != 0)
										{
											string text17 = files7[0];
											string text18 = "-msgbnd-dcx";
											int num6 = text17.IndexOf(text18);
											if (num6 >= 0)
											{
												int num7 = text17.LastIndexOf(Path.DirectorySeparatorChar, num6);
												string text19 = ((num7 >= 0) ? text17.Substring(num7 + 1, num6 + text18.Length - num7 - 1) : "");
												if (!string.IsNullOrEmpty(text19) && text19.EndsWith(text18))
												{
													int num8 = num6 + text18.Length + 1;
													string path7 = ((num8 < text17.Length) ? text17.Substring(num8) : "");
													string text20 = Path.Combine(Path.Combine(workDir, text19), path7);
													text15 = targetLang;
													string text16 = ((text15 == "zhocn") ? "zhoCN" : ((!(text15 == "zhotw")) ? targetLang : "zhoTW"));
													string newValue = text16;
													text20 = text20.Replace("engUS", newValue);
													text20 = text20.Replace("engus", newValue);
													text20 = text20.Replace("EngUS", newValue);
													Directory.CreateDirectory(Path.GetDirectoryName(text20));
													File.Copy(text17, text20, overwrite: true);
													value2 = text20;
													list2.Add("  [COPY] " + fileName3 + " -> 已从英文参考复制到目标语言目录");
												}
											}
											if (value2 == null)
											{
												value2 = text17;
											}
										}
									}
								}
								if (value2 == null)
								{
									value2 = item9.XmlFilePath;
								}
								dictionary2[fileName3] = value2;
							}
							if (!dictionary.ContainsKey(value2))
							{
								dictionary[value2] = new List<FoundString>();
							}
							dictionary[value2].Add(item9);
						}
					}
					list2.Add($"\n翻译完成，共 {translatedCount} 条，写入 {dictionary.Count} 个文件");
					int num9 = 0;
					foreach (KeyValuePair<string, List<FoundString>> item10 in dictionary)
					{
						if (!File.Exists(item10.Key))
						{
							list2.Add("[MISS] " + Path.GetFileName(item10.Key) + " 不存在");
						}
						else
						{
							try
							{
								XmlDocument xmlDocument3 = new XmlDocument();
								xmlDocument3.Load(item10.Key);
								XmlNode xmlNode3 = xmlDocument3.SelectSingleNode("//entries");
								int num10 = 0;
								foreach (FoundString item11 in item10.Value)
								{
									XmlNodeList xmlNodeList = xmlDocument3.SelectNodes("//text[@id='" + item11.TextId + "']");
									if (xmlNodeList != null && xmlNodeList.Count > 0)
									{
										xmlNodeList[0].InnerText = item11.TranslatedText;
										num9++;
										num10++;
									}
									else if (xmlNode3 != null)
									{
										XmlElement xmlElement = xmlDocument3.CreateElement("text");
										XmlAttribute xmlAttribute = xmlDocument3.CreateAttribute("id");
										xmlAttribute.Value = item11.TextId;
										xmlElement.Attributes.Append(xmlAttribute);
										xmlElement.InnerText = item11.TranslatedText;
										xmlNode3.AppendChild(xmlElement);
										num9++;
										num10++;
									}
								}
								xmlDocument3.Save(item10.Key);
								list2.Add($"  ✅ {Path.GetFileName(item10.Key)}: {num10}/{item10.Value.Count} 条");
							}
							catch (Exception ex3)
							{
								list2.Add("  [ERR] " + Path.GetFileName(item10.Key) + ": " + ex3.Message);
							}
						}
					}
					list2.Add($"\n共写入 {num9} 条");
					return list2;
				});
				TextBox applyResultText = ApplyResultText;
				applyResultText.Text = applyResultText.Text + string.Join("\n", values) + "\n";
				ApplyResultText.Text += UILocalizer.Tr("status_writing") + "，正在调用 Yabber 打包...\n";
				ApplyResultText.ScrollToEnd();
				bool item4 = (await RunYabberPackAsync()).Item1;
				ApplyProgressBar.Visibility = Visibility.Collapsed;
				if (item4)
				{
					ApplyResultText.Text += UILocalizer.Tr("status_done_packed");
					ApplyResultText.Text += "✅ 翻译流程全部结束\n";
					ApplyResultText.ScrollToEnd();
					StatusText.Text = UILocalizer.Tr("status_done");
				}
				else
				{
					StatusText.Text = UILocalizer.Tr("status_pack_failed");
				}
			}
			catch (Exception ex2)
			{
				ApplyResultText.Text = "❌ 出错: " + ex2.Message;
				StatusText.Text = UILocalizer.Tr("status_trans_failed");
			}
			string? findFmgDir(string d)
			{
				return new string[10]
				{
					Path.Combine(d, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(d, "GR", "data", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(d, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhoCN"),
					Path.Combine(d, "GR", "data", "INTERROOT_win64", "msg", "zhoCN"),
					Path.Combine(d, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhocn"),
					Path.Combine(d, "GR", "data", "INTERROOT_win64", "msg", "zhocn"),
					Path.Combine(d, "CL", "data", "Target", "INTERROOT_win64", "msg", langFolder),
					Path.Combine(d, "GR", "data", "INTERROOT_win64", "msg", langFolder),
					Path.Combine(d, "CL", "data", "Target", "INTERROOT_win64", "msg", targetLang),
					Path.Combine(d, "GR", "data", "INTERROOT_win64", "msg", targetLang)
				}.FirstOrDefault(Directory.Exists);
			}
		}

		private static void CopyDirectory(string source, string dest)
		{
			Directory.CreateDirectory(dest);
			string[] files = Directory.GetFiles(source);
			foreach (string text in files)
			{
				File.Copy(text, Path.Combine(dest, Path.GetFileName(text)), overwrite: true);
			}
			files = Directory.GetDirectories(source);
			foreach (string text2 in files)
			{
				CopyDirectory(text2, Path.Combine(dest, Path.GetFileName(text2)));
			}
		}

		private async Task HandleGenericMod(string modDir)
		{
			List<string> list = new List<string>();
			list.AddRange(Directory.GetFiles(modDir, "*.assets", SearchOption.AllDirectories));
			list.AddRange(Directory.GetFiles(modDir, "level*", SearchOption.AllDirectories));
			list.AddRange(Directory.GetFiles(modDir, "*.resource", SearchOption.AllDirectories));
			if (list.Count == 0)
			{
				ScanFileText.Text = "⚠\ufe0f No scanable files found";
				StatusText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_no_text");
				return;
			}
			ScanFileText.Text = $"找到 {list.Count} 个文件，正在扫描字符串...";
			GameInfo modInfo = new GameInfo
			{
				GameName = Path.GetFileName(modDir),
				DataDir = modDir,
				AssetFiles = list,
				IsUnityGame = true,
				EngineType = "MOD (Unity)",
				ManagedDir = ""
			};
			UnityAssetScanner unityAssetScanner = new UnityAssetScanner();
			Progress<int> progress = new Progress<int>(delegate(int p)
			{
				base.Dispatcher.Invoke(() => ScanProgressBar.Value = p);
			});
			_foundStrings = await unityAssetScanner.ScanAllAsync(modInfo, progress);
			_gameInfo = modInfo;
			_patcher = new StringPatcher(modDir);
			if (_foundStrings.Count == 0)
			{
				ScanFileText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_scan_done_no_strings");
				StatusText.Text = "⚠\ufe0f " + UILocalizer.Tr("status_no_text");
				return;
			}
			ShowPage("editor");
			StatusText.Text = $"\ud83d\udce6 MOD 模式 - 找到 {_foundStrings.Count} 个字符串";
			LoadStringsGrid();
		}

		private void BackToSelect_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			ShowPage("welcome");
			StatusText.Text = "就绪";
			UpdateNavActive("welcome");
		}

		private void BackToFileSelect_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			CleanupTranslationResources();
			ShowPage("fileselect");
		}

		private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
		{
			if (_fileItems == null)
			{
				return;
			}
			foreach (FileTranslateItem fileItem in _fileItems)
			{
				if (fileItem.Status == FileTransStatus.Pending)
				{
					fileItem.IsSelected = true;
				}
			}
		}

		private void DeselectAllFiles_Click(object sender, RoutedEventArgs e)
		{
			if (_fileItems == null)
			{
				return;
			}
			foreach (FileTranslateItem fileItem in _fileItems)
			{
				fileItem.IsSelected = false;
			}
		}

		private async Task TranslateUnknownFileNamesAsync()
		{
			if (_fileItems == null)
			{
				return;
			}
			_appSettings = SettingsService.Load();
			string resolvedApiKey = _appSettings.ResolvedApiKey;
			if (string.IsNullOrEmpty(resolvedApiKey))
			{
				return;
			}
			List<string> needsTrans = (from f in _fileItems
				where f.Status == FileTransStatus.Pending && !FmgNameCache.ContainsKey(f.FileName)
				select Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f.FileName))).Distinct().ToList();
			if (needsTrans.Count == 0)
			{
				return;
			}
			try
			{
				using HttpClient http = new HttpClient();
				string text = string.Join("\n", needsTrans.Select((string t, int i) => $"{i + 1}. {t}"));
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				dictionary["model"] = _appSettings.ResolvedModel;
				dictionary["messages"] = new Dictionary<string, string>[2]
				{
					new Dictionary<string, string>
					{
						["role"] = "system",
						["content"] = "你是一个游戏文件名翻译助手。我会给你一个带编号的英文文件名列表。\n请逐行回复中文翻译，每行格式为：编号. 中文翻译\n示例：\n1. 武器名称\n2. 道具说明\n只返回翻译结果，不要包含英文原文。"
					},
					new Dictionary<string, string>
					{
						["role"] = "user",
						["content"] = "翻译以下文件名：\n" + text
					}
				};
				dictionary["temperature"] = 0.1;
				dictionary["max_tokens"] = 2048;
				Dictionary<string, object> value = dictionary;
				HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, _appSettings.ResolvedApiUrl);
				httpRequestMessage.Headers.Add("Authorization", "Bearer " + resolvedApiKey);
				httpRequestMessage.Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
				HttpResponseMessage httpResponseMessage = await http.SendAsync(httpRequestMessage);
				if (!httpResponseMessage.IsSuccessStatusCode)
				{
					return;
				}
				JsonDocument jsonDocument = JsonDocument.Parse(await httpResponseMessage.Content.ReadAsStringAsync());
				string text2 = null;
				if (jsonDocument.RootElement.TryGetProperty("choices", out var value2) && value2.GetArrayLength() > 0 && value2[0].TryGetProperty("message", out var value3) && value3.TryGetProperty("content", out var value4))
				{
					text2 = value4.GetString();
				}
				if (text2 == null)
				{
					return;
				}
				Dictionary<string, string> apiResults = new Dictionary<string, string>();
				string[] array = text2.Split('\n', StringSplitOptions.RemoveEmptyEntries);
				for (int j = 0; j < array.Length; j++)
				{
					Match match = Regex.Match(array[j].Trim(), "^(\\d+)\\s*[\\.\\:\\）\\s]\\s*(.+)");
					if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
					{
						continue;
					}
					string text3 = match.Groups[2].Value.Trim();
					if (!string.IsNullOrEmpty(text3))
					{
						int num = text3.LastIndexOf('→');
						if (num >= 0)
						{
							text3 = text3.Substring(num + 1).Trim();
						}
						int num2 = text3.LastIndexOf("->");
						if (num2 >= 0)
						{
							text3 = text3.Substring(num2 + 2).Trim();
						}
						int num3 = result - 1;
						if (num3 >= 0 && num3 < needsTrans.Count)
						{
							apiResults[needsTrans[num3]] = text3;
						}
					}
				}
				base.Dispatcher.Invoke(delegate
				{
					foreach (KeyValuePair<string, string> item in apiResults)
					{
						FmgNameCache[item.Key] = item.Value;
						foreach (FileTranslateItem fileItem in _fileItems)
						{
							if (Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileItem.FileName)) == item.Key)
							{
								fileItem.ChineseName = item.Value;
							}
						}
					}
					FileListBox.Items.Refresh();
				});
			}
			catch
			{
			}
		}

		private void ShowPage(string page)
		{
			WelcomePage.Visibility = Visibility.Collapsed;
			SponsorPage.Visibility = Visibility.Collapsed;
			ScanningPage.Visibility = Visibility.Collapsed;
			EditorPage.Visibility = Visibility.Collapsed;
			ApplyingPage.Visibility = Visibility.Collapsed;
			FileSelectPage.Visibility = Visibility.Collapsed;
			SettingsPage.Visibility = Visibility.Collapsed;
			if (page == null)
			{
				return;
			}
			switch (page.Length)
			{
			case 7:
				switch (page[0])
				{
				case 'w':
					if (page == "welcome")
					{
						WelcomePage.Visibility = Visibility.Visible;
					}
					break;
				case 's':
					if (page == "sponsor")
					{
						SponsorPage.Visibility = Visibility.Visible;
					}
					break;
				}
				break;
			case 8:
				switch (page[1])
				{
				case 'c':
					if (page == "scanning")
					{
						ScanningPage.Visibility = Visibility.Visible;
						ScanProgressBar.Value = 0.0;
						ApplyProgressBar.Value = 0.0;
					}
					break;
				case 'p':
					if (page == "applying")
					{
						ApplyingPage.Visibility = Visibility.Visible;
						ApplyProgressBar.Value = 0.0;
						ApplyProgressBar.Visibility = Visibility.Visible;
						ApplyResultText.Text = "";
						ApplyPercentageText.Text = "";
					}
					break;
				case 'e':
					if (page == "settings")
					{
						SettingsPage.Visibility = Visibility.Visible;
					}
					break;
				}
				break;
			case 6:
				if (page == "editor")
				{
					EditorPage.Visibility = Visibility.Visible;
				}
				break;
			case 10:
				if (page == "fileselect")
				{
					FileSelectPage.Visibility = Visibility.Visible;
				}
				break;
			}
		}

		private static string? FindPython()
		{
			string[] array = new string[3] { "python.exe", "python3.exe", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python", "python.exe") };
			foreach (string text in array)
			{
				try
				{
					Process process = Process.Start(new ProcessStartInfo(text, "--version")
					{
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true
					});
					if (process != null)
					{
						process.WaitForExit(3000);
						if (process.ExitCode == 0)
						{
							return text;
						}
					}
				}
				catch
				{
				}
			}
			return null;
		}

		private async Task<(bool success, string fullOutput)> RunYabberPackAsync()
		{
			if (_fsModDir == null || _yabberDir == null)
			{
				return (success: false, fullOutput: "_fsModDir or _yabberDir is null");
			}
			string yabberExe = Path.Combine(_yabberDir, "Yabber.exe");
			string targetLang = _appSettings?.TargetLanguage ?? "zhocn";
			string workDir = Path.Combine(_fsModDir, "msg", targetLang);
			_ = _yabberDir;
			(bool, string) result = await Task.Run(delegate
			{
				StringBuilder stringBuilder = new StringBuilder();
				if (!File.Exists(yabberExe))
				{
					stringBuilder.AppendLine("⚠\ufe0f 未找到 Yabber.exe");
					return (false, stringBuilder.ToString());
				}
				stringBuilder.AppendLine("[INFO] 准备打包，目标目录: " + workDir);
				List<string> list = Directory.GetDirectories(workDir).ToList();
				if (list.Count == 0)
				{
					stringBuilder.AppendLine("[WARN] 目标目录中没有子目录，无法打包");
					return (false, stringBuilder.ToString());
				}
				StringBuilder stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
				handler.AppendLiteral("[INFO] 找到 ");
				handler.AppendFormatted(list.Count);
				handler.AppendLiteral(" 个 dcx 文件夹");
				stringBuilder3.AppendLine(ref handler);
				string text = targetLang;
				string text2 = ((text == "zhocn") ? "zhoCN" : ((!(text == "zhotw")) ? targetLang : "zhoTW"));
				string text3 = text2;
				int num = 0;
				int num2 = 0;
				foreach (string item in list)
				{
					string fileName = Path.GetFileName(item);
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
					handler.AppendLiteral("[");
					handler.AppendFormatted(fileName);
					handler.AppendLiteral("] 开始处理...");
					stringBuilder4.AppendLine(ref handler);
					string[] obj = new string[10]
					{
						Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
						Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", "engUS"),
						Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhoCN"),
						Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", "zhoCN"),
						Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhocn"),
						Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", "zhocn"),
						Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", text3),
						Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", text3),
						Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", targetLang),
						Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", targetLang)
					};
					string text4 = null;
					string[] array = obj;
					foreach (string text5 in array)
					{
						if (Directory.Exists(text5))
						{
							text4 = text5;
							break;
						}
					}
					if (text4 == null)
					{
						stringBuilder.AppendLine("  [SKIP] 未找到 FMG 目录");
					}
					else
					{
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder5 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
						handler.AppendLiteral("  FMG 目录: ");
						handler.AppendFormatted(text4);
						stringBuilder5.AppendLine(ref handler);
						string[] files = Directory.GetFiles(text4, "*.fmg.xml");
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder6 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder2);
						handler.AppendLiteral("  找到 ");
						handler.AppendFormatted(files.Length);
						handler.AppendLiteral(" 个 .fmg.xml");
						stringBuilder6.AppendLine(ref handler);
						if (files.Length != 0)
						{
							array = files;
							foreach (string text6 in array)
							{
								string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text6);
								(int, string) tuple = RunSingleYabber(yabberExe, text6, text4);
								if (tuple.Item1 == 0 && File.Exists(Path.Combine(text4, fileNameWithoutExtension)))
								{
									File.Delete(text6);
									stringBuilder2 = stringBuilder;
									StringBuilder stringBuilder7 = stringBuilder2;
									handler = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder2);
									handler.AppendLiteral("  [OK] ");
									handler.AppendFormatted(fileNameWithoutExtension);
									stringBuilder7.AppendLine(ref handler);
								}
								else
								{
									stringBuilder2 = stringBuilder;
									StringBuilder stringBuilder8 = stringBuilder2;
									handler = new StringBuilder.AppendInterpolatedStringHandler(22, 2, stringBuilder2);
									handler.AppendLiteral("  [WARN] ");
									handler.AppendFormatted(fileNameWithoutExtension);
									handler.AppendLiteral(" 回包失败 (exit=");
									handler.AppendFormatted(tuple.Item1);
									handler.AppendLiteral(")");
									stringBuilder8.AppendLine(ref handler);
								}
							}
						}
						string fileName2 = Path.GetFileName(text4);
						stringBuilder2 = stringBuilder;
						StringBuilder stringBuilder9 = stringBuilder2;
						handler = new StringBuilder.AppendInterpolatedStringHandler(23, 2, stringBuilder2);
						handler.AppendLiteral("  [PACK] 打包 ");
						handler.AppendFormatted(fileName);
						handler.AppendLiteral(" (lang=");
						handler.AppendFormatted(fileName2);
						handler.AppendLiteral(")...");
						stringBuilder9.AppendLine(ref handler);
						string path = Path.Combine(item, "_yabber-bnd4.xml");
						if ((fileName2 == "zhoCN" || fileName2 == "zhocn" || fileName2 == text3 || fileName2 == targetLang) && File.Exists(path))
						{
							string text7 = File.ReadAllText(path, Encoding.UTF8);
							if (text7.Contains("engUS"))
							{
								text7 = text7.Replace("engUS", fileName2);
								File.WriteAllText(path, text7, Encoding.UTF8);
								stringBuilder.AppendLine("  [UPDATE] _yabber-bnd4.xml");
							}
						}
						if (fileName2 != "engUS")
						{
							string text8 = Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS");
							string text9 = Path.Combine(item, "CL", "data", "Target", "INTERROOT_win64", "msg", fileName2);
							if (Directory.Exists(text8) && !Directory.Exists(text9))
							{
								try
								{
									Directory.Move(text8, text9);
									stringBuilder2 = stringBuilder;
									StringBuilder stringBuilder10 = stringBuilder2;
									handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
									handler.AppendLiteral("  [RENAME] engUS -> ");
									handler.AppendFormatted(fileName2);
									stringBuilder10.AppendLine(ref handler);
								}
								catch
								{
								}
							}
							string text10 = Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", "engUS");
							string text11 = Path.Combine(item, "GR", "data", "INTERROOT_win64", "msg", fileName2);
							if (Directory.Exists(text10) && !Directory.Exists(text11))
							{
								try
								{
									Directory.Move(text10, text11);
									stringBuilder2 = stringBuilder;
									StringBuilder stringBuilder11 = stringBuilder2;
									handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
									handler.AppendLiteral("  [RENAME] engUS -> ");
									handler.AppendFormatted(fileName2);
									stringBuilder11.AppendLine(ref handler);
								}
								catch
								{
								}
							}
						}
						(int, string) tuple2 = RunSingleYabber(yabberExe, item, Path.GetDirectoryName(item) ?? "");
						if (tuple2.Item1 == 0)
						{
							string text12 = fileName.Replace("-msgbnd-dcx", ".msgbnd.dcx");
							if (File.Exists(Path.Combine(workDir, text12)))
							{
								stringBuilder2 = stringBuilder;
								StringBuilder stringBuilder12 = stringBuilder2;
								handler = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder2);
								handler.AppendLiteral("  [OK] 打包成功 → ");
								handler.AppendFormatted(text12);
								stringBuilder12.AppendLine(ref handler);
								num++;
								try
								{
									Directory.Delete(item, recursive: true);
								}
								catch
								{
								}
							}
							else
							{
								stringBuilder2 = stringBuilder;
								StringBuilder stringBuilder13 = stringBuilder2;
								handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
								handler.AppendLiteral("  [WARN] 未找到输出文件 ");
								handler.AppendFormatted(text12);
								stringBuilder13.AppendLine(ref handler);
								num2++;
							}
						}
						else
						{
							stringBuilder2 = stringBuilder;
							StringBuilder stringBuilder14 = stringBuilder2;
							handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
							handler.AppendLiteral("  [FAIL] 打包失败 (exit=");
							handler.AppendFormatted(tuple2.Item1);
							handler.AppendLiteral(")");
							stringBuilder14.AppendLine(ref handler);
							stringBuilder2 = stringBuilder;
							StringBuilder stringBuilder15 = stringBuilder2;
							handler = new StringBuilder.AppendInterpolatedStringHandler(4, 1, stringBuilder2);
							handler.AppendLiteral("    ");
							handler.AppendFormatted(tuple2.Item2);
							stringBuilder15.AppendLine(ref handler);
							num2++;
						}
					}
				}
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder16 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder2);
				handler.AppendLiteral("\n打包完成: 成功 ");
				handler.AppendFormatted(num);
				handler.AppendLiteral(", 失败 ");
				handler.AppendFormatted(num2);
				stringBuilder16.AppendLine(ref handler);
				return (num2 == 0, stringBuilder.ToString());
			});
			TextBox applyResultText = ApplyResultText;
			applyResultText.Text = applyResultText.Text + result.Item2 + "\n";
			ApplyResultText.ScrollToEnd();
			return result;
		}

		private (int exitCode, string output) RunSingleYabber(string yabberExe, string argument, string? workingDir = null)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = yabberExe,
				Arguments = "\"" + argument + "\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			if (!string.IsNullOrEmpty(workingDir))
			{
				processStartInfo.WorkingDirectory = workingDir;
			}
			Process proc = new Process
			{
				StartInfo = processStartInfo
			};
			lock (_yabberLock)
			{
				_runningYabberProcesses.Add(proc);
			}
			try
			{
				StringBuilder output = new StringBuilder();
				proc.Start();
				Task task = Task.WhenAll(Task.Run(delegate
				{
					string value2;
					while ((value2 = proc.StandardOutput.ReadLine()) != null)
					{
						output.AppendLine(value2);
					}
				}), Task.Run(delegate
				{
					string value;
					while ((value = proc.StandardError.ReadLine()) != null)
					{
						StringBuilder stringBuilder = output;
						StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
						handler.AppendLiteral("[ERR] ");
						handler.AppendFormatted(value);
						stringBuilder.AppendLine(ref handler);
					}
				}));
				bool num = proc.WaitForExit(30000);
				task.Wait();
				if (!num)
				{
					try
					{
						proc.Kill();
					}
					catch
					{
					}
					output.AppendLine("[TIMEOUT] Yabber 超时 (30s)");
					return (exitCode: -1, output: output.ToString());
				}
				return (exitCode: proc.ExitCode, output: output.ToString());
			}
			finally
			{
				lock (_yabberLock)
				{
					_runningYabberProcesses.Remove(proc);
				}
				if (proc != null)
				{
					((IDisposable)proc).Dispose();
				}
			}
		}

		private async Task<(int exitCode, string output)> RunYabberAsync(string yabberExe, string argument, string? workingDir = null)
		{
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = yabberExe,
				Arguments = "\"" + argument + "\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			if (!string.IsNullOrEmpty(workingDir))
			{
				psi.WorkingDirectory = workingDir;
			}
			return await Task.Run(delegate
			{
				Process proc = new Process
				{
					StartInfo = psi
				};
				lock (_yabberLock)
				{
					_runningYabberProcesses.Add(proc);
				}
				try
				{
					StringBuilder outputBuilder = new StringBuilder();
					proc.Start();
					Task task = Task.WhenAll(Task.Run(delegate
					{
						string value2;
						while ((value2 = proc.StandardOutput.ReadLine()) != null)
						{
							outputBuilder.AppendLine(value2);
						}
					}), Task.Run(delegate
					{
						string value;
						while ((value = proc.StandardError.ReadLine()) != null)
						{
							StringBuilder stringBuilder = outputBuilder;
							StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder);
							handler.AppendLiteral("[ERR] ");
							handler.AppendFormatted(value);
							stringBuilder.AppendLine(ref handler);
						}
					}));
					bool flag = proc.WaitForExit(60000);
					task.Wait();
					if (!flag)
					{
						try
						{
							proc.Kill();
						}
						catch
						{
						}
						outputBuilder.AppendLine("[TIMEOUT] Yabber 进程超时 (60s)，已强制终止");
					}
					return (flag ? proc.ExitCode : (-1), outputBuilder.ToString());
				}
				finally
				{
					lock (_yabberLock)
					{
						_runningYabberProcesses.Remove(proc);
					}
					proc.Dispose();
				}
			});
		}

		private async Task EnsureReferenceUnpackedAsync(string engDir, string yabberExe)
		{
			string[] files = Directory.GetFiles(engDir, "*.msgbnd.dcx");
			if (files.Length == 0)
			{
				return;
			}
			string[] array = files;
			foreach (string dcx in array)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dcx));
				string dcxFolder = Path.Combine(engDir, fileNameWithoutExtension + "-msgbnd-dcx");
				bool flag = false;
				if (Directory.Exists(dcxFolder))
				{
					string text = new string[2]
					{
						Path.Combine(dcxFolder, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
						Path.Combine(dcxFolder, "GR", "data", "INTERROOT_win64", "msg", "engUS")
					}.FirstOrDefault(Directory.Exists);
					if (text != null)
					{
						int num = Directory.GetFiles(text, "*.fmg").Length;
						int num2 = Directory.GetFiles(text, "*.fmg.xml").Length;
						if (num > 0 && num2 == 0)
						{
							flag = true;
						}
					}
				}
				if (!flag && Directory.Exists(dcxFolder))
				{
					continue;
				}
				if (flag)
				{
					try
					{
						Directory.Delete(dcxFolder, recursive: true);
					}
					catch
					{
					}
				}
				TextBlock scanDetailText = ScanDetailText;
				scanDetailText.Text = scanDetailText.Text + "\n[AUTO] 解包参考文件: " + Path.GetFileName(dcx);
				(int, string) tuple = await RunYabberAsync(yabberExe, dcx);
				if (tuple.Item1 != 0)
				{
					ScanDetailText.Text += $"\n  [WARN] 解包失败: {Path.GetFileName(dcx)} (exit={tuple.Item1})";
					continue;
				}
				string[] obj2 = new string[4]
				{
					Path.Combine(dcxFolder, "CL", "data", "Target", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(dcxFolder, "GR", "data", "INTERROOT_win64", "msg", "engUS"),
					Path.Combine(dcxFolder, "CL", "data", "Target", "INTERROOT_win64", "msg", "zhoCN"),
					Path.Combine(dcxFolder, "GR", "data", "INTERROOT_win64", "msg", "zhoCN")
				};
				string fmgDir = null;
				string[] array2 = obj2;
				foreach (string text2 in array2)
				{
					if (Directory.Exists(text2))
					{
						fmgDir = text2;
						break;
					}
				}
				if (fmgDir == null)
				{
					TextBlock scanDetailText2 = ScanDetailText;
					scanDetailText2.Text = scanDetailText2.Text + "\n  [WARN] 未找到 FMG 目录: " + Path.GetFileName(dcx);
					continue;
				}
				string[] files2 = Directory.GetFiles(fmgDir, "*.fmg");
				string[] array3 = files2;
				foreach (string text3 in array3)
				{
					if (!File.Exists(text3 + ".xml"))
					{
						await RunYabberAsync(yabberExe, text3, fmgDir);
					}
				}
			}
		}

		private async Task ReadStreamAsync(StreamReader reader, StringBuilder output, bool isStderr)
		{
			string line;
			while ((line = await reader.ReadLineAsync()) != null)
			{
				output.AppendLine(isStderr ? ("[ERR] " + line) : line);
				base.Dispatcher.Invoke(delegate
				{
					AppendOutput(isStderr ? ("[stderr] " + line) : line);
				});
			}
		}

		private void AppendOutput(string line)
		{
			TextBox applyResultText = ApplyResultText;
			applyResultText.Text = applyResultText.Text + line + "\n";
			ApplyResultText.ScrollToEnd();
		}

		private void AppendText(string text)
		{
			ApplyResultText.Text += text;
			ApplyResultText.ScrollToEnd();
		}

		private static string? FindFsModBat()
		{
			string[] array = new string[1]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fs_mod_tool.bat"),
			};
			foreach (string text in array)
			{
				if (File.Exists(text))
				{
					return text;
				}
			}
			return null;
		}

		private void NavSponsor_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			CleanupTranslationResources();
			ShowPage("sponsor");
			StatusText.Text = "☕ 赞助支持";
			UpdateNavActive("sponsor");
		}

		private void SettingsBtn_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			CleanupTranslationResources();
			_appSettings = SettingsService.Load();
			LoadSettingsUI();
			ShowPage("settings");
			StatusText.Text = "⚙ 设置 - 配置翻译 API";
			UpdateNavActive("settings");
		}

		private void LoadSettingsUI()
		{
			_loadingSettings = true;
			PopulateProviderCombo();
			ProviderCombo.SelectedIndex = _appSettings.ProviderIndex;
			CustomUrlBox.Text = (string.IsNullOrEmpty(_appSettings.CustomApiUrl) ? "https://" : _appSettings.CustomApiUrl);
			CustomModelBox.Text = _appSettings.CustomModelName;
			CustomApiKeyBox.Text = "";
			bool flag = _appSettings.Provider == "自定义";
			if (ApiKeyPanel != null)
			{
				ApiKeyPanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			}
			if (CustomPanel != null)
			{
				CustomPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (!string.IsNullOrEmpty(_appSettings.OwnApiKey))
			{
				OwnApiKeyBox.Text = "";
			}
			_loadingSettings = false;
		}

		private void GenerateCodeBtn_Click(object sender, RoutedEventArgs e)
		{
		}

		private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (CustomUrlBox == null)
			{
				return;
			}
			bool flag = ProviderCombo.SelectedIndex == 12;
			if (ApiKeyPanel != null && CustomPanel != null)
			{
				if (flag)
				{
					CustomApiKeyBox.Text = OwnApiKeyBox.Text;
				}
				else
				{
					OwnApiKeyBox.Text = CustomApiKeyBox.Text;
				}
				ApiKeyPanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
				CustomPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		private void PopulateProviderCombo()
		{
			ProviderCombo.Items.Clear();
			string currentLang = UILocalizer.CurrentLang;
			foreach (string key in AppSettings.ProviderInfo.Keys)
			{
				ProviderCombo.Items.Add(AppSettings.ProviderDisplayName(key, currentLang));
			}
			ProviderCombo.Items.Add(AppSettings.ProviderDisplayName("自定义", currentLang));
		}

		private void PopulateGameCombo()
		{
			GameCombo.Items.Clear();
			string currentLang = UILocalizer.CurrentLang;
			string[] games = AppSettings.Games;
			foreach (string game in games)
			{
				GameCombo.Items.Add((currentLang == "en") ? AppSettings.GameDisplayNameEn(game) : AppSettings.GameDisplayName(game));
			}
			int num = Array.IndexOf(AppSettings.Games, _appSettings.CurrentGame);
			GameCombo.SelectedIndex = ((num >= 0) ? num : 0);
		}

		private void PopulateLangCombo()
		{
			LangCombo.Items.Clear();
			foreach (KeyValuePair<string, string> language in AppSettings.Languages)
			{
				LangCombo.Items.Add(language.Value);
			}
			int num = AppSettings.Languages.Keys.ToList().IndexOf(_appSettings.TargetLanguage);
			LangCombo.SelectedIndex = ((num >= 0) ? num : 0);
		}

		private void RefreshLocalizedCombos()
		{
			string text = ProviderCombo.SelectedItem as string;
			_ = GameCombo.SelectedItem;
			_ = LangCombo.SelectedItem;
			PopulateProviderCombo();
			PopulateGameCombo();
			PopulateLangCombo();
			if (text == null)
			{
				return;
			}
			for (int i = 0; i < ProviderCombo.Items.Count; i++)
			{
				if (ProviderCombo.Items[i] as string == text)
				{
					ProviderCombo.SelectedIndex = i;
					break;
				}
			}
		}

		private void SaveSettingsBtn_Click(object sender, RoutedEventArgs e)
		{
			string displayName = (ProviderCombo.SelectedItem as string) ?? "";
			_appSettings.Provider = AppSettings.ProviderKeyFromDisplay(displayName);
			int selectedIndex = ProviderCombo.SelectedIndex;
			CustomUrlBox.Visibility = ((selectedIndex != 12) ? Visibility.Collapsed : Visibility.Visible);
			_appSettings.CustomApiUrl = CustomUrlBox.Text;
			_appSettings.CustomModelName = CustomModelBox.Text;
			string text = ((_appSettings.Provider == "自定义") ? CustomApiKeyBox.Text : OwnApiKeyBox.Text);
			if (!string.IsNullOrWhiteSpace(text))
			{
				_appSettings.OwnApiKey = text;
			}
			SettingsService.Save(_appSettings);
			_cachedApiKey = _appSettings.ResolvedApiKey;
			SettingsStatusText.Text = "✅ 设置已保存！";
			StatusText.Text = "⚙ 设置已保存";
		}

		private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton != 0)
			{
				return;
			}
			if (e.ClickCount == 2)
			{
				MaximizeBtn_Click(sender, e);
				return;
			}
			try
			{
				DragMove();
			}
			catch
			{
			}
		}

		private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = WindowState.Minimized;
		}

		private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
		{
			base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
			MaximizeBtn.Content = ((base.WindowState == WindowState.Maximized) ? "❐" : "□");
		}

		private void CloseBtn_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			Close();
		}

		private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
		{
			PrepareCancellation();
		}

		private void UpdateNavActive(string page)
		{
			if (NavHome != null)
			{
				NavHome.Style = (Style)FindResource("NavItem");
			}
			if (NavSettings != null)
			{
				NavSettings.Style = (Style)FindResource("NavItem");
			}
			switch (page)
			{
			case "welcome":
				if (NavHome != null)
				{
					NavHome.Style = (Style)FindResource("NavItemActive");
				}
				break;
			case "settings":
				if (NavSettings != null)
				{
					NavSettings.Style = (Style)FindResource("NavItemActive");
				}
				break;
			case "sponsor":
				break;
		}
	}

		private void NavHome_Click(object sender, RoutedEventArgs e)
		{
			PrepareCancellation();
			ShowPage("welcome");
			StatusText.Text = UILocalizer.Tr("status_ready");
			UpdateNavActive("welcome");
		}

		private void ApplyUILanguage()
		{
			StatusText.Text = UILocalizer.Tr("status_ready");
			BottomStatusText.Text = UILocalizer.Tr("status_ready");
			NavHomeText.Text = UILocalizer.Tr("nav_home");
			NavSettingsText.Text = UILocalizer.Tr("nav_settings");
			NavSponsorText.Text = UILocalizer.Tr("nav_sponsor");
			SettingsLangLabel.Text = UILocalizer.Tr("ui_language_label");
			SettingsApiHintText.Text = UILocalizer.Tr("settings_api_hint");
			SettingsTipText.Text = UILocalizer.Tr("settings_tip_body");
			TipBodyText.Text = UILocalizer.Tr("tip_body");
			SponsorDescText.Document = new FlowDocument(new Paragraph(new Run(string.Join("\n\n", UILocalizer.Tr("sponsor_desc1"), UILocalizer.Tr("sponsor_desc2"), UILocalizer.Tr("sponsor_desc3"), UILocalizer.Tr("sponsor_desc4"), UILocalizer.Tr("sponsor_desc5"), UILocalizer.Tr("sponsor_desc6"), UILocalizer.Tr("sponsor_desc7")))));
			BottomStatusText.Text = UILocalizer.Tr("status_ready");
			if (LangUISwitch.Items.Count >= 2)
			{
				LangUISwitch.Items[0] = UILocalizer.Tr("lang_zh");
				LangUISwitch.Items[1] = UILocalizer.Tr("lang_en");
			}
			ApplyUIStrings(this);
			RefreshLocalizedCombos();
		}

		private static string ChineseForKey(string key)
		{
			if (_keyToChinese == null)
			{
				_keyToChinese = new Dictionary<string, string>();
				foreach (KeyValuePair<string, string> item in _uiTextMap)
				{
					_keyToChinese[item.Value] = item.Key;
				}
			}
			if (!_keyToChinese.TryGetValue(key, out string value))
			{
				return key;
			}
			return value;
		}

		private void ApplyUIStrings(DependencyObject parent)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child is TextBlock textBlock)
				{
					if (!_originalTextCache.ContainsKey(textBlock))
					{
						_originalTextCache[textBlock] = textBlock.Text;
					}
					string text = _originalTextCache[textBlock];
					if (!string.IsNullOrEmpty(text) && _uiTextMap.TryGetValue(text, out string value))
					{
						textBlock.Text = UILocalizer.Tr(value);
					}
				}
				else if (child is Button button)
				{
					if (button.Content is string value2)
					{
						if (!_originalTextCache.ContainsKey(button))
						{
							_originalTextCache[button] = value2;
						}
						string text2 = _originalTextCache[button];
						if (!string.IsNullOrEmpty(text2) && _uiTextMap.TryGetValue(text2, out string value3))
						{
							button.Content = UILocalizer.Tr(value3);
						}
					}
					if (button.ToolTip is string value4)
					{
						if (!_originalTextCache.ContainsKey("tooltip_" + button.Name))
						{
							_originalTextCache["tooltip_" + button.Name] = value4;
						}
						string text3 = _originalTextCache["tooltip_" + button.Name];
						if (!string.IsNullOrEmpty(text3) && _uiTextMap.TryGetValue(text3, out string value5))
						{
							button.ToolTip = UILocalizer.Tr(value5);
						}
					}
				}
				ApplyUIStrings(child);
			}
		}

		private void LangUISwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (LangUISwitch.SelectedIndex >= 0)
			{
				string text = ((LangUISwitch.SelectedIndex == 0) ? "zh" : "en");
				if (!(UILocalizer.CurrentLang == text))
				{
					UILocalizer.CurrentLang = text;
					_appSettings.UILanguage = text;
					SettingsService.Save(_appSettings);
					ApplyUILanguage();
				}
			}
		}

		private void LoadUILanguage()
		{
			string text = _appSettings.UILanguage;
			if (string.IsNullOrEmpty(text))
			{
				text = "zh";
			}
			UILocalizer.CurrentLang = text;
			LangUISwitch.SelectedIndex = ((!(text == "zh")) ? 1 : 0);
			ApplyUILanguage();
		}

		private void NavPlaceholder_Click(object sender, RoutedEventArgs e)
		{
			ShowPage("welcome");
			StatusText.Text = "该功能即将推出";
			UpdateNavActive("welcome");
		}

		static MainWindow()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary["程序运行正常"] = "status_ok";
			dictionary["贴心提示"] = "tip_title";
			dictionary["安全可靠"] = "feature_safe_title";
			dictionary["本地处理"] = "feature_safe_sub";
			dictionary["简单易用"] = "feature_easy_title";
			dictionary["高效精准"] = "feature_accurate_title";
			dictionary["智能识别"] = "feature_accurate_sub";
			dictionary["持续更新"] = "feature_updating_title";
			dictionary["功能迭代"] = "feature_updating_sub";
			dictionary["MOD 翻译"] = "mod_translate_title";
			dictionary["推荐"] = "badge_recommended";
			dictionary["选择 MOD 文件夹"] = "step1_title";
			dictionary["支持 FromSoftware 游戏 MOD"] = "step1_desc";
			dictionary["自动分析文本"] = "step2_title";
			dictionary["智能识别 MOD 文本差异"] = "step2_desc";
			dictionary["AI 自动翻译"] = "step3_title";
			dictionary["支持多语言·高效准确"] = "step3_desc";
			dictionary["自动打包回文件"] = "step4_title";
			dictionary["直接生成 .msgbnd.dcx 文件"] = "step4_desc";
			dictionary["单机 MOD 翻译，请遵守游戏使用条款"] = "warning_terms";
			dictionary["选择 MOD"] = "select_mod_btn";
			dictionary["选择 MOD 的 msg/engus 文件夹"] = "drop_zone_mod";
			dictionary["FromSoftware 游戏"] = "mod_translate_subtitle";
			dictionary["FromSoftware MOD"] = "tool_translate_subtitle";
			dictionary["选择 msg/engUS 文件夹"] = "bstep1_title";
			dictionary["定位游戏语言文件"] = "bstep1_desc";
			dictionary["自动解包对比文本"] = "bstep2_title";
			dictionary["深度对比·精准匹配"] = "bstep2_desc";
			dictionary["翻译修改的条目"] = "bstep3_title";
			dictionary["支持批量编辑翻译"] = "bstep3_desc";
			dictionary["打包回 .msgbnd.dcx"] = "bstep4_title";
			dictionary["安全打包·完美兼容"] = "bstep4_desc";
			dictionary["选择你需要翻译的游戏"] = "select_game_label";
			dictionary["目标语言"] = "target_lang_label";
			dictionary["MOD 文件可能较大，处理需耐心等待"] = "warning_mod_size";
			dictionary["将 msg/engUS 文件夹拖拽到此处"] = "drop_zone_tool";
			dictionary["© 2026 FSMODTSBOX  |  让游戏无语言障碍  "] = "footer_text";
			dictionary["← 返回"] = "btn_back";
			dictionary["← 返回首页"] = "btn_back_home";
			dictionary["一键翻译"] = "editor_translate";
			dictionary["写入翻译"] = "editor_apply";
			dictionary["保存"] = "editor_save";
			dictionary["选择文件夹"] = "file_select_folder";
			dictionary["开始扫描"] = "file_start_scan";
			dictionary["全选"] = "file_select_all";
			dictionary["取消全选"] = "file_deselect_all";
			dictionary["翻译选中"] = "file_translate_selected";
			dictionary["打包"] = "file_pack";
			dictionary["保存设置"] = "settings_save";
			dictionary["欢迎加入 FSMODTSBOX 社区"] = "community_welcome";
			dictionary["QQ 交流群：扫描上方二维码加入"] = "community_qq_tip";
			dictionary["扫描上方二维码赞助支持"] = "sponsor_qr_tip";
			dictionary["翻译服务"] = "settings_title";
			dictionary["选择翻译引擎"] = "settings_engine_hint";
			dictionary["\ud83d\udd11 API 密钥配置"] = "settings_api_title";
			dictionary["API 密钥"] = "settings_api_key";
			dictionary["配置你的翻译 API 密钥（留空则使用内置 Agnes 2.0 Flash）"] = "settings_api_hint";
			dictionary["自定义 URL"] = "settings_custom_url";
			dictionary["模型名称"] = "settings_custom_model";
			dictionary["自定义 API 密钥"] = "settings_custom_key";
			dictionary["\ud83d\udca1 提示"] = "settings_tip_title";
			dictionary["保存设置"] = "settings_save";
			dictionary["当前版本: v1.0.0"] = "status_version";
		dictionary["正在翻译…"] = "applying_title";
			dictionary["最小化"] = "tooltip_minimize";
			dictionary["最大化"] = "tooltip_maximize";
			dictionary["关闭"] = "tooltip_close";
			_uiTextMap = dictionary;
		}
	}
}