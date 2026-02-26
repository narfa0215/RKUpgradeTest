using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RKUpgradeTest
{
    internal class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct INIT_DEV_INFO
        {
            public bool bScan4FsUsb;
            public ushort usRockusbVid;
            public ushort usRockusbPid;
            public ushort usRockMscVid;
            public ushort usRockMscPid;
            public ushort usRockAdbVid;
            public ushort usRockAdbPid;
            public ushort usRockUvcVid;
            public ushort usRockUvcPid;
            public ushort usRockMtpVid;
            public ushort usRockMtpPid;
            public uint uiRockusbTimeout;
            public uint uiRockMscTimeout;
            public uint emSupportDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct INIT_LOG_INFO_W
        {
            public bool bLogEnable;
            public string lpszLogPathName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INIT_CALLBACK_INFO
        {
            public IntPtr pUpgradeStepPromptProc;
            public IntPtr pProgressPromptProc;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STRUCT_DEVICE_DESC_W
        {
            public ushort usVid;
            public ushort usPid;
            public uint dwDeviceInstance;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] // MAX_PATH = 260
            public string szLinkName;

            public uint dwLayer;
            public uint emUsbType;
            public uint emDeviceType;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bUsb20;
        }

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_InitializeW",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern bool RK_Initialize(
            INIT_DEV_INFO devInfo,
            INIT_LOG_INFO_W logInfo,
            INIT_CALLBACK_INFO cbInfo
        );

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_ScanDeviceW",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int RK_ScanDevice(out IntPtr ppDevs);

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_ReadVendorRpmbData",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern bool RK_ReadVendorRpmbData(
            ushort nID,               // 数据 ID
            byte dest,                // 0=Vendor, 1=RPMB
            [Out] byte[] pDataBuffer, // 调用方分配的缓冲区
            ref ushort nBufferSize,   // 传入: 缓冲区大小, 返回: 实际数据大小
            uint dwLayer = 0          // 设备层 (默认0)
        );

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_WriteVendorRpmbData",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern bool RK_WriteVendorRpmbData(
            ushort nID,               // 数据 ID
            byte dest,                // 0=Vendor, 1=RPMB
            [In] byte[] pDataBuffer,  // 要写入的数据
            ushort nBufferSize,       // 数据长度
            uint dwLayer = 0          // 设备层 (默认0)
        );
        
        [DllImport("RKUpgrade.dll", EntryPoint = "RK_Uninitialize",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern bool RK_Uninitialize();
        
        public static T[] PtrToStructArray<T>(IntPtr ptr, int count) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            T[] arr = new T[count];

            for (int i = 0; i < count; i++)
            {
                IntPtr p = IntPtr.Add(ptr, i * size);
                arr[i] = Marshal.PtrToStructure<T>(p);
            }

            return arr;
        }

        public static void Main(string[] args)
        {
            // 初始化结构体
            INIT_DEV_INFO devInfo = new INIT_DEV_INFO
            {
                bScan4FsUsb = false,
                emSupportDevice = 0,
                uiRockMscTimeout = 30,
                uiRockusbTimeout = 30,
                usRockMscPid = 0,
                usRockMscVid = 0,
                usRockusbPid = 0,
                usRockusbVid = 0
            };

            INIT_LOG_INFO_W logInfo = new INIT_LOG_INFO_W
            {
                bLogEnable = false,
                lpszLogPathName = null
            };

            INIT_CALLBACK_INFO callbackInfo = new INIT_CALLBACK_INFO
            {
                pProgressPromptProc = IntPtr.Zero, // 不显示进度
                pUpgradeStepPromptProc = IntPtr.Zero // 不显示步骤
            };

            // 调用初始化
            bool initOk = RK_Initialize(devInfo, logInfo, callbackInfo);
            if (!initOk)
            {
                Console.WriteLine("RK_Initialize failed!");
                return;
            }

            Console.WriteLine("RK_Initialize succeeded!");
            
            // 调用扫描函数
            IntPtr pDevices;
            int nDeviceCount = RK_ScanDevice(out pDevices);

            if (nDeviceCount <= 0 || pDevices == IntPtr.Zero)
            {
                Console.WriteLine("No devices found.");
                return;
            }

            // 转换成 C# 数组
            var devices = PtrToStructArray<STRUCT_DEVICE_DESC_W>(pDevices, nDeviceCount);

            foreach (var dev in devices)
            {
                Console.WriteLine(
                    $"VID={dev.usVid:X4}, PID={dev.usPid:X4}, Link={dev.szLinkName}, USBType=0x{dev.emUsbType:X}");
            }
            
            byte[] oldSnBuffer = new byte[128];
            ushort oldSnBufferLen = (ushort)oldSnBuffer.Length;

            // 读取旧 SN
            bool readOldSnOk = RK_ReadVendorRpmbData(1, 0, oldSnBuffer, ref oldSnBufferLen);
            if (!readOldSnOk)
            {
                Console.WriteLine("Failed to read old SN.");
                return;
            }
            
            string readOldSn = System.Text.Encoding.ASCII.GetString(oldSnBuffer, 0, oldSnBufferLen);
            Console.WriteLine("Read old SN: " + readOldSn);
            
            // 写入新 SN
            string writeNewSn = "2504010029";
            byte[] writeNewSnBytes = System.Text.Encoding.ASCII.GetBytes(writeNewSn);
            ushort writeNewSnBytesLen = (ushort)writeNewSn.Length;
            bool writeNewSnOk = RK_WriteVendorRpmbData(1, 0, writeNewSnBytes, writeNewSnBytesLen);
            if (!writeNewSnOk)
            {
                Console.WriteLine("Write new SN failed.");
                return;
            }
            
            Console.WriteLine("Write new SN: " + writeNewSn);
            
            byte[] newSnBuffer = new byte[128];
            ushort newSnBufferLen = (ushort)newSnBuffer.Length;
            
            // 读取新 SN
            bool readNewSnOk = RK_ReadVendorRpmbData(1, 0, newSnBuffer, ref newSnBufferLen);
            if (!readNewSnOk)
            {
                Console.WriteLine("Failed to read new SN.");
                return;
            }
            
            string readNewSn = System.Text.Encoding.ASCII.GetString(newSnBuffer, 0, newSnBufferLen);
            Console.WriteLine("Read new SN: " + readNewSn);
            
            // 释放已初始化资源
            bool uninitializeOk = RK_Uninitialize();
            if (!uninitializeOk)
            {
                Console.WriteLine("Failed to uninitialize.");
                return;
            }
            
            Console.WriteLine("Completion!");
            
            
        }
    }
}