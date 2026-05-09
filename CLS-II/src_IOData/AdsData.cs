using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
{
    static class AdsData
    {
        public static _CLSModel CLSModel = new _CLSModel();
        public static _CLSParam CLSParam = new _CLSParam();
        public static _CLS5K CLS5K = new _CLS5K();
        public static _CLSConsts CLSConsts = new _CLSConsts();
        public static _TestMDL TestMDL = new _TestMDL();
    }

    

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 152)]
    public struct _CLSModel
    {
        public Double bA;
        public Double bF;
        public Double bH;
        public Double bS;
        public Double kS;
        public Double kH;
        public Double kQ;
        public Double kU;
        public Double mA;
        public Double mF;
        public Double Vbrk;
        public Double Fbrk;
        public Double Fc;
        public Double fF;
        public Double dzF;
        public Double NFC_P;
        public Double NFC_I;
        public Double NFC_D;
        public Double NFC_N;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct _CLSParam
    {
        [FieldOffset(0)] [MarshalAs(UnmanagedType.U1)] public bool AutoHoming;
        [FieldOffset(2)] public UInt16 VPos;
        [FieldOffset(4)] public Int16 Jagment;
        [FieldOffset(8)] public Int32 JagmentP;
        [FieldOffset(12)] public Int32 JagmentN;
        [FieldOffset(16)] public Int32 P0Home;
        [FieldOffset(24)] public Double Foffset;
        [FieldOffset(32)] public Double F0Aoff;
        [FieldOffset(40)] public Double Poffset;
        [FieldOffset(48)] public Double P0Trim;
        [FieldOffset(56)] public Double L0Trim;
        [FieldOffset(64)] public Double L0TravA;
        [FieldOffset(72)] public Double L0TravB;
        [FieldOffset(80)] public Double ShakerA;
        [FieldOffset(88)] public Double ShakerF;

    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 120)]
    public struct _CLS5K
    {
        public Double K0;
        public Double K1;
        public Double K2;
        public Double K3;
        public Double K4;
        public Double K5;
        public Double X0;
        public Double X1;
        public Double X2;
        public Double X3;
        public Double X4;
        public Double X5;
        public Double X6;
        public Double Ke;
        public Double Vt;
    }

    [StructLayout(LayoutKind.Explicit, Size = 88)]
    public struct _CLSConsts
    {
        [FieldOffset(0)] [MarshalAs(UnmanagedType.U1)] public bool KUSEDEG;
        [FieldOffset(1)] [MarshalAs(UnmanagedType.U1)] public bool CCWDIR;
        [FieldOffset(2)] public UInt16 KFLMT;
        [FieldOffset(8)] public Double KFmax;
        [FieldOffset(16)] public Double KVmax;
        [FieldOffset(24)] public Double KLFWD;
        [FieldOffset(32)] public Double KX2P;
        [FieldOffset(40)] public Double KFXA;
        [FieldOffset(48)] public Double KForceTo;
        [FieldOffset(56)] public Double KForceTQ;
        [FieldOffset(64)] public Double KForceTV;
        [FieldOffset(72)] public Double KRPM2DPS;
        [FieldOffset(80)] public Double KREV2DEG;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 88)]
    public struct _TestMDL
    {
        public Double Km;
        public Double Ks;
        public Double Kp;
        public Double Ka;
        public Double Kq;
        public Double bs;
        public Double bA;
        public Double Ksf;
        public Double bsf;
        public Double DL;
        public Double PL;
    }


}
