﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using STranslate.Model;
using STranslate.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace STranslate.ViewModels.Preference.Services
{
    public partial class TranslatorCaiyun : ObservableObject, ITranslator
    {
        #region Constructor

        public TranslatorCaiyun()
            : this(Guid.NewGuid(), "http://api.interpreter.caiyunai.com/v1/translator", "彩云小译") { }

        public TranslatorCaiyun(
            Guid guid,
            string url,
            string name = "",
            IconType icon = IconType.Caiyun,
            string appID = "",
            string appKey = "",
            bool isEnabled = true,
            ServiceType type = ServiceType.CaiyunService
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
        private IconType _icon = IconType.Baidu;

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
        public BindingList<UserDefinePrompt> UserDefinePrompts { get; set; } = [];

        [JsonIgnore]
        [ObservableProperty]
        private bool _autoExpander = true;

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
        private bool _idHide = true;

        [JsonIgnore]
        [ObservableProperty]
        [property: JsonIgnore]
        private bool _keyHide = true;

        private void ShowEncryptInfo(string? obj)
        {
            if (obj == null)
                return;

            if (obj.Equals(nameof(AppID)))
            {
                IdHide = !IdHide;
            }
            else if (obj.Equals(nameof(AppKey)))
            {
                KeyHide = !KeyHide;
            }
        }

        private RelayCommand<string>? showEncryptInfoCommand;

        [JsonIgnore]
        public IRelayCommand<string> ShowEncryptInfoCommand => showEncryptInfoCommand ??= new RelayCommand<string>(new Action<string?>(ShowEncryptInfo));

        #endregion Show/Hide Encrypt Info

        #endregion Properties

        #region Interface Implementation

        public async Task<TranslationResult> TranslateAsync(object request, CancellationToken token)
        {
            if (request is RequestModel req)
            {
                //检查语种
                var convSource = LangConverter(req.SourceLang) ?? throw new Exception($"该服务不支持{req.SourceLang.GetDescription()}");
                var convTarget = LangConverter(req.TargetLang) ?? throw new Exception($"该服务不支持{req.TargetLang.GetDescription()}");

                var body = new
                {
                    source = req.Text.Split(Environment.NewLine),
                    trans_type = $"{convSource}2{convTarget}",
                    request_id = "demo",
                    detect = true
                };

                var headers = new Dictionary<string, string> { { "X-Authorization", $"token {AppKey}" }, };

                string resp = await HttpUtil.PostAsync(Url, JsonConvert.SerializeObject(body), null, headers, token);
                if (string.IsNullOrEmpty(resp))
                    throw new Exception("请求结果为空");

                // 解析JSON数据
                var parsedData = JsonConvert.DeserializeObject<JObject>(resp ?? throw new Exception("请求结果为空")) ?? throw new Exception($"反序列化失败: {resp}");

                // 提取content的值
                JArray arrayData = parsedData["target"] as JArray ?? throw new Exception("未获取到结果");

                string data = string.Join(Environment.NewLine, arrayData.Select(item => item.ToString()));

                return string.IsNullOrEmpty(data) ? TranslationResult.Fail("获取结果为空") : TranslationResult.Success(data);
            }

            throw new Exception($"请求数据出错: {request}");
        }

        public Task TranslateAsync(object request, Action<string> OnDataReceived, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public ITranslator Clone()
        {
            return new TranslatorCaiyun
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
                AutoExpander = this.AutoExpander,
                Icons = this.Icons,
                IdHide = this.IdHide,
                KeyHide = this.KeyHide,
            };
        }

        /// <summary>
        /// https://docs.caiyunapp.com/blog/2018/09/03/lingocloud-api/#%E6%94%AF%E6%8C%81%E7%9A%84%E8%AF%AD%E8%A8%80
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        public string? LangConverter(LangEnum lang)
        {
            return lang switch
            {
                LangEnum.auto => "auto",
                LangEnum.zh_cn => "zh",
                LangEnum.zh_tw => "zh",
                LangEnum.yue => "zh",
                LangEnum.en => "en",
                LangEnum.ja => "ja",

                LangEnum.ko => null,
                LangEnum.fr => null,
                LangEnum.es => null,
                LangEnum.ru => null,
                LangEnum.de => null,
                LangEnum.it => null,
                LangEnum.tr => null,
                LangEnum.pt_pt => null,
                LangEnum.pt_br => null,
                LangEnum.vi => null,
                LangEnum.id => null,
                LangEnum.th => null,
                LangEnum.ms => null,
                LangEnum.ar => null,
                LangEnum.hi => null,
                LangEnum.km => null,
                LangEnum.nb_no => null,
                LangEnum.nn_no => null,
                LangEnum.fa => null,
                LangEnum.sv => null,
                LangEnum.pl => null,
                LangEnum.nl => null,
                LangEnum.uk => null,
                _ => "auto"
            };
        }

        #endregion Interface Implementation
    }
}