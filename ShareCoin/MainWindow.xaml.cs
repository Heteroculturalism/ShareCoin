using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace ShareCoin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            // ensure running as admin

            // install Chocolatey
            this.textBox.Text = "Installing Chocolatey...";
            
            var scriptDirectory = Path.GetTempPath();
            var installScript = Path.Combine(scriptDirectory, "installChocolatey.bat");
            var psScript = Path.Combine(scriptDirectory, "install.ps1");
            var script = Resource1.installChocolatey.Replace(@".\install.ps1", $@"""{psScript}""");
            File.WriteAllText(installScript, script);
            File.WriteAllText(psScript, ShareCoin.Resource1.install);

            var chocolateyInstallProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {installScript}",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                }
            };

            chocolateyInstallProc.Start();

            var chocoInstallOutput = await chocolateyInstallProc.StandardOutput.ReadToEndAsync();
            this.textBox.Text = $"{this.textBox.Text} {Environment.NewLine} {chocoInstallOutput}";

            await Task.Run(() => chocolateyInstallProc.WaitForExit());

            this.textBox.Text = $"{this.textBox.Text} {Environment.NewLine} Installed Chocolatey. {Environment.NewLine} Installing ShareCoin...";

            var shareCoinInstallProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\chocolatey\choco.exe",
                    Arguments = "install -y ShareCoin",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            shareCoinInstallProc.Start();

            var shareCoinInstallOutput = await shareCoinInstallProc.StandardOutput.ReadToEndAsync();
            this.textBox.Text = $"{this.textBox.Text} {Environment.NewLine} {shareCoinInstallOutput}";
            await Task.Run(() => shareCoinInstallProc.WaitForExit());

            this.textBox.Text = $"{this.textBox.Text} {Environment.NewLine} Installed ShareCoin.";

            Application.Current.Shutdown();
        }
    }
}
