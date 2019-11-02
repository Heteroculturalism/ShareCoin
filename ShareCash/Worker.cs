using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using chocolatey.infrastructure.results;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGet;

namespace ShareCash
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ShareCash.SetLogger(_logger);

            _ = MonitorForAppUpdateAsync(stoppingToken);

           return ShareCash.RunAsync(stoppingToken);
        }

        private async Task MonitorForAppUpdateAsync(CancellationToken stoppingToken)
        {
            // check for new version
            while (!stoppingToken.IsCancellationRequested)
            {
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\chocolatey\choco.exe",
                        Arguments = "upgrade -y ShareCash",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                if(proc.Start())
                {
                    string standardOutput;
                    while ((standardOutput = await proc.StandardOutput.ReadLineAsync()) != null)
                    {
                        _logger.LogInformation(standardOutput);
                    }

                    await Task.Run(() => proc.Start(), stoppingToken);
                }

                await Task.Delay(60 * 60 * 1000, stoppingToken);
            }
        }
    }
}
