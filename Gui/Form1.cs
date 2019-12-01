using Gma.System.MouseKeyHook;
using ProtoBuf;
using Serilog;
using Serilog.Events;
using ShareCash.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gui
{
    public partial class Form1 : Form
    {
        private readonly FileStream _file;
        private readonly StreamWriter _writer;

        private IKeyboardMouseEvents _globalKeyboardEvents;

        public Form1()
        {
            InitializeComponent();
            this.Opacity = 0;
            this.ShowInTaskbar = false;

            _globalKeyboardEvents = Hook.GlobalEvents();

            _file = new FileStream(ShareCash.Core.ShareCash.UserInteractionSignalPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(_file);

            _file.Seek(0, SeekOrigin.End);

            this.Load += Form1_Load;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            this.Load -= Form1_Load;

            Log.Logger.Information("Waiting for ShareCash...");

            await this.WaitForShareCashAsync();

            Log.Logger.Information("ShareCash up.");

            _globalKeyboardEvents.KeyDown += this.OnKeyDown;

            _globalKeyboardEvents.MouseMove += this.OnMouseEvent;
            _globalKeyboardEvents.MouseClick += this.OnMouseEvent;
            _globalKeyboardEvents.MouseWheel += this.OnMouseEvent;
        }

        private async void OnKeyDown(object sender, KeyEventArgs e)
        {
            _globalKeyboardEvents.KeyDown -= this.OnKeyDown;

            await this.WriteOutputAsync("detected key down");

            await WaitUntilReadyForUserInteractionMonitoringAsync();

            _globalKeyboardEvents.KeyDown += this.OnKeyDown;
        }

        private async void OnMouseEvent(object sender, MouseEventArgs e)
        {
            _globalKeyboardEvents.MouseMove -= this.OnMouseEvent;
            _globalKeyboardEvents.MouseClick -= this.OnMouseEvent;
            _globalKeyboardEvents.MouseWheel -= this.OnMouseEvent;

            await this.WriteOutputAsync("detected mouse action");

            await WaitUntilReadyForUserInteractionMonitoringAsync();

            _globalKeyboardEvents.MouseMove += this.OnMouseEvent;
            _globalKeyboardEvents.MouseClick += this.OnMouseEvent;
            _globalKeyboardEvents.MouseWheel += this.OnMouseEvent;
        }

        private async Task WaitUntilReadyForUserInteractionMonitoringAsync()
        {
            await Task.Delay(ShareCash.Core.ShareCash.UserInteractionCheckInterval);

            await WaitForShareCashAsync();
        }

        private async Task WaitForShareCashAsync()
        {
            while (!Process.GetProcessesByName("ShareCash").Any())
            {
                await Task.Delay(ShareCash.Core.ShareCash.UserInteractionCheckInterval);
            }
        }

        private async Task WriteOutputAsync(string output)
        {
            Log.Information(output);

            await _writer.WriteAsync('a');
            await _writer.FlushAsync();
        }
    }
}
