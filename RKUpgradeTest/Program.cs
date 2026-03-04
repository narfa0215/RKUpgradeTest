using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
            
            bool uninitializeOk;
            try
            {
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

                // 检查是否有设备需要重启到loader模式
                bool needReboot = false;
                foreach (var dev in devices)
                {
                    Console.WriteLine(
                        $"VID={dev.usVid:X4}, PID={dev.usPid:X4}, Link={dev.szLinkName}, USBType=0x{dev.emUsbType:X}");
                    
                    // 检查USBType是否为0x8
                    if (dev.emUsbType == 0x8)
                    {
                        needReboot = true;
                        break;
                    }
                }
                
                // 如果需要重启到loader模式
                if (needReboot)
                {
                    Console.WriteLine("\nDetected device in ADB mode (USBType=0x8). Rebooting to loader mode...");
                    if (RebootToLoader())
                    {
                        Console.WriteLine("Reboot to loader mode succeeded. Waiting for device to reconnect...");
                        // 等待设备重新连接
                        Thread.Sleep(15000);
                        
                        // 重新扫描设备
                        Console.WriteLine("Rescanning devices...");
                        nDeviceCount = RK_ScanDevice(out pDevices);
                        
                        if (nDeviceCount <= 0 || pDevices == IntPtr.Zero)
                        {
                            Console.WriteLine("No devices found after reboot.");
                            return;
                        }
                        
                        // 重新转换设备数组
                        devices = PtrToStructArray<STRUCT_DEVICE_DESC_W>(pDevices, nDeviceCount);
                        
                        Console.WriteLine("Devices after reboot:");
                        foreach (var dev in devices)
                        {
                            Console.WriteLine(
                                $"VID={dev.usVid:X4}, PID={dev.usPid:X4}, Link={dev.szLinkName}, USBType=0x{dev.emUsbType:X}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to reboot to loader mode.");
                        return;
                    }
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
            }
            finally
            {
                // 释放已初始化资源
                uninitializeOk = RK_Uninitialize();
            }
            if (!uninitializeOk)
            {
                Console.WriteLine("Failed to uninitialize.");
                return;
            }
            
            Console.WriteLine("RK_Uninitialize succeeded!");
            
            Console.WriteLine("Completion!");
            
            // 执行固件升级
            Console.WriteLine("\nStarting firmware upgrade...");
            bool upgradeSuccess = UpgradeFirmware(@"D:\Temp\update.img");
            if (upgradeSuccess)
            {
                Console.WriteLine("Firmware upgrade completed successfully!");
            }
            else
            {
                Console.WriteLine("Firmware upgrade failed!");
            }
        }
        
        /// <summary>
        /// 重启设备到loader模式
        /// </summary>
        /// <returns>重启是否成功</returns>
        public static bool RebootToLoader()
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
        
        // ------------------- Win32 API -------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
            uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            uint dwAttributeCount,
            uint dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PeekNamedPipe(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize,
            out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);

        const uint HANDLE_FLAG_INHERIT = 0x00000001;
        const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        const uint INFINITE = 0xFFFFFFFF;
        const uint WAIT_OBJECT_0 = 0;

        const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        struct COORD
        {
            public short X;
            public short Y;
            public COORD(short x, short y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public uint cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        // ------------------- 伪终端运行程序 -------------------
        public static int RunProcessWithConPTY(string fileName, string args)
        {
            IntPtr hInputRead = IntPtr.Zero, hInputWrite = IntPtr.Zero;
            IntPtr hOutputRead = IntPtr.Zero, hOutputWrite = IntPtr.Zero;
            IntPtr hPC = IntPtr.Zero;
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

            try
            {
                // 创建管道
                if (!CreatePipe(out hInputRead, out hInputWrite, IntPtr.Zero, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (input) failed");
                }
                if (!CreatePipe(out hOutputRead, out hOutputWrite, IntPtr.Zero, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe (output) failed");
                }

                // 设置管道句柄不继承（我们手动指定要继承的）
                SetHandleInformation(hInputWrite, HANDLE_FLAG_INHERIT, 0);
                SetHandleInformation(hOutputRead, HANDLE_FLAG_INHERIT, 0);

                // 创建伪终端
                COORD size = new COORD(120, 30);
                int hr = CreatePseudoConsole(size, hInputRead, hOutputWrite, 0, out hPC);
                if (hr != 0)
                {
                    throw new Exception($"CreatePseudoConsole failed, hr=0x{hr:X8}");
                }

                // 关闭我们这边不需要的管道端点
                CloseHandle(hInputRead);
                hInputRead = IntPtr.Zero;
                CloseHandle(hOutputWrite);
                hOutputWrite = IntPtr.Zero;

                // 准备启动信息
                STARTUPINFOEX si = new STARTUPINFOEX();
                si.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEX>();

                // 初始化属性列表
                IntPtr attrListSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);
                si.lpAttributeList = Marshal.AllocHGlobal(attrListSize);
                if (!InitializeProcThreadAttributeList(si.lpAttributeList, 1, 0, ref attrListSize))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed");
                }

                // 添加伪终端属性
                if (!UpdateProcThreadAttribute(
                    si.lpAttributeList,
                    0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPC,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed");
                }

                // 创建进程
                StringBuilder cmdLine = new StringBuilder($"\"{fileName}\" {args}");
                bool success = CreateProcess(
                    null,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi);

                if (!success)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
                }

                // 关闭本地线程句柄（我们不需要它）
                CloseHandle(pi.hThread);
                pi.hThread = IntPtr.Zero;

                // 清理属性列表
                DeleteProcThreadAttributeList(si.lpAttributeList);
                Marshal.FreeHGlobal(si.lpAttributeList);
                si.lpAttributeList = IntPtr.Zero;

                // 启动异步读取输出的任务
                CancellationTokenSource cts = new CancellationTokenSource();
                Task outputTask = Task.Run(() => ReadOutputAsync(hOutputRead, cts.Token));

                // 等待进程退出
                uint waitResult = WaitForSingleObject(pi.hProcess, INFINITE);

                // 取消读取任务
                cts.Cancel();
                try
                {
                    outputTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch { }

                // 获取退出码
                uint exitCode = 0;
                GetExitCodeProcess(pi.hProcess, out exitCode);

                return (int)exitCode;
            }
            finally
            {
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                if (hPC != IntPtr.Zero) ClosePseudoConsole(hPC);
                if (hInputRead != IntPtr.Zero) CloseHandle(hInputRead);
                if (hInputWrite != IntPtr.Zero) CloseHandle(hInputWrite);
                if (hOutputRead != IntPtr.Zero) CloseHandle(hOutputRead);
                if (hOutputWrite != IntPtr.Zero) CloseHandle(hOutputWrite);
            }
        }

        private static string StripAnsiEscapeCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // 使用正则表达式移除 ANSI 转义序列
            // 匹配 ESC [ ... m (SGR - Select Graphic Rendition)
            // 匹配 ESC [ ... h (Set Mode)
            // 匹配 ESC [ ... l (Reset Mode)
            // 匹配 ESC ] ... \x07 (OSC - Operating System Command)
            // 匹配 ESC [ ... H (Cursor Position)
            // 匹配 ESC [ ... J (Erase Display)
            // 匹配 ESC [ ? ... h/l (Private Mode)
            return Regex.Replace(input,
                @"\x1B\[[0-9;?]*[a-zA-Z]|\x1B\][^\x07]*\x07|\x1B\[[0-9;?]*[hl]",
                string.Empty);
        }
        
        private static string _latestResult = string.Empty;
        
        // 存储输出结果，用于判断升级是否成功
        private static readonly ConcurrentQueue<string> ResultQueue = new ConcurrentQueue<string>();

        private static void ReadOutputAsync(IntPtr hOutputRead, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            uint bytesRead;

            while (!cancellationToken.IsCancellationRequested)
            {
                // 检查是否有数据可读
                uint bytesAvail = 0;
                uint bytesLeft = 0;
                if (!PeekNamedPipe(hOutputRead, null, 0, out _, out bytesAvail, out bytesLeft))
                {
                    break;
                }

                if (bytesAvail > 0)
                {
                    if (ReadFile(hOutputRead, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero))
                    {
                        if (bytesRead > 0)
                        {
                            string output = Encoding.ASCII.GetString(buffer, 0, (int)bytesRead);
                            string clean = Regex.Replace(StripAnsiEscapeCodes(output), @"(\r\n){2,}", "\r\n");
                            string latestResult = Volatile.Read(ref _latestResult);
                            if (latestResult.EndsWith("\r\n") && clean.StartsWith("\r\n"))
                            {
                                clean = clean.Substring(2);
                            }
                            Interlocked.Exchange(ref _latestResult, clean);
                            Console.Write(clean);
                            ResultQueue.Enqueue(clean);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    // 没有数据时短暂休眠，避免CPU占用过高
                    Thread.Sleep(10);
                }
            }
        }
        
        /// <summary>
        /// 执行固件升级
        /// </summary>
        /// <param name="firmwarePath">固件路径</param>
        /// <returns>升级是否成功</returns>
        public static bool UpgradeFirmware(string firmwarePath)
        {
            int exitCode = RunProcessWithConPTY("upgrade_tool.exe", $"uf \"{firmwarePath}\"");
            // 判断升级是否成功
            // 成功条件：包含"Upgrade firmware ok"
            bool success = string.Join("", ResultQueue).Contains("Upgrade firmware ok");
            // 循环出队直到空
            while (ResultQueue.TryDequeue(out _))
            {
                // 每次取出一个元素并丢弃
            }

            return exitCode == 0 && success;
        }
    }
}