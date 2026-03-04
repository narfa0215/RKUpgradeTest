using System;
using System.Windows.Forms;

namespace RKUpgradeTest
{
    public partial class FirmwareUpgradeProgress : Form
    {
        public FirmwareUpgradeProgress()
        {
            InitializeComponent();
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
}