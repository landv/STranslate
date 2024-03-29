﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using STranslate.Helper;
using STranslate.Log;
using STranslate.Model;
using STranslate.Util;
using STranslate.ViewModels.Preference.OCR;

namespace STranslate.ViewModels.Preference
{
    public partial class OCRViewModel : ObservableObject
    {
        public async Task SpeakTextAsync(byte[] bytes, CancellationToken token)
        {
            if (ActivedOCR is null)
            {
                ToastHelper.Show("未启用OCR服务", WindowType.Main);
                return;
            }

            await ActivedOCR.ExecuteAsync(bytes, token);
        }

        [ObservableProperty]
        private IOCR? _activedOCR;

        public OCRViewModel()
        {
            //添加默认支持OCR
            //TODO: 新OCR服务需要适配
            OcrServices.Add(new PaddleOCR());
            OcrServices.Add(new TencentOCR());

            CurOCRServiceList.OnActiveOCRChanged += CurOCRServiceList_OnActiveOCRChanged;

            ResetView();
        }

        private void CurOCRServiceList_OnActiveOCRChanged(IOCR obj)
        {
            if (obj.IsEnabled)
            {
                ActivedOCR = obj;
                LogService.Logger.Debug($"Change Actived OCR Service: {obj.Identify} {obj.Name}");
            }
        }

        /// <summary>
        /// 重置选中项
        /// </summary>
        /// <param name="type"></param>
        private void ResetView(ActionType type = ActionType.Initialize)
        {
            OcrCounter = CurOCRServiceList.Count;

            //当全部删除时则清空view绑定属性
            if (OcrCounter < 1)
            {
                SelectedIndex = 0;
                OcrServiceContent = null;
                return;
            }

            switch (type)
            {
                case ActionType.Delete:
                {
                    //不允许小于0
                    SelectedIndex = Math.Max(tmpIndex - 1, 0);
                    TogglePage(CurOCRServiceList[SelectedIndex]);
                    break;
                }
                case ActionType.Add:
                {
                    //选中最后一项
                    SelectedIndex = OcrCounter - 1;
                    TogglePage(CurOCRServiceList[SelectedIndex]);
                    break;
                }
                default:
                {
                    //初始化默认执行选中第一条
                    SelectedIndex = 0;
                    TogglePage(CurOCRServiceList.First());
                    break;
                }
            }
        }

        private int tmpIndex;

        [RelayCommand]
        private void TogglePage(IOCR ocr)
        {
            if (ocr != null)
            {
                if (SelectedIndex != -1)
                    tmpIndex = SelectedIndex;

                string head = "STranslate.Views.Preference.OCR.";
                //TODO: 新TTS服务需要适配
                var name = ocr.Type switch
                {
                    OCRType.PaddleOCR => string.Format("{0}PaddleOCRPage", head),
                    OCRType.TencentOCR => string.Format("{0}TencentOCRPage", head),
                    _ => string.Format("{0}PaddleOCRPage", head)
                };

                NavigationPage(name, ocr);
            }
        }

        [RelayCommand]
        private void Popup(Popup control) => control.IsOpen = true;

        [RelayCommand]
        private void Add(List<object> list)
        {
            if (list?.Count == 2)
            {
                var ocr = list.First();

                //TODO: 新OCR服务需要适配
                CurOCRServiceList.Add(
                    ocr switch
                    {
                        PaddleOCR paddleocr => paddleocr.Clone(),
                        TencentOCR tencentocr => tencentocr.Clone(),
                        _ => throw new InvalidOperationException($"Unsupported ocr type: {ocr.GetType().Name}")
                    }
                );

                (list.Last() as Popup)!.IsOpen = false;

                ResetView(ActionType.Add);
            }
        }

        [RelayCommand]
        private void Delete(IOCR ocr)
        {
            if (ocr != null)
            {
                CurOCRServiceList.Remove(ocr);

                ResetView(ActionType.Delete);

                ToastHelper.Show("删除成功", WindowType.Preference);
            }
        }

        [RelayCommand]
        private void Save()
        {
            if (!Singleton<ConfigHelper>.Instance.WriteConfig(CurOCRServiceList))
            {
                LogService.Logger.Debug($"保存OCR失败，{JsonConvert.SerializeObject(CurOCRServiceList)}");

                ToastHelper.Show("保存失败", WindowType.Preference);
            }
            ToastHelper.Show("保存成功", WindowType.Preference);
        }

        [RelayCommand]
        private void Reset()
        {
            CurOCRServiceList = Singleton<ConfigHelper>.Instance.ResetConfig.OCRList ?? [];
            ResetView(ActionType.Initialize);
            ToastHelper.Show("重置配置", WindowType.Preference);
        }

        /// <summary>
        /// 导航页面
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ocr"></param>
        public void NavigationPage(string name, IOCR ocr)
        {
            UIElement? content = null;

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("param name is null or empty", nameof(name));

                ArgumentNullException.ThrowIfNull(ocr);

                Type? type = Type.GetType(name) ?? throw new Exception($"{nameof(NavigationPage)} get {name} exception");

                //读取缓存是否存在，存在则从缓存中获取View实例并通过UpdateVM刷新ViewModel
                if (ContentCache.ContainsKey(type))
                {
                    content = ContentCache[type];
                    if (content is UserControl uc)
                    {
                        var method = type.GetMethod("UpdateVM");
                        method?.Invoke(uc, new[] { ocr });
                    }
                }
                else //不存在则创建并通过构造函数传递ViewModel
                {
                    content = (UIElement?)Activator.CreateInstance(type, ocr);
                    ContentCache.Add(type, content);
                }

                OcrServiceContent = content;
            }
            catch (Exception ex)
            {
                LogService.Logger.Error("OCR导航出错", ex);
            }
        }

        /// <summary>
        /// 导航 UI 缓存
        /// </summary>
        private readonly Dictionary<Type, UIElement?> ContentCache = [];

        [ObservableProperty]
        private int _selectedIndex = 0;

        [ObservableProperty]
        private UIElement? _ocrServiceContent;

        [ObservableProperty]
        private int _ocrCounter;

        [ObservableProperty]
        private BindingList<IOCR> _ocrServices = [];

        [ObservableProperty]
        private OCRCollection<IOCR> _curOCRServiceList = Singleton<ConfigHelper>.Instance.CurrentConfig?.OCRList ?? [];
    }
}
