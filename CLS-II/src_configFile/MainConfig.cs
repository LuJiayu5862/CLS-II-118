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
 *  路径策略（2026-05-15 任务3 修订）：
 *      mainConfigFile  → 跟 exe 走，固定为 .\MainConfig.ini
 *      WatchFile       → 跟 xrp 走，打开 xrp 后由 RelocateConfigPaths 重定位
 *                        默认 fallback：.\config\WatchConfig.ini
 *      JdConstsFile    → 跟 xrp 走，同上，默认 .\config\JdConfig.ini
 *      ParamConstsFile → 跟 xrp 走，同上，默认 .\config\ParamConfig.ini
 *      ProjectFile     → 保留字段，暂未使用，维持 .\ProjectConfig.ini
 *
 *  启动时 Watch 窗口为空；打开 xrp 后由 LoadProjectFile 调用
 *  RelocateConfigPaths，Watch 才从 xrp 目录下的 config/ 加载。
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
            public string JdConstsFile;     // 新增 2026-05-15，跟 xrp 走
            public string ParamConstsFile;  // 新增 2026-05-15，跟 xrp 走
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
            // WatchFile / JdConstsFile / ParamConstsFile 的最终路径由 RelocateConfigPaths 在
            // LoadProjectFile 后重算；此处写入 fallback 默认值供首次读取。
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile",       @".\config\WatchConfig.ini");
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ProjectFile",     @".\ProjectConfig.ini");
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",    @".\config\JdConfig.ini");
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile", @".\config\ParamConfig.ini");
        }

        private static void ReadConfigFile()
        {
            // SetItems
            ConfigInfo.SetItems.Language = iniFileRW.INIGetStringValue(mainConfigFile, "SetItems", "Language", "zh");

            // DebugItems
            ConfigInfo.DebugItems.isWatchVisible = bool.Parse(iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "isWatchVisible", "true"));
            ConfigInfo.DebugItems.isAutoWatch    = bool.Parse(iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "isAutoWatch",    "false"));
            ConfigInfo.DebugItems.WatchMode      = int.Parse( iniFileRW.INIGetStringValue(mainConfigFile, "DebugItems", "WatchMode",      "0"));

            // FileItems
            ConfigInfo.FileItems.WatchFile       = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "WatchFile",       @".\config\WatchConfig.ini");
            ConfigInfo.FileItems.ProjectFile     = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "ProjectFile",     @".\ProjectConfig.ini");
            ConfigInfo.FileItems.JdConstsFile    = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "JdConstsFile",    @".\config\JdConfig.ini");
            ConfigInfo.FileItems.ParamConstsFile = iniFileRW.INIGetStringValue(mainConfigFile, "FileItems", "ParamConstsFile", @".\config\ParamConfig.ini");

            // ⚠️ 注意：此处不再调用 WatchConfig.SetDefaultWatchConfigFile()
            // 启动时 Watch 窗口为空，打开 xrp 后由 LoadProjectFile → RelocateConfigPaths 触发加载。
        }

        public static void WriteConfigFile()
        {
            // SetItems
            iniFileRW.INIWriteValue(mainConfigFile, "SetItems", "Language", ConfigInfo.SetItems.Language);

            // DebugItems
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isWatchVisible", ConfigInfo.DebugItems.isWatchVisible.ToString());
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "isAutoWatch",    ConfigInfo.DebugItems.isAutoWatch.ToString());
            iniFileRW.INIWriteValue(mainConfigFile, "DebugItems", "WatchMode",      ConfigInfo.DebugItems.WatchMode.ToString());

            // FileItems（修复之前的遗漏：WriteConfigFile 原来没有写 FileItems）
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile",       ConfigInfo.FileItems.WatchFile);
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",    ConfigInfo.FileItems.JdConstsFile);
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile", ConfigInfo.FileItems.ParamConstsFile);
            // ProjectFile 不写（字段保留，暂未使用，不需持久化）
        }

        /// <summary>
        /// 将 WatchConfig / JdConfig / ParamConfig 的路径重定位到 xrp 同目录的 config\ 子文件夹。
        /// 由 LoadProjectFile() 在 xrp 加载成功后调用。
        /// config\ 不存在时自动创建；各 ini 文件不存在时由对应的 ConfigFileInit 负责创建。
        /// </summary>
        public static void RelocateConfigPaths(string xrpDir)
        {
            if (string.IsNullOrEmpty(xrpDir)) return;

            string configDir = Path.Combine(xrpDir, "config");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            ConfigInfo.FileItems.WatchFile       = Path.Combine(configDir, "WatchConfig.ini");
            ConfigInfo.FileItems.JdConstsFile    = Path.Combine(configDir, "JdConfig.ini");
            ConfigInfo.FileItems.ParamConstsFile = Path.Combine(configDir, "ParamConfig.ini");

            // 持久化到 MainConfig.ini，下次启动直接走新路径
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile",       ConfigInfo.FileItems.WatchFile);
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",    ConfigInfo.FileItems.JdConstsFile);
            iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile", ConfigInfo.FileItems.ParamConstsFile);

            // 通知 WatchConfig 切换到新路径
            // SetDefaultWatchConfigFile 内部处理：文件不存在则自动创建空白 ini
            WatchConfig.SetDefaultWatchConfigFile(ConfigInfo.FileItems.WatchFile);
        }
    }
}
