using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace RKUpgradeTest
{
    internal class Program
    {
        // 进度窗口类
        public class ProgressForm : Form
        {
            private ProgressBar progressBar;
            private Label statusLabel;
            private System.Windows.Forms.Timer closeTimer;
            
            public ProgressForm()
            {
                InitializeComponent();
            }
            
            private void InitializeComponent()
            {
                this.progressBar = new ProgressBar();
                this.statusLabel = new Label();
                this.closeTimer = new System.Windows.Forms.Timer();
                this.SuspendLayout();
                
                // 设置窗口属性
                this.Text = "固件升级进度";
                this.Size = new Size(400, 150);
                this.StartPosition = FormStartPosition.CenterScreen;
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                
                // 进度条
                this.progressBar.Location = new Point(20, 60);
                this.progressBar.Size = new Size(350, 20);
                this.progressBar.Minimum = 0;
                this.progressBar.Maximum = 100;
                this.progressBar.Value = 0;
                
                // 状态标签
                this.statusLabel.Location = new Point(20, 20);
                this.statusLabel.Size = new Size(350, 30);
                this.statusLabel.Text = "准备固件升级...";
                this.statusLabel.TextAlign = ContentAlignment.MiddleCenter;
                
                // 关闭定时器
                this.closeTimer.Interval = 3000; // 3秒
                this.closeTimer.Tick += new EventHandler(closeTimer_Tick);
                
                // 添加控件
                this.Controls.Add(this.progressBar);
                this.Controls.Add(this.statusLabel);
                this.ResumeLayout(false);
            }
            
            public void UpdateProgress(int progress, string status)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action<int, string>(UpdateProgress), progress, status);
                }
                else
                {
                    this.progressBar.Value = progress;
                    this.statusLabel.Text = status;
                }
            }
            
            public void StartCloseTimer()
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(StartCloseTimer));
                }
                else
                {
                    this.closeTimer.Start();
                }
            }
            
            private void closeTimer_Tick(object sender, EventArgs e)
            {
                this.closeTimer.Stop();
                this.Close();
            }
        }

        public static void Main(string[] args)
        {
            DeviceManager deviceManager = new DeviceManager();
            
            // 初始化设备管理
            bool initOk = deviceManager.Initialize();
            if (!initOk)
            {
                Console.WriteLine("RK_Initialize failed!");
                return;
            }

            Console.WriteLine("RK_Initialize succeeded!");
            
            bool uninitializeOk;
            try
            {
                // 扫描设备
                var devices = deviceManager.ScanDevices();

                if (devices.Length <= 0)
                {
                    Console.WriteLine("No devices found.");
                    return;
                }

                // 检查是否有设备需要重启到loader模式
                bool needReboot = deviceManager.NeedRebootToLoader(devices);
                
                foreach (var dev in devices)
                {
                    Console.WriteLine(
                        $"VID={dev.usVid:X4}, PID={dev.usPid:X4}, Link={dev.szLinkName}, USBType=0x{dev.emUsbType:X}");
                }
                
                // 如果需要重启到loader模式
                if (needReboot)
                {
                    Console.WriteLine("\nDetected device in ADB mode (USBType=0x8). Rebooting to loader mode...");
                    if (deviceManager.RebootToLoader())
                    {
                        Console.WriteLine("Reboot to loader mode succeeded. Waiting for device to reconnect...");
                        // 等待设备重新连接
                        deviceManager.WaitForDeviceReconnect();
                        
                        // 重新扫描设备
                        Console.WriteLine("Rescanning devices...");
                        devices = deviceManager.ScanDevices();
                        
                        if (devices.Length <= 0)
                        {
                            Console.WriteLine("No devices found after reboot.");
                            return;
                        }
                        
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

                // 读取旧 SN
                string readOldSn = deviceManager.ReadSN();
                if (readOldSn == null)
                {
                    Console.WriteLine("Failed to read old SN.");
                    return;
                }
                Console.WriteLine("Read old SN: " + readOldSn);

                // 写入新 SN
                string writeNewSn = "2504010029";
                bool writeNewSnOk = deviceManager.WriteSN(writeNewSn);
                if (!writeNewSnOk)
                {
                    Console.WriteLine("Write new SN failed.");
                    return;
                }
                Console.WriteLine("Write new SN: " + writeNewSn);

                // 读取新 SN
                string readNewSn = deviceManager.ReadSN();
                if (readNewSn == null)
                {
                    Console.WriteLine("Failed to read new SN.");
                    return;
                }
                Console.WriteLine("Read new SN: " + readNewSn);
            }
            finally
            {
                // 释放已初始化资源
                uninitializeOk = deviceManager.Uninitialize();
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
        /// 执行固件升级
        /// </summary>
        /// <param name="firmwarePath">固件路径</param>
        /// <returns>升级是否成功</returns>
        public static bool UpgradeFirmware(string firmwarePath)
        {
            // 创建进度窗口
            ProgressForm progressForm = new ProgressForm();
            Task progressFormTask = Task.Run(() => Application.Run(progressForm));
            
            // 创建升级管理器
            RKUpgradeManager upgradeManager = new RKUpgradeManager();
            
            // 订阅进度更新事件
            upgradeManager.ProgressUpdated += (progress, status) =>
            {
                progressForm.UpdateProgress(progress, status);
                
                // 当进度达到100%时，启动关闭定时器
                if (progress >= 100)
                {
                    progressForm.StartCloseTimer();
                }
            };
            
            // 订阅升级完成事件
            bool[] upgradeResult = { false };
            upgradeManager.UpgradeCompleted += (isSuccess, message) =>
            {
                upgradeResult[0] = isSuccess;
                progressForm.StartCloseTimer();
            };
            
            // 执行升级
            bool success = upgradeManager.UpgradeFirmware(firmwarePath);
            
            // 等待进度窗口关闭
            if (progressFormTask != null)
            {
                progressFormTask.Wait();
            }
            
            return success;
        }
    }
}