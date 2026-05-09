using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using INIFileRW;

/*  File:MainConfig.ini
 *  储存:
 *      (1) 主界面相关配置项
 *      (2) 子界面配置文件路径
 * 
 * 
 */

namespace CLS_II
{
    class MainConfig
    {
        public static string mainConfigFile = @".\MainConfig.ini";
        public static _ConfigInfo ConfigInfo;

        public struct _ConfigInfo
        {
            public _SetItems SetItems;
            public _DebugItems DebugItems;
            public _FileItems FileItems;
        }

        public struct _SetItems
        {
            public string Language;
        }

        public struct _DebugItems
        {
            public bool isWatchVisible;
            public bool isAutoWatch;
            public int WatchMode;
        }

        public struct _FileItems
        {
            public string WatchFile;
            public string ProjectFile;
        }

        public enum WatchDataMode
        {
            DEC = 0,
            HEX = 1,
            OCT = 2,
            BIN = 3
        }

        public static void ConfigFileInit()
        {
            if (File.Exists(mainConfigFile))
            {
                ReadConfigFile();
            }
            else
            {
                CreateConfigFile();
                ReadConfigFile();
            }
        }

        private static void CreateConfigFile()
        {
            // SetItems
            iniFileRW.INIWriteValue(mainConfigFile, "SetItems", "Language", "zh");

            // DebugItems
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isWatchVisible", "true");
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isAutoWatch", "false");
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "WatchMode", "0");

            // FileItems
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile", @".\WatchConfig.ini");
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ProjectFile", @".\ProjectConfig.ini");
        }

        private static void ReadConfigFile()
        {
            // SetItems
            ConfigInfo.SetItems.Language = iniFileRW.INIGetStringValue(mainConfigFile, "SetItems", "Language", "zh");

            // DebugItems
            ConfigInfo.DebugItems.isWatchVisible = bool.Parse(iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "isWatchVisible", "true"));
            ConfigInfo.DebugItems.isAutoWatch = bool.Parse(iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "isAutoWatch", "false"));
            ConfigInfo.DebugItems.WatchMode = int.Parse(iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "WatchMode", "0"));

            // FileItems
            ConfigInfo.FileItems.WatchFile = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "WatchFile", @".\WatchConfig.ini");
            ConfigInfo.FileItems.ProjectFile = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "ProjectFile", @".\ProjectConfig.ini");

            WatchConfig.SetDefaultWatchConfigFile(ConfigInfo.FileItems.WatchFile);
        }

        public static void WriteConfigFile()
        {
            // SetItems
            iniFileRW.INIWriteValue(mainConfigFile, "SetItems", "Language", ConfigInfo.SetItems.Language);

            // DebugItems
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isWatchVisible", ConfigInfo.DebugItems.isWatchVisible.ToString());
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isAutoWatch", ConfigInfo.DebugItems.isAutoWatch.ToString());
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "WatchMode", ConfigInfo.DebugItems.WatchMode.ToString());
        }
    }
}
