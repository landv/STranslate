﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using STranslate.Model;
using STranslate.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace STranslate.ViewModels.Preference.Services
{
    public partial class TranslatorGemini : ObservableObject, ITranslator
    {
        #region Constructor

        public TranslatorGemini()
            : this(Guid.NewGuid(), "https://generativelanguage.googleapis.com", "Gemini") { }

        public TranslatorGemini(
            Guid guid,
            string url,
            string name = "",
            IconType icon = IconType.Gemini,
            string appID = "",
            string appKey = "",
            bool isEnabled = true,
            ServiceType type = ServiceType.GeminiService
        )
        {
            Identify = guid;
            Url = url;
            Name = name;
            Icon = icon;
            AppID = appID;
            AppKey = appKey;
            IsEnabled = isEnabled;
            Type = type;
        }

        #endregion Constructor

        #region Properties

        [ObservableProperty]
        private Guid _identify = Guid.Empty;

        [JsonIgnore]
        [ObservableProperty]
        private ServiceType _type = 0;

        [JsonIgnore]
        [ObservableProperty]
        public bool _isEnabled = true;

        [JsonIgnore]
        [ObservableProperty]
        private string _name = string.Empty;

        [JsonIgnore]
        [ObservableProperty]
        private IconType _icon = IconType.Gemini;

        [JsonIgnore]
        [ObservableProperty]
        [property: DefaultValue("")]
        [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string _url = string.Empty;

        [JsonIgnore]
        [ObservableProperty]
        [property: DefaultValue("")]
        [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string _appID = string.Empty;

        [JsonIgnore]
        [ObservableProperty]
        [property: DefaultValue("")]
        [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string _appKey = string.Empty;

        [JsonIgnore]
        [ObservableProperty]
        private bool _autoExpander = true;

        [JsonIgnore]
        [ObservableProperty]
        public int _timeOut = 10;

        [JsonIgnore]
        [ObservableProperty]
        [property: JsonIgnore]
        public TranslationResult _data = TranslationResult.Reset;

        [JsonIgnore]
        public Dictionary<IconType, string> Icons { get; private set; } = ConstStr.ICONDICT;

        #region Show/Hide Encrypt Info

        [JsonIgnore]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool _keyHide = true;

        private void ShowEncryptInfo() => KeyHide = !KeyHide;

        private RelayCommand? showEncryptInfoCommand;

        [JsonIgnore]
        public IRelayCommand ShowEncryptInfoCommand => showEncryptInfoCommand ??= new RelayCommand(new Action(ShowEncryptInfo));

        #endregion Show/Hide Encrypt Info

        #region Prompt

        [JsonIgnore]
        [ObservableProperty]
        private BindingList<UserDefinePrompt> _userDefinePrompts =
        [
            new UserDefinePrompt("翻译", [new Prompt("user", "You are a professional translation engine, please translate the text into a colloquial, professional, elegant and fluent content, without the style of machine translation. You must only translate the text content, never interpret it."), new Prompt("model", "Ok, I will only translate the text content, never interpret it"), new Prompt("user", "Translate the following text from en to zh: hello world"), new Prompt("model", "你好，世界"), new Prompt("user", "Translate the following text from $source to $target: $content")], true),
            new UserDefinePrompt("润色", [new Prompt("user", "You are a text embellisher, you can only embellish the text, never interpret it."), new Prompt("model", "Ok, I will only embellish the text, never interpret it."), new Prompt("user", "Embellish the following text in $source: $content")]),
            new UserDefinePrompt("总结", [new Prompt("user", "You are a text summarizer, you can only summarize the text, never interpret it."), new Prompt("model", "Ok, I will only summarize the text, never interpret it."), new Prompt("user", "Summarize the following text in $source: $content")]),
        ];

        [RelayCommand]
        [property: JsonIgnore]
        private void SelectedPrompt(List<object> obj)
        {
            var userDefinePrompt = (UserDefinePrompt)obj.First();
            foreach (var item in UserDefinePrompts)
            {
                item.Enabled = false;
            }
            userDefinePrompt.Enabled = true;

            if (obj.Count == 2) Singleton<ServiceViewModel>.Instance.SaveCommand.Execute(null);
        }

        [RelayCommand]
        [property: JsonIgnore]
        private void UpdatePrompt(UserDefinePrompt userDefinePrompt)
        {
            var dialog = new Views.Preference.Service.PromptDialog(ServiceType.GeminiService, (UserDefinePrompt)userDefinePrompt.Clone());
            if (dialog.ShowDialog() ?? false)
            {
                var tmp = ((PromptViewModel)dialog.DataContext).UserDefinePrompt;
                userDefinePrompt.Name = tmp.Name;
                userDefinePrompt.Prompts = tmp.Prompts;
            }
        }

        [RelayCommand]
        [property: JsonIgnore]
        private void DeletePrompt(UserDefinePrompt userDefinePrompt)
        {
            UserDefinePrompts.Remove(userDefinePrompt);
        }

        [RelayCommand]
        [property: JsonIgnore]
        private void AddPrompt()
        {
            var userDefinePrompt = new UserDefinePrompt("Undefined", []);
            var dialog = new Views.Preference.Service.PromptDialog(ServiceType.GeminiService, userDefinePrompt);
            if (dialog.ShowDialog() ?? false)
            {
                var tmp = ((PromptViewModel)dialog.DataContext).UserDefinePrompt;
                userDefinePrompt.Name = tmp.Name;
                userDefinePrompt.Prompts = tmp.Prompts;
                UserDefinePrompts.Add(userDefinePrompt);
            }
        }

        #endregion Prompt

        #endregion Properties

        #region Interface Implementation

        public async Task TranslateAsync(object request, Action<string> OnDataReceived, CancellationToken token)
        {
            if (string.IsNullOrEmpty(Url) || string.IsNullOrEmpty(AppKey))
                throw new Exception("请先完善配置");

            if (request is RequestModel req)
            {
                //检查语种
                var source = LangConverter(req.SourceLang) ?? throw new Exception($"该服务不支持{req.SourceLang.GetDescription()}");
                var target = LangConverter(req.TargetLang) ?? throw new Exception($"该服务不支持{req.TargetLang.GetDescription()}");
                var content = req.Text;

                UriBuilder uriBuilder = new(Url);

                if (!uriBuilder.Path.EndsWith("/v1beta/models/gemini-pro:streamGenerateContent"))
                {
                    uriBuilder.Path = "/v1beta/models/gemini-pro:streamGenerateContent";
                }

                uriBuilder.Query = $"key={AppKey}";

                // 替换Prompt关键字
                var a_messages = (UserDefinePrompts.FirstOrDefault(x => x.Enabled)?.Prompts ?? throw new Exception("请先完善Propmpt配置")).Clone();
                a_messages.ToList().ForEach(item => item.Content = item.Content.Replace("$source", source).Replace("$target", target).Replace("$content", content));

                // 构建请求数据
                var reqData = new
                {
                    contents = a_messages.Select(e => new
                    {
                        role = e.Role,
                        parts = new[]
                        {
                            new { text = e.Content }
                        }
                    })
                };

                // 为了流式输出与MVVM还是放这里吧
                var jsonData = JsonConvert.SerializeObject(reqData);

                await HttpUtil.PostAsync(
                    uriBuilder.Uri,
                    jsonData,
                    null,
                    msg =>
                    {
                        // 使用正则表达式提取目标字符串
                        string pattern = "(?<=\"text\": \")[^\"]+(?=\")";

                        var match = Regex.Match(msg, pattern);

                        if (match.Success)
                        {
                            OnDataReceived?.Invoke(match.Value.Replace("\\n", "\n"));
                        }
                    },
                    token,
                    TimeOut
                );

                return;
            }

            throw new Exception($"请求数据出错: {request}");
        }

        public Task<TranslationResult> TranslateAsync(object request, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public ITranslator Clone()
        {
            return new TranslatorGemini
            {
                Identify = this.Identify,
                Type = this.Type,
                IsEnabled = this.IsEnabled,
                Icon = this.Icon,
                Name = this.Name,
                Url = this.Url,
                Data = TranslationResult.Reset,
                AppID = this.AppID,
                AppKey = this.AppKey,
                UserDefinePrompts = this.UserDefinePrompts,
                AutoExpander = this.AutoExpander,
                Icons = this.Icons,
                KeyHide = this.KeyHide,
            };
        }

        /// <summary>
        /// https://zh.wikipedia.org/wiki/ISO_639-1%E4%BB%A3%E7%A0%81%E5%88%97%E8%A1%A8
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        public string? LangConverter(LangEnum lang)
        {
            return lang switch
            {
                LangEnum.auto => "auto",
                LangEnum.zh_cn => "zh-cn",
                LangEnum.zh_tw => "zh-tw",
                LangEnum.yue => "yue",
                LangEnum.ja => "ja",
                LangEnum.en => "en",
                LangEnum.ko => "ko",
                LangEnum.fr => "fr",
                LangEnum.es => "es",
                LangEnum.ru => "ru",
                LangEnum.de => "de",
                LangEnum.it => "it",
                LangEnum.tr => "tr",
                LangEnum.pt_pt => "pt_pt",
                LangEnum.pt_br => "pt_br",
                LangEnum.vi => "vi",
                LangEnum.id => "id",
                LangEnum.th => "th",
                LangEnum.ms => "ms",
                LangEnum.ar => "ar",
                LangEnum.hi => "hi",
                LangEnum.mn_cy => "mn_cy",
                LangEnum.mn_mo => "mn_mo",
                LangEnum.km => "km",
                LangEnum.nb_no => "nb_no",
                LangEnum.nn_no => "nn_no",
                LangEnum.fa => "fa",
                LangEnum.sv => "sv",
                LangEnum.pl => "pl",
                LangEnum.nl => "nl",
                LangEnum.uk => "uk",
                _ => "auto"
            };
        }

        #endregion Interface Implementation
    }
}