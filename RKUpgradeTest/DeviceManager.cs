using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RKUpgradeTest
{
    public class DeviceManager
    {
        // 结构体定义
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

        // DllImport 声明
        [DllImport("RKUpgrade.dll", EntryPoint = "RK_InitializeW",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern bool RK_Initialize(
            INIT_DEV_INFO devInfo,
            INIT_LOG_INFO_W logInfo,
            INIT_CALLBACK_INFO cbInfo
        );

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_ScanDeviceW",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern int RK_ScanDevice(out IntPtr ppDevs);

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_ReadVendorRpmbData",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern bool RK_ReadVendorRpmbData(
            ushort nID,               // 数据 ID
            byte dest,                // 0=Vendor, 1=RPMB
            [Out] byte[] pDataBuffer, // 调用方分配的缓冲区
            ref ushort nBufferSize,   // 传入: 缓冲区大小, 返回: 实际数据大小
            uint dwLayer = 0          // 设备层 (默认0)
        );

        [DllImport("RKUpgrade.dll", EntryPoint = "RK_WriteVendorRpmbData",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern bool RK_WriteVendorRpmbData(
            ushort nID,               // 数据 ID
            byte dest,                // 0=Vendor, 1=RPMB
            [In] byte[] pDataBuffer,  // 要写入的数据
            ushort nBufferSize,       // 数据长度
            uint dwLayer = 0          // 设备层 (默认0)
        );
        
        [DllImport("RKUpgrade.dll", EntryPoint = "RK_Uninitialize",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private static extern bool RK_Uninitialize();

        /// <summary>
        /// 将指针转换为结构体数组
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="ptr">指针</param>
        /// <param name="count">元素数量</param>
        /// <returns>结构体数组</returns>
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

        /// <summary>
        /// 初始化设备管理
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public bool Initialize()
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
            return initOk;
        }

        /// <summary>
        /// 扫描设备
        /// </summary>
        /// <returns>设备列表</returns>
        public STRUCT_DEVICE_DESC_W[] ScanDevices()
        {
            IntPtr pDevices;
            int nDeviceCount = RK_ScanDevice(out pDevices);

            if (nDeviceCount <= 0 || pDevices == IntPtr.Zero)
            {
                return new STRUCT_DEVICE_DESC_W[0];
            }

            // 转换成 C# 数组
            var devices = PtrToStructArray<STRUCT_DEVICE_DESC_W>(pDevices, nDeviceCount);
            return devices;
        }

        /// <summary>
        /// 重启设备到loader模式
        /// </summary>
        /// <returns>重启是否成功</returns>
        public bool RebootToLoader()
        {
            try
            {
                // 创建进程启动信息
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "reboot loader",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false // 显示命令窗口，以便查看执行过程
                };
                
                // 启动进程
                using (Process process = Process.Start(startInfo))
                {
                    // 读取输出
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    // 等待进程结束
                    process.WaitForExit();
                    
                    // 输出执行结果
                    Console.WriteLine(output);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("Error: " + error);
                    }
                    
                    // 判断重启是否成功（adb命令通常在成功时没有输出）
                    bool success = string.IsNullOrEmpty(error);
                    
                    return success;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during reboot to loader: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 读取设备 SN
        /// </summary>
        /// <param name="id">数据 ID</param>
        /// <param name="dest">目标存储 (0=Vendor, 1=RPMB)</param>
        /// <returns>SN 字符串</returns>
        public string ReadSN(ushort id = 1, byte dest = 0)
        {
            byte[] buffer = new byte[128];
            ushort bufferLen = (ushort)buffer.Length;

            bool readOk = RK_ReadVendorRpmbData(id, dest, buffer, ref bufferLen);
            if (!readOk)
            {
                return null;
            }

            string sn = System.Text.Encoding.ASCII.GetString(buffer, 0, bufferLen);
            return sn;
        }

        /// <summary>
        /// 写入设备 SN
        /// </summary>
        /// <param name="sn">SN 字符串</param>
        /// <param name="id">数据 ID</param>
        /// <param name="dest">目标存储 (0=Vendor, 1=RPMB)</param>
        /// <returns>写入是否成功</returns>
        public bool WriteSN(string sn, ushort id = 1, byte dest = 0)
        {
            byte[] snBytes = System.Text.Encoding.ASCII.GetBytes(sn);
            ushort snBytesLen = (ushort)sn.Length;
            bool writeOk = RK_WriteVendorRpmbData(id, dest, snBytes, snBytesLen);
            return writeOk;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <returns>释放是否成功</returns>
        public bool Uninitialize()
        {
            return RK_Uninitialize();
        }

        /// <summary>
        /// 检查设备是否需要重启到 loader 模式
        /// </summary>
        /// <param name="devices">设备列表</param>
        /// <returns>是否需要重启</returns>
        public bool NeedRebootToLoader(STRUCT_DEVICE_DESC_W[] devices)
        {
            foreach (var dev in devices)
            {
                // 检查USBType是否为0x8 (ADB模式)
                if (dev.emUsbType == 0x8)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 等待设备重新连接
        /// </summary>
        /// <param name="waitTimeMs">等待时间（毫秒）</param>
        public void WaitForDeviceReconnect(int waitTimeMs = 15000)
        {
            Thread.Sleep(waitTimeMs);
        }
    }
}