using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RKUpgradeTest
{
    public class ConPTYHelper
    {
        // ------------------- Win32 API -------------------
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle, out IntPtr lpTargetHandle,
            uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
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
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            uint dwAttributeCount,
            uint dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PeekNamedPipe(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize,
            out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);

        private const uint HANDLE_FLAG_INHERIT = 0x00000001;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint INFINITE = 0xFFFFFFFF;
        private const uint WAIT_OBJECT_0 = 0;

        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
            public COORD(short x, short y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
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
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        // 事件定义
        public event Action<string> OutputReceived;
        public event Action<int> ProcessExited;

        // 存储输出结果
        private string _latestResult = string.Empty;

        /// <summary>
        /// 运行进程并使用 ConPTY 捕获输出
        /// </summary>
        /// <param name="fileName">可执行文件路径</param>
        /// <param name="args">命令行参数</param>
        /// <returns>进程退出码</returns>
        public int RunProcessWithConPTY(string fileName, string args)
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

                // 触发进程退出事件
                ProcessExited?.Invoke((int)exitCode);

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

        private void ReadOutputAsync(IntPtr hOutputRead, CancellationToken cancellationToken)
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
                            
                            // 触发输出接收事件
                            OutputReceived?.Invoke(clean);
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

        private string StripAnsiEscapeCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // 使用正则表达式移除 ANSI 转义序列
            return Regex.Replace(input,
                @"\x1B\[[0-9;?]*[a-zA-Z]|\x1B\][^\x07]*\x07|\x1B\[[0-9;?]*[hl]",
                string.Empty);
        }
    }
}