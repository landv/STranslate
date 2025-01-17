﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
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
    public partial class TranslatorBaidu : ObservableObject, ITranslator
    {
        #region Constructor

        public TranslatorBaidu()
            : this(Guid.NewGuid(), "https://fanyi-api.baidu.com/api/trans/vip/translate", "百度翻译") { }

        public TranslatorBaidu(
            Guid guid,
            string url,
            string name = "",
            IconType icon = IconType.Baidu,
            string appID = "",
            string appKey = "",
            bool isEnabled = true,
            ServiceType type = ServiceType.BaiduService
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
            if (obj == null) return;

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

                string salt = new Random().Next(100000).ToString();
                string sign = StringUtil.EncryptString(AppID + req.Text + salt + AppKey);

                var queryparams = new Dictionary<string, string>
                {
                    { "q", req.Text },
                    { "from", convSource},
                    { "to", convTarget },
                    { "appid", AppID },
                    { "salt", salt },
                    { "sign", sign }
                };

                string resp = await HttpUtil.GetAsync(Url, queryparams, token);
                if (string.IsNullOrEmpty(resp))
                    throw new Exception("请求结果为空");

                var ret = JsonConvert.DeserializeObject<ResponseBaidu>(resp ?? "");

                //如果出错就将整个返回信息写入取值处
                if (ret is null || string.IsNullOrEmpty(ret.TransResult?.FirstOrDefault()?.Dst))
                {
                    throw new Exception(resp);
                }

                var transResults = ret.TransResult ?? [];
                var data = transResults.Length == 0 ? throw new Exception("请求结果为空")
                        : string.Join(Environment.NewLine, transResults.Where(trans => !string.IsNullOrEmpty(trans.Dst)).Select(trans => trans.Dst));

                return TranslationResult.Success(data);
            }

            throw new Exception($"请求数据出错: {request}");
        }

        public Task TranslateAsync(object request, Action<string> OnDataReceived, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public ITranslator Clone()
        {
            return new TranslatorBaidu
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
        /// https://fanyi-api.baidu.com/product/113
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        public string? LangConverter(LangEnum lang)
        {
            return lang switch
            {
                LangEnum.auto => "auto",
                LangEnum.zh_cn => "zh",
                LangEnum.zh_tw => "cht",
                LangEnum.yue => "yue",
                LangEnum.en => "en",
                LangEnum.ja => "jp",
                LangEnum.ko => "kor",
                LangEnum.fr => "fra",
                LangEnum.es => "spa",
                LangEnum.ru => "ru",
                LangEnum.de => "de",
                LangEnum.it => "it",
                LangEnum.tr => "tr",
                LangEnum.pt_pt => "pt",
                LangEnum.pt_br => "pot",
                LangEnum.vi => "vie",
                LangEnum.id => "id",
                LangEnum.th => "th",
                LangEnum.ms => "may",
                LangEnum.ar => "ar",
                LangEnum.hi => "hi",
                LangEnum.mn_cy => null,
                LangEnum.mn_mo => null,
                LangEnum.km => "hkm",
                LangEnum.nb_no => "nob",
                LangEnum.nn_no => "nno",
                LangEnum.fa => "per",
                LangEnum.sv => "swe",
                LangEnum.pl => "pl",
                LangEnum.nl => "nl",
                LangEnum.uk => "ukr",
                _ => "auto"
            };
        }

        #endregion Interface Implementation
    }
}