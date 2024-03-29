﻿using STranslate.Util;
using STranslate.ViewModels.Preference;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace STranslate.Views.Preference
{
    public partial class OCRPage : UserControl
    {
        public OCRPage()
        {
            InitializeComponent();
            DataContext = Singleton<OCRViewModel>.Instance;
        }

        public static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T t)
                {
                    return t;
                }
                current = VisualTreeHelper.GetParent(current);
            } while (current != null);
            return null;
        }
    }
}
