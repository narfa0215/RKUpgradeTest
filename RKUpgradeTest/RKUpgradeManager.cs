using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace RKUpgradeTest
{
    public class RKUpgradeManager
    {
        // 事件定义
        public event Action<int, string> ProgressUpdated;
        public event Action<bool, string> UpgradeCompleted;

        // 存储输出结果，用于判断升级是否成功
        private readonly ConcurrentQueue<string> _resultQueue = new ConcurrentQueue<string>();
        
        // 升级状态信息
        private string _currentStatus = string.Empty;
        private int _currentProgress = 0;
        private bool _upgradeSuccess = false;

        private readonly ConPTYHelper _conPTYHelper;

        public RKUpgradeManager()
        {
            _conPTYHelper = new ConPTYHelper();
            _conPTYHelper.OutputReceived += OnOutputReceived;
            _conPTYHelper.ProcessExited += OnProcessExited;
        }

        /// <summary>
        /// 执行固件升级
        /// </summary>
        /// <param name="firmwarePath">固件路径</param>
        /// <returns>升级是否成功</returns>
        public bool UpgradeFirmware(string firmwarePath)
        {
            // 初始化进度状态
            _currentStatus = "准备固件升级...";
            _currentProgress = 0;
            _upgradeSuccess = false;
            
            // 清空结果队列
            while (_resultQueue.TryDequeue(out _)) { }
            
            // 触发初始进度更新
            ProgressUpdated?.Invoke(_currentProgress, _currentStatus);
            
            // 执行升级命令
            int exitCode = _conPTYHelper.RunProcessWithConPTY("upgrade_tool.exe", $"uf \"{firmwarePath}\"");
            
            // 判断升级是否成功
            bool success = string.Join("", _resultQueue).Contains("Upgrade firmware ok");
            
            return exitCode == 0 && success;
        }

        private void OnOutputReceived(string output)
        {
            Console.Write(output);
            _resultQueue.Enqueue(output);
            
            // 解析升级状态和进度
            ParseUpgradeInfo(output);
        }

        private void OnProcessExited(int exitCode)
        {
            // 判断升级是否成功
            _upgradeSuccess = string.Join("", _resultQueue).Contains("Upgrade firmware ok") && exitCode == 0;
            
            // 触发升级完成事件
            string message = _upgradeSuccess ? "固件升级成功完成！" : "固件升级失败！";
            UpgradeCompleted?.Invoke(_upgradeSuccess, message);
        }

        private void ParseUpgradeInfo(string output)
        {
            // 解析状态信息
            if (output.Contains("Starting firmware upgrade"))
            {
                _currentStatus = "开始固件升级...";
                _currentProgress = 0;
            }
            else if (output.Contains("Loading firmware"))
            {
                _currentStatus = "加载固件中...";
            }
            else if (output.Contains("Support Type"))
            {
                _currentStatus = "固件信息已加载";
            }
            else if (output.Contains("Loader ver"))
            {
                _currentStatus = "加载器信息已加载";
            }
            else if (output.Contains("Start to upgrade firmware"))
            {
                _currentStatus = "开始固件升级过程";
            }
            else if (output.Contains("Test Device Start"))
            {
                _currentStatus = "正在测试设备...";
            }
            else if (output.Contains("Test Device Success"))
            {
                _currentStatus = "设备测试通过";
                _currentProgress = 10;
            }
            else if (output.Contains("Check Chip Start"))
            {
                _currentStatus = "正在检查芯片...";
            }
            else if (output.Contains("Check Chip Success"))
            {
                _currentStatus = "芯片检查通过";
                _currentProgress = 20;
            }
            else if (output.Contains("Get FlashInfo Start"))
            {
                _currentStatus = "正在获取闪存信息...";
            }
            else if (output.Contains("Get FlashInfo Success"))
            {
                _currentStatus = "已获取闪存信息";
                _currentProgress = 30;
            }
            else if (output.Contains("Prepare IDB Start"))
            {
                _currentStatus = "正在准备IDB...";
            }
            else if (output.Contains("Prepare IDB Success"))
            {
                _currentStatus = "IDB准备完成";
                _currentProgress = 40;
            }
            else if (output.Contains("Download IDB Start"))
            {
                _currentStatus = "正在下载IDB...";
            }
            else if (output.Contains("Download IDB Success"))
            {
                _currentStatus = "IDB下载完成";
                _currentProgress = 50;
            }
            else if (output.Contains("Download Firmware Start"))
            {
                _currentStatus = "正在下载固件...";
            }
            else if (output.Contains("Download Image..."))
            {
                // 解析下载进度
                Match match = Regex.Match(output, @"Download Image\.\.\. \((\d+)%\)");
                if (match.Success)
                {
                    int progress = int.Parse(match.Groups[1].Value);
                    _currentProgress = 50 + (progress / 2); // 50% 到 100%
                    _currentStatus = $"正在下载固件... {progress}%";
                }
            }
            else if (output.Contains("Download Firmware Success"))
            {
                _currentStatus = "固件下载成功";
                _currentProgress = 100;
            }
            else if (output.Contains("Upgrade firmware ok"))
            {
                _currentStatus = "固件升级成功完成！";
                _currentProgress = 100;
            }
            
            // 触发进度更新事件
            ProgressUpdated?.Invoke(_currentProgress, _currentStatus);
        }
    }
}