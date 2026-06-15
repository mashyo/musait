// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Musait.Models;
using Musait.Services;
using Musait.Utils;

namespace Musait.UI.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isDarkTheme;
        private int _resolutionIndex;
        private bool _forceWhiteBackground;
        private bool _changeDisplayMode;
        private bool _autoArchiveCaptures;
        private bool _autoSendCaptures;
        private bool _showTrendsInComposer;
        private bool _showTrendsInActionBar;

        public ObservableCollection<string> Resolutions { get; } = new()
        {
            "1024 x 768 (Standard)",
            "1024 x 1024 (Square)",
            "1920 x 1080 (HD)",
            "2560 x 1440 (2K)",
            "3840 x 2160 (4K)",
            "4096 x 2160 (Cinema 4K)"
        };

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set { _isDarkTheme = value; OnPropertyChanged(); }
        }

        public int ResolutionIndex
        {
            get => _resolutionIndex;
            set { _resolutionIndex = value; OnPropertyChanged(); }
        }

        public bool ForceWhiteBackground
        {
            get => _forceWhiteBackground;
            set { _forceWhiteBackground = value; OnPropertyChanged(); }
        }

        public bool ChangeDisplayMode
        {
            get => _changeDisplayMode;
            set { _changeDisplayMode = value; OnPropertyChanged(); }
        }

        public bool AutoArchiveCaptures
        {
            get => _autoArchiveCaptures;
            set { _autoArchiveCaptures = value; OnPropertyChanged(); }
        }

        public bool AutoSendCaptures
        {
            get => _autoSendCaptures;
            set { _autoSendCaptures = value; OnPropertyChanged(); }
        }

        public bool ShowTrendsInComposer
        {
            get => _showTrendsInComposer;
            set { _showTrendsInComposer = value; OnPropertyChanged(); }
        }

        public bool ShowTrendsInActionBar
        {
            get => _showTrendsInActionBar;
            set { _showTrendsInActionBar = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }

        public SettingsViewModel()
        {
            LoadSettings();
            SaveCommand = new RelayCommand(SaveSettings);
        }

        private void LoadSettings()
        {
            SettingsManager.Load();
            var settings = SettingsManager.Settings;
            IsDarkTheme = settings.IsDarkTheme;
            ResolutionIndex = settings.CaptureResolutionIndex;
            ForceWhiteBackground = settings.ForceWhiteBackground;
            ChangeDisplayMode = settings.ChangeDisplayMode;
            AutoArchiveCaptures = settings.AutoArchiveCaptures;
            AutoSendCaptures = settings.AutoSendCaptures;
            ShowTrendsInComposer = settings.ShowTrendsInComposer;
            ShowTrendsInActionBar = settings.ShowTrendsInActionBar;
        }

        private void SaveSettings()
        {
            var settings = SettingsManager.Settings;
            settings.IsDarkTheme = IsDarkTheme;
            settings.CaptureResolutionIndex = ResolutionIndex;
            settings.ForceWhiteBackground = ForceWhiteBackground;
            settings.ChangeDisplayMode = ChangeDisplayMode;
            settings.AutoArchiveCaptures = AutoArchiveCaptures;
            settings.AutoSendCaptures = AutoSendCaptures;
            settings.ShowTrendsInComposer = ShowTrendsInComposer;
            settings.ShowTrendsInActionBar = ShowTrendsInActionBar;
            
            SettingsManager.Save();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

