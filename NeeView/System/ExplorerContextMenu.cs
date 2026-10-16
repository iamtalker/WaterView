using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using NeeView.Properties;
using NeeView.Windows.Property;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace NeeView
{
    /// <summary>
    /// Adds an "Open in WaterView" entry to the Explorer context menu for archives, images and folders.
    /// Per-user setting (HKCU). The accelerator key shown in the menu is configurable (default "4").
    /// </summary>
    public class ExplorerContextMenu : ObservableObject
    {
        static ExplorerContextMenu() => Current = new ExplorerContextMenu();
        public static ExplorerContextMenu Current { get; }

        private const string _keyName = "OpenInNeeView";
        private const string _legacyKeyFile = @"*\shell\" + _keyName;
        private const string _keyDirectory = @"Directory\shell\" + _keyName;
        private const string _keyDirectoryBackground = @"Directory\Background\shell\" + _keyName;
        private const string _defaultAccessKey = "4";

        // archives and images that get the menu entry
        private static readonly string[] _extensions =
        {
            ".zip", ".rar", ".7z", ".cbz", ".cbr", ".cb7", ".lzh", ".pdf",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff", ".avif", ".jxl", ".heic",
        };

        private readonly RegistryKey _root;
        private bool _isEnabled;
        private string _accessKey;


        private ExplorerContextMenu()
        {
            _root = Registry.CurrentUser.OpenSubKey(@"Software\Classes", true) ?? throw new InvalidOperationException("Cannot get registry");

            _isEnabled = Exists();
            _accessKey = ReadAccessKeyFromRegistry() ?? _defaultAccessKey;
        }

        /// <summary>
        /// Explorer ContextMenu 登録状態。セーブデータには保存されない。
        /// </summary>
        [PropertyMember]
        [DefaultValue(false)]
        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }
            set
            {
                if (value != _isEnabled)
                {
                    if (value)
                    {
                        Create();
                    }
                    else
                    {
                        Delete();
                    }
                    _isEnabled = Exists();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Accelerator key of the menu label, e.g. "4" => "물뷰로 열기(&amp;4)". Stored in the registry label.
        /// </summary>
        [PropertyMember]
        [DefaultValue(_defaultAccessKey)]
        public string AccessKey
        {
            get { return _accessKey; }
            set
            {
                var key = (value ?? "").Trim();
                if (key.Length != 1 || !char.IsLetterOrDigit(key[0])) return;
                if (_accessKey == key) return;

                _accessKey = key;
                OnPropertyChanged();
                Update();
            }
        }


        private bool Exists()
        {
            return IsExistSubKey(GetFileKey(_extensions[0]));
        }

        private static string GetFileKey(string extension)
        {
            return @"SystemFileAssociations\" + extension + @"\shell\" + _keyName;
        }

        public void Create()
        {
            var commandName = "\"" + Environment.AssemblyLocation + "\"";
            var label = GetMenuLabel();
            var icon = commandName + ",0";
            var command = commandName + " \"%1\"";
            var commandTypeV = commandName + " \"%V\"";

            foreach (var extension in _extensions)
            {
                CreateSubKey(GetFileKey(extension), label, icon, command);
            }
            CreateSubKey(_keyDirectory, label, icon, command);
            CreateSubKey(_keyDirectoryBackground, label, icon, commandTypeV);

            // older versions registered the entry for every file type
            DeleteSubKey(_legacyKeyFile);
        }

        public void Delete()
        {
            foreach (var extension in _extensions)
            {
                DeleteSubKey(GetFileKey(extension));
            }
            DeleteSubKey(_keyDirectory);
            DeleteSubKey(_keyDirectoryBackground);
            DeleteSubKey(_legacyKeyFile);
        }

        public void Update()
        {
            if (!IsEnabled) return;

            var oldLabel = GetSubKey(GetFileKey(_extensions[0]), null);
            var newLabel = GetMenuLabel();

            if (oldLabel != newLabel)
            {
                foreach (var extension in _extensions)
                {
                    UpdateSubKey(GetFileKey(extension), newLabel);
                }
                UpdateSubKey(_keyDirectory, newLabel);
                UpdateSubKey(_keyDirectoryBackground, newLabel);
            }
        }

        private string GetMenuLabel()
        {
            var text = TextResources.GetString("ExplorerContextMenu.OpenInNeeView", false) ?? "WaterView";

            // drop any accelerator mark in the resource text, then append the configured one
            text = Regex.Replace(text, @"\(_.\)", "").Replace("_", "").Trim();
            return $"{text}(&{_accessKey})";
        }

        private string? ReadAccessKeyFromRegistry()
        {
            var label = GetSubKey(GetFileKey(_extensions[0]), null);
            if (label is null) return null;

            var match = Regex.Match(label, @"\(&(.)\)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private bool IsExistSubKey(string keyName)
        {
            try
            {
                using var key = _root.OpenSubKey(keyName);
                return key != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        private void CreateSubKey(string key, string label, string icon, string command)
        {
            try
            {
                using (var reg = _root.CreateSubKey(key, true))
                {
                    reg.SetValue(null, label);
                    reg.SetValue("Icon", icon);
                }
                using (var reg = _root.CreateSubKey(key + @"\command"))
                {
                    reg.SetValue(null, command);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void UpdateSubKey(string key, string label)
        {
            try
            {
                using (var reg = _root.CreateSubKey(key, true))
                {
                    reg.SetValue(null, label);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void DeleteSubKey(string key)
        {
            try
            {
                if (IsExistSubKey(key))
                {
                    _root.DeleteSubKeyTree(key);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private string? GetSubKey(string key, string? name = null)
        {
            try
            {
                using (var reg = _root.OpenSubKey(key))
                {
                    if (reg is null) return null;
                    return (string?)reg.GetValue(name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
