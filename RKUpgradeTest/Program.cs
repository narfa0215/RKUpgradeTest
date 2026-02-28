using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RKUpgradeTest
{
    internal class Program
    {
        /// <summary>
        /// 升级步骤状态
        /// </summary>
        public enum UpgradePrompt
        {
            DOWNLOADBOOT_START = 1,      //开始下载Boot
            DOWNLOADBOOT_FAIL = 2,       //下载Boot失败
            DOWNLOADBOOT_PASS = 3,       //下载Boot成功
            DOWNLOADIDBLOCK_START = 4,   //开始下载IDBlock数据
            DOWNLOADIDBLOCK_FAIL = 5,    //下载IDBlock数据失败
            DOWNLOADIDBLOCK_PASS = 6,    //下载IDBlock数据成功
            DOWNLOADIMAGE_START = 7,     //开始下载Image文件
            DOWNLOADIMAGE_FAIL = 8,      //下载Image文件失败
            DOWNLOADIMAGE_PASS = 9,      //下载Image文件成功
            TESTDEVICE_START = 10,       //开始测试设备是否就绪
            TESTDEVICE_FAIL = 11,        //测试设备失败
            TESTDEVICE_PASS = 12,        //测试设备成功
            RESETDEVICE_START = 13,      //开始重启设备
            RESETDEVICE_FAIL = 14,       //重启设备失败
            RESETDEVICE_PASS = 15,       //重启设备成功
            FORMATDISK_START = 16,       //开始格式化磁盘
            FORMATDISK_FAIL = 17,        //格式化磁盘失败
            FORMATDISK_PASS = 18,        //格式化磁盘成功
            COPYDATA_START = 19,         //开始拷贝数据
            COPYDATA_FAIL = 20,          //拷贝数据失败
            COPYDATA_PASS = 21,          //拷贝数据成功
            WAITMSC_START = 22,          //开始等待U盘重新连上
            WAITMSC_FAIL = 23,           //等待U盘重新连上失败
            WAITMSC_PASS = 24,           //等待U盘重新连上成功
            WAITLOADER_START = 25,       //开始等待RockUsbLoader设备重新连上
            WAITLOADER_FAIL = 26,        //等待RockUsbLoader设备重新连上失败
            WAITLOADER_PASS = 27,        //等待RockUsbLoader设备重新连上成功
            WAITMASKROM_START = 28,      //开始等待RockUsbMaskrom设备重新连上
            WAITMASKROM_FAIL = 29,       //等待RockUsbMaskrom设备重新连上失败
            WAITMASKROM_PASS = 30,       //等待RockUsbMaskrom设备重新连上成功
            ERASEIDB_START = 31,         //开始擦除IDBlock数据
            ERASEIDB_FAIL = 32,          //擦除IDBlock数据失败
            ERASEIDB_PASS = 33,          //擦除IDBlock数据成功
            SWITCHMSC_START = 34,        //开始切换U盘至RockUsb设备
            SWITCHMSC_FAIL = 35,         //切换U盘至RockUsb设备失败
            SWITCHMSC_PASS = 36,         //切换U盘至RockUsb设备成功
            CHECKCHIP_START = 37,        //开始检测芯片是否支持
            CHECKCHIP_FAIL = 38,         //检测芯片失败
            CHECKCHIP_PASS = 39,         //检测芯片成功
            PREPAREIDB_START = 40,       //开始构建IDBlock数据
            PREPAREIDB_FAIL = 41,        //构建IDBlock数据失败
            PREPAREIDB_PASS = 42,        //构建IDBlock数据成功
            MUTEXRESETDEVICE_START = 43, //开始互斥重启设备
            MUTEXRESETDEVICE_FAIL = 44,  //互斥重启设备失败
            MUTEXRESETDEVICE_PASS = 45,  //互斥重启设备成功
            GETOLDDISKSIZE_START = 46,   //开始从设备获取旧磁盘大小
            GETOLDDISKSIZE_FAIL = 47,    //从设备获取旧磁盘大小失败
            GETOLDDISKSIZE_PASS = 48,    //从设备获取旧磁盘大小成功
            READSN_START = 49,           //开始读取SN
            READSN_FAIL = 50,            //读取SN失败
            READSN_PASS = 51,            //读取SN成功
            WRITESN_START = 52,          //开始写入SN
            WRITESN_FAIL = 53,           //写入SN失败
            WRITESN_PASS = 54,           //写入SN成功
            ERASEALLBLOCKS_START = 55,   //开始擦除整片Flash
            ERASEALLBLOCKS_FAIL = 56,    //擦除整片Flash失败
            ERASEALLBLOCKS_PASS = 57,    //擦除整片Flash成功
            GETBLOCKSTATE_START = 58,    //开始获取Flash块状态
            GETBLOCKSTATE_FAIL = 59,     //获取Flash块状态失败
            GETBLOCKSTATE_PASS = 60,     //获取Flash块状态成功
            GETFLASHINFO_START = 61,     //开始获取Flash信息
            GETFLASHINFO_FAIL = 62,      //获取Flash信息失败
            GETFLASHINFO_PASS = 63,      //获取Flash信息成功
            WRITEBACK_START = 64,        //开始回写备份数据
            WRITEBACK_FAIL = 65,         //回写备份数据失败
            WRITEBACK_PASS = 66,         //回写备份数据成功
            FINDUSERDISK_START = 67,     //开始搜寻用户磁盘
            FINDUSERDISK_FAIL = 68,      //搜寻用户磁盘失败
            FINDUSERDISK_PASS = 69,      //搜寻用户磁盘成功
            SHOWUSERDISK_START = 70,     //开始启用用户磁盘（为了保证不拷贝进SD卡）
            SHOWUSERDISK_FAIL = 71,      //启用用户磁盘失败
            SHOWUSERDISK_PASS = 72,      //启用用户磁盘成功
            READMAC_START = 73,          //开始读网卡地址
            READMAC_FAIL = 74,           //读网卡地址失败
            READMAC_PASS = 75,           //读网卡地址成功
            WRITEMAC_START = 76,         //开始写网卡地址
            WRITEMAC_FAIL = 77,          //写网卡地址失败
            WRITEMAC_PASS = 78,          //写网卡地址成功
            READBT_START = 79,           //开始读蓝牙地址
            READBT_FAIL = 80,            //读蓝牙地址失败
            READBT_PASS = 81,            //读蓝牙地址成功
            WRITEBT_START = 82,          //开始写蓝牙地址
            WRITEBT_FAIL = 83,           //写蓝牙地址失败
            WRITEBT_PASS = 84,           //写蓝牙地址成功
            LOWERFORMAT_START = 85,      //设备低格开始
            LOWERFORMAT_FAIL = 86,       //设备低格失败
            LOWERFORMAT_PASS = 87,       //设备低格成功
            READIMEI_START = 88,         //读IMEI开始
            READIMEI_FAIL = 89,          //读IMEI失败
            READIMEI_PASS = 90,          //读IMEI成功
            WRITEIMEI_START = 91,        //写IMEI开始
            WRITEIMEI_FAIL = 92,         //写IMEI失败
            WRITEIMEI_PASS = 93,         //写IMEI成功
            SHOWDATADISK_START = 94,     //显示数据盘开始
            SHOWDATADISK_FAIL = 95,      //显示数据盘失败
            SHOWDATADISK_PASS = 96,      //显示数据盘成功
            FINDDATADISK_START = 97,     //查找数据盘开始
            FINDDATADISK_FAIL = 98,      //查找数据盘失败
            FINDDATADISK_PASS = 99,      //查找数据盘成功
            FORMATDATADISK_START = 100,  //格式化数据盘开始
            FORMATDATADISK_FAIL = 101,   //格式化数据盘失败
            FORMATDATADISK_PASS = 102,   //格式化数据盘成功
            COPYDATADISK_START = 103,    //数据盘拷贝开始
            COPYDATADISK_FAIL = 104,     //数据盘拷贝失败
            COPYDATADISK_PASS = 105,     //数据盘拷贝成功
            READUID_START = 106,         //读UID开始
            READUID_FAIL = 107,          //读UID失败
            READUID_PASS = 108,          //读UID成功
        }

        /// <summary>
        /// 进度提示
        /// </summary>
        public enum ProgressPrompt
        {
            TESTDEVICE_PROGRESS,    //设备测试进度
            DOWNLOADIMAGE_PROGRESS, //Image文件下载进度
            CHECKIMAGE_PROGRESS,    //Image有效性检测进度
            TAGBADBLOCK_PROGRESS,   //Flash坏块标记进度
            TESTBLOCK_PROGRESS,     //Flash块检测进度
            ERASEFLASH_PROGRESS,    //Flash擦除进度
        }
        
        /// <summary>
        /// 调用次序
        /// </summary>
        public enum CallStep
        {
            CALL_FIRST,  //第一次调用
            CALL_MIDDLE, //中途调用
            CALL_LAST,   //最后一次调用
        }
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void UpgradeStepPromptCB(
            uint deviceLayer,
            int promptID,
            uint oldDeviceLayer
        );
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ProgressPromptCB(
            uint deviceLayer,
            int promptID,
            uint totalValue,
            uint currentValue,
            int emCall
        );
        
        private static void OnUpgradeStep(
            uint deviceLayer,
            int promptID,
            uint oldDeviceLayer)
        {
            Console.WriteLine("步骤ID: " + promptID);

            if (promptID == 7) // DOWNLOADIMAGE_START
                Console.WriteLine("开始下载固件");

            if (promptID == 9) // DOWNLOADIMAGE_PASS
                Console.WriteLine("固件下载成功");
        }
        
        private static void OnProgress(
            uint deviceLayer,
            int promptID,
            uint totalValue,
            uint currentValue,
            int emCall)
        {
            if (totalValue == 0) return;

            int percent = (int)(currentValue * 100 / totalValue);

            Console.WriteLine($"进度: {percent}%");
        }
        
        private static UpgradeStepPromptCB upgradeStepDelegate;
        private static ProgressPromptCB progressDelegate;
        
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
            upgradeStepDelegate = OnUpgradeStep;
            progressDelegate = OnProgress;

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
                pProgressPromptProc = Marshal.GetFunctionPointerForDelegate(progressDelegate),
                pUpgradeStepPromptProc = Marshal.GetFunctionPointerForDelegate(upgradeStepDelegate)
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