using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.IO;

namespace AffectorLookupPlugin.Windows
{
    public partial class AffectorLookupWindow : FrostyDockableWindow
    {
        private readonly Dictionary<uint, string> idToNameMap = new Dictionary<uint, string>();
        private readonly Dictionary<uint, string> idToReasonMap = new Dictionary<uint, string>();
        private readonly Dictionary<uint, bool> idToIsDupeMap = new Dictionary<uint, bool>();
        public bool cacheReadFailed = false;
        private bool lookupFailed = true;

        public AffectorLookupWindow()
        {
            FrostyTaskWindow.Show("Loading Affector Cache", "Loading Affector Cache...", (task) =>
            {
                task.Update("Loading Cache", 0);
                if (LoadCache(task))
                {
                    task.Update("Cache Loaded", 100);
                }
                else
                {
                    task.Update("Cache Load Failed", 100);
                    cacheReadFailed = true;
                }
            });
            InitializeComponent();
            if (cacheReadFailed)
            {
                Close();
            }
        }

        private void IdInput_OnKeyUp(object sender, KeyEventArgs e)
        {
            uint value;
            try
            {
                value = Convert.ToUInt32(IdInput.Text, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                AffectorPath.Text = ex is FormatException ? "Invalid Id (make sure you only put in numbers)" : "Invalid Id (value too large to be a UInt32)";
                AffectorReason.Text = "Reference details will appear here";
                lookupFailed = true;
                setOpenAssetButton_IsEnabled(lookupFailed);
                return;
            }

            bool foundValue = idToNameMap.TryGetValue(value, out string name);
            idToReasonMap.TryGetValue(value, out string reason);
            idToIsDupeMap.TryGetValue(value, out bool isDupe);
            isDupe = isDupe || false;

            if (foundValue)
            {
                AffectorPath.Text = name;
                AffectorReason.Text = "Id referenced is " + reason;
                DuplicateWarning.Visibility = isDupe ? Visibility.Visible : Visibility.Collapsed;
                lookupFailed = false;
            }
            else
            {
                AffectorPath.Text = "Asset not found for id. (Maybe this id is from an added affector?)";
                lookupFailed = true;
            }
            setOpenAssetButton_IsEnabled(lookupFailed);
        }

        private void setOpenAssetButton_IsEnabled(bool failure)
        {
            OpenAssetButton.IsEnabled = !failure;
        }

        private void OpenAssetButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (lookupFailed) return;

            Close();

            App.EditorWindow.OpenAsset(App.AssetManager.GetEbxEntry(AffectorPath.Text));
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool LoadCache(FrostyTaskWindow task)
        {
            string cacheFilePath = AffectorGenerateCacheMenuExtension.CacheFilePath;
            if (!File.Exists(cacheFilePath))
            {
                FrostyMessageBox.Show("Cache file not found. Please generate the cache first through \"Tools>Generate Cache>Affector Cache\".");
                return false;
            }
            using (NativeReader reader = new NativeReader(new FileStream(cacheFilePath, FileMode.Open)))
            {
                int version = reader.ReadInt();
                if (version != AffectorGenerateCacheMenuExtension.version)
                {
                    FrostyMessageBox.Show($"Cache file version mismatch. Please generate the cache again. (file: {version}, expected: {AffectorGenerateCacheMenuExtension.version})");
                    return false;
                }
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    uint id = reader.ReadUInt();
                    string reason = reader.ReadNullTerminatedString();
                    string name = reader.ReadNullTerminatedString();
                    idToReasonMap[id] = reason;
                    idToNameMap[id] = name;
                }
                int dupeCount = reader.ReadInt();
                for (int i = 0; i < dupeCount; i++)
                {
                    uint id = reader.ReadUInt();
                    idToIsDupeMap[id] = true;
                }
            }
            return true;
        }
    }
}