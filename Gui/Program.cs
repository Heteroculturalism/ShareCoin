using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gui
{
    static class Program
    {
        internal static readonly string LogLocation = $@"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\GuiLog.txt";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(LogLocation)
                .WriteTo.Console()
                .CreateLogger();

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            while (true)
            { 
                try
                {
                    Application.Run(new Form1());
                }
                catch (Exception e) 
                {
                    Log.Logger.Error(e, "failed to run GUI monitor");
                    Task.Delay(ShareCash.Core.ShareCash.UserInteractionCheckInterval).Wait();
                }
            }
        }
    }
}
