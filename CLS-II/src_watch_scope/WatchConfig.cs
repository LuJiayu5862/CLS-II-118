using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using INIFileRW;

/*  File:WatchConfig.ini
 *  储存:
 *      (1) Watch窗口添加的监测对象及其相关属性
 * 
 * 
 * 
 */

namespace CLS_II
{
    class WatchConfig
    {
        public static string watchConfigFile = @".\WatchConfig.ini";
        public static List<_VarietyInfo> VarietyInfos = new List<_VarietyInfo>();

        public class _ScopeVariety
        {
            public string VarName { get; set; }
            public string Type { get; set; }
            public Double Value { get; set; }

            public _ScopeVariety() { }
        }

        public static List<_ScopeVariety> ScopeVarieties = new List<_ScopeVariety>();

        public class _VarietyInfo
        {
            public string VarName { get; set; }
            public string Scope { get; set; }
            public string Category { get; set; }    // ADS or UDP
            public string Type { get; set; }        // SINT INT DINT LINT REAL LREAL BOOL STRING
            public string Value { get; set; }
            public string NextValue { get; set; }
            public string Port { get; set; }
            public string Source { get; set; }
            public string Comment { get; set; }

            public _VarietyInfo() { }
            public _VarietyInfo(string _VarName, string _Category, string _Type, string _Port, string _Source, string _Comment = "", bool isScope = false)
            {
                this.VarName = _VarName;
                this.Category = _Category;
                this.Type = _Type;
                this.Port = _Port;
                this.Source = _Source;
                this.Comment = _Comment;
                this.Scope = isScope ? "True" : "False";
            }
        }

        public static void SetDefaultWatchConfigFile(string file)
        {
            watchConfigFile = file;
        }

        public static void ConfigFileInit()
        {
            if (File.Exists(watchConfigFile))
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
            // Info            
            iniFileRW.INIWriteValue(watchConfigFile, "Info", "LastModifiedTime", DateTime.Now.ToString());
        }

        private static void ReadConfigFile()
        {
            iniFileRW.INIDeleteSection(watchConfigFile, "Info");
            VarietyInfos.Clear();
            string[] sections = iniFileRW.INIGetAllSectionNames(watchConfigFile);
            foreach (string s in sections)
            {
                string realName = UnescapeSectionName(s);
                _VarietyInfo v = new _VarietyInfo
                (
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "VarName", realName),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Category", String.Empty),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Type", String.Empty),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Port", String.Empty),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Source", String.Empty),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Comment", String.Empty),
                    iniFileRW.INIGetStringValue(watchConfigFile, s, "Scope", "False") == "True" ? true : false
                );
                VarietyInfos.Add(v);
            }
        }

        public static void WriteConfigFile()
        {
            string[] sections = iniFileRW.INIGetAllSectionNames(watchConfigFile);
            foreach (string s in sections)
            {
                iniFileRW.INIDeleteSection(watchConfigFile, s);
            }
            // Info            
            iniFileRW.INIWriteValue(watchConfigFile, "Info", "LastModifiedTime", DateTime.Now.ToString());

            // Variety
            foreach (_VarietyInfo v in VarietyInfos)
            {
                string sec = EscapeSectionName(v.VarName);

                iniFileRW.INIWriteValue(watchConfigFile, sec, "Name", v.VarName);
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Scope", v.Scope == "True"? "True" : "False");
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Category", v.Category is null ? string.Empty : v.Category);
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Type", v.Type is null?string.Empty:v.Type);
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Port", v.Port is null ? string.Empty : v.Port);
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Source", v.Source is null ? string.Empty : v.Source);
                iniFileRW.INIWriteValue(watchConfigFile, sec, "Comment", v.Comment is null ? string.Empty : v.Comment);
            }
        }

        // ── INI Section名转义：[ → {{  ] → }}
        private static string EscapeSectionName(string name)
        {
            return name.Replace("[", "{{").Replace("]", "}}");
        }

        // ── INI Section名反转义：{{ → [  }} → ]
        private static string UnescapeSectionName(string name)
        {
            return name.Replace("{{", "[").Replace("}}", "]");
        }
    }
}
