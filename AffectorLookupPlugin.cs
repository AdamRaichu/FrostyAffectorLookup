using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AffectorLookupPlugin.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;

namespace AffectorLookupPlugin
{
    public class AffectorGenerateCacheMenuExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Generate Cache";
        public override string MenuItemName => "Affector Lookup Cache";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Database.png") as ImageSource;

        public static string CacheDirectory = System.AppDomain.CurrentDomain.BaseDirectory + @"Plugins\Caches\";
        public static string CacheFilePath = CacheDirectory + Enum.GetName(typeof(ProfileVersion), ProfilesLibrary.DataVersion) + "_AffectorLookupPlugin_Cache.cache";
        public static int version = 0x00000003;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            FrostyTaskWindow.Show("Creating Cache", "Enumerating EBX Files...", (task) =>
            {
                CreateCache(task);
            });
        });

        private void CreateCache(FrostyTaskWindow task)
        {
            AssetManager AM = App.AssetManager;
            List<EbxAssetEntry> ebxFiles = AM.EnumerateEbx(type: "AffectorAsset").ToList();
            int index = 0;
            int count = ebxFiles.Count;
            List<UInt32> ids = new List<UInt32>();
            List<string> reasons = new List<string>();
            List<string> paths = new List<string>();
            List<UInt32> idsWithDuplicates = new List<UInt32>();
            ebxFiles.ForEach((affectorEntry) =>
            {
                if (affectorEntry.IsAdded) {
                    App.Logger.Log("Skipping " + affectorEntry.Name + " as it is an added asset.");
                    return;
                }

                EbxAsset affectorAsset = AM.GetEbx(affectorEntry);

                task.Update($"Processing ({index + 1}/{count}): {affectorEntry.Filename}", index / count * 50);
                dynamic root = affectorAsset.RootObject;
                if (ids.Contains(root.Identifier))
                {
                    if (!idsWithDuplicates.Contains(root.Identifier))
                    {
                        idsWithDuplicates.Add(root.Identifier);
                    }
                } else
                {
                    ids.Add(root.Identifier);
                    reasons.Add("asset.Identifier");
                    paths.Add(affectorEntry.Name);
                }
                List<UInt32> descriptorIds = (List<UInt32>) root.DescriptorIds;
                for (int i = 0; i < descriptorIds.Count; i++) {
                    
                    if (ids.Contains(descriptorIds[i]))
                    {
                        if (!idsWithDuplicates.Contains(descriptorIds[i]))
                        {
                            idsWithDuplicates.Add(descriptorIds[i]);
                        }
                    } else
                    {
                        ids.Add(descriptorIds[i]);
                        reasons.Add($"asset.DescriptorIds[{i}]");
                        paths.Add(affectorEntry.Name);
                    }
                }
                index++;
            });
            using (NativeWriter writer = new NativeWriter(new FileStream(CacheFilePath, FileMode.Create)))
            {
                // format version
                writer.Write(version);
                // number of ids
                writer.Write(ids.Count);
                // for each id, track...
                for (int i = 0; i < ids.Count; i++)
                {
                    // the id itself
                    writer.Write(ids[i]);
                    // where it was referenced in the asset
                    writer.WriteNullTerminatedString(reasons[i]);
                    // the path to the asset
                    writer.WriteNullTerminatedString(paths[i]);
                }
                // number of ids with duplicates
                writer.Write(idsWithDuplicates.Count);
                // for each id with duplicates, write the actual id
                for (int i = 0; i < idsWithDuplicates.Count; i++)
                {
                    writer.Write(idsWithDuplicates[i]);
                }
            }
        }
    }

    public class ViewAffectorLookupWindow : MenuExtension
    {
        // public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/AffectorLookupPlugin;component/Images/Hexagon.png") as ImageSource;

        public override string TopLevelMenuName => "View";
        public override string MenuItemName => "Affector Lookup";

        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            AffectorLookupWindow window = new AffectorLookupWindow();
            if (!window.cacheReadFailed)
                window.Show();
        });
    }
}