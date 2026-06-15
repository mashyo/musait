// Copyright (c) 2026 Mashyo. All Rights Reserved.

using System;
using System.IO;
using Newtonsoft.Json;
using Musait.Models;

namespace Musait.Services
{
    public static class SettingsManager
    {
        private static AppSettings _settings = new AppSettings();

        public static AppSettings Settings => _settings;

        static SettingsManager()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(Constants.SettingsFilePath))
                {
                    string json = File.ReadAllText(Constants.SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        _settings = settings;
                        return;
                    }
                }
            }
            catch
            {
                // Fallback to defaults
            }

            _settings = new AppSettings();
        }

        public static void Save()
        {
            try
            {
                string? directoryName = Path.GetDirectoryName(Constants.SettingsFilePath);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(Constants.SettingsFilePath, json);
            }
            catch
            {
                // Fail silently
            }
        }
    }
}

