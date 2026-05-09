using INIFileRW;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TwinCAT.Ads;

namespace CLS_II
{
    public static class AdsInfo
    {
        public static _AdsObject AdsObject = new _AdsObject();

    }

    public class _AdsObject
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CLSConsts.TotalChannels)]
        public _AdsPort[] AdsPort;

        public _AdsObject()
        {
            this.AdsPort = new _AdsPort[CLSConsts.TotalChannels];
            for (int i = 0; i < CLSConsts.TotalChannels; i++)
            {
                this.AdsPort[i].Enabled = false;
                this.AdsPort[i].Name = "Untitled" + (i + 1);
                this.AdsPort[i].Port = 350 + i;
                this.AdsPort[i].AdsSymbols = new _AdsSymbol[CLSConsts.TotalSymbols];
                for (int j = 0; j < CLSConsts.TotalSymbols; j++)
                {
                    this.AdsPort[i].AdsSymbols[j].IndexGroup = (int)AdsReservedIndexGroups.SymbolValueByHandle;
                }
            }
        }

        public void ReadConfig(string File)
        {
            for (int i = 0; i < CLSConsts.TotalChannels; i++)
            {
                string section = "AdsChannel" + (i + 1);
                this.AdsPort[i].Enabled = bool.Parse(iniFileRW.INIGetStringValue(File, section, "Enabled", "False"));
                this.AdsPort[i].Name = iniFileRW.INIGetStringValue(File, section, "Name", "Untitled" + (i + 1));
                this.AdsPort[i].Port = int.Parse(iniFileRW.INIGetStringValue(File, section, "Port", "" + (350 + i)));
                for (int j = 0; j < CLSConsts.TotalSymbols; j++)
                {
                    string key = "Symbol" + (j + 1);
                    this.AdsPort[i].AdsSymbols[j].Handle = int.Parse(iniFileRW.INIGetStringValue(File, section, key, "0"));
                }
            }
        }

        public void WriteConfig(string File)
        {
            for (int i = 0; i < CLSConsts.TotalChannels; i++)
            {
                string section = "AdsChannel" + (i + 1);
                iniFileRW.INIWriteValue(File, section, "Enabled", this.AdsPort[i].Enabled.ToString());
                iniFileRW.INIWriteValue(File, section, "Name", this.AdsPort[i].Name);
                iniFileRW.INIWriteValue(File, section, "Port", this.AdsPort[i].Port.ToString());
                for (int j = 0; j < CLSConsts.TotalSymbols; j++)
                {
                    string key = "Symbol" + (j + 1);
                    iniFileRW.INIWriteValue(File, section, key, this.AdsPort[i].AdsSymbols[j].Handle.ToString());
                }
            }
        }
    }

    public struct _AdsPort
    {
        public bool Enabled;
        public string Name;
        public int Port;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CLSConsts.TotalSymbols)]
        public _AdsSymbol[] AdsSymbols;
    }

    public struct _AdsSymbol
    {
        public int IndexGroup;
        public int Handle;
    }
}
