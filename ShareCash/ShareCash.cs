using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace ShareCash
{
    public static class ShareCash
    {
        public const int DiskCapacityCheckInterval = 1 * 60 * 1000;

        private const string AppDirectoryName = "ShareCash";
        private const long TargetPoolCapacity = 10L * 1024 * 1024 * 1024 * 1024 * 1024;
        private const long AverageIndividualCapacity = 60L * 1024 * 1024 * 1024;
        private const int NonceSize = 262144;
        private const int PlotFileNameNonceIndex = 1;
        private const int PlotFileNameNumberOfNoncesIndex = 2;

        private static ILogger _logger;

        public static void SetLogger(ILogger log)
        {
            _logger = log;
        }

        public static async Task RunAsync(CancellationToken stopMiningDrivesToken)
        {
            var fixedDrives = DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed).ToArray();
            var moreSpaceForPlottingEvent = new MoreSpaceForPlottingEvent(fixedDrives, stopMiningDrivesToken);
            var insufficientSpaceForMiningEvent = new InsufficientSpaceForMiningEvent(fixedDrives, stopMiningDrivesToken);

            try
            {
                var initialDrivePlottings = fixedDrives.Select(fixedDrive => PlotDiskAsync(fixedDrive, insufficientSpaceForMiningEvent, stopMiningDrivesToken)).ToList();
                var completedDrivePlottings = new List<DriveInfo>();

                _logger.LogInformation("Plotting drives...");

                await Task.WhenAll(initialDrivePlottings);

                _logger.LogInformation("Done plotting drives");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error running ShareCash");
            }
            finally
            {
                _ = MonitoredMineAsync(fixedDrives, moreSpaceForPlottingEvent, insufficientSpaceForMiningEvent, stopMiningDrivesToken);
            }
        }

        private static async Task<DriveInfo> PlotDiskAsync(DriveInfo disk, InsufficientSpaceForMiningEvent insufficientSpaceForMiningEvent, CancellationToken stopPlottingDisk)
        {
            using var cancelDueToLowDiskSpace = new CancellationTokenSource();
            using var cancelPlotting = CancellationTokenSource.CreateLinkedTokenSource(cancelDueToLowDiskSpace.Token, stopPlottingDisk);

            insufficientSpaceForMiningEvent.DiskSpaceTooLowForMining += OnDiskSpaceTooLowForMining;

            void OnDiskSpaceTooLowForMining(object sender, EventArgs<DriveInfo> e)
            {
                if(e.Value == disk)
                {
                    insufficientSpaceForMiningEvent.DiskSpaceTooLowForMining -= OnDiskSpaceTooLowForMining;

                    // stop plotting
                    cancelDueToLowDiskSpace.Cancel();

                    // delete plot files until above minimum free space limit
                    DeletePlotFilesUntilFreeSpaceAboveMinimum(disk);
                }
            }

            // plot big files
            await PlotFilesOfSizeAsync(disk, GetBigPlotSize(disk), cancelPlotting.Token);

            // plot small files
            await PlotFilesOfSizeAsync(disk, GetSmallPlotSize(disk), cancelPlotting.Token);

            return disk;
        }

        private static long GetBigPlotSize(DriveInfo disk) => disk.TotalSize / 10;

        public static long GetSmallPlotSize(DriveInfo disk) => disk.TotalSize / 100;

        public static long GetMinFreeDiskSpace(DriveInfo disk) => disk.TotalSize * 15/100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="diskToPlot"></param>
        /// <param name="plotFileSize"></param>
        /// <param name="stopPlottingToken"></param>
        private static async Task PlotFilesOfSizeAsync(DriveInfo diskToPlot, long plotFileSize, CancellationToken stopPlottingToken)
        {
            var expectedFreeDiskSpace = diskToPlot.AvailableFreeSpace - plotFileSize;

            // create plot directory if it does not exist
            var plotDirectory = GetPlotDirectory(diskToPlot);
            if (!Directory.Exists(plotDirectory))
            {
                Directory.CreateDirectory(plotDirectory);
            }

            // re-plot existing plot files, in case any are corrupt or incomplete
            foreach (var existingPlotFile in Directory.GetFiles(plotDirectory))
            {
                var numberOfNonces = long.Parse(existingPlotFile.Split('_')[PlotFileNameNumberOfNoncesIndex]);
                var startingNonce = long.Parse(existingPlotFile.Split('_')[PlotFileNameNonceIndex]);

                await PlotFileAsync(diskToPlot, numberOfNonces, startingNonce, stopPlottingToken);
            }

            // plot until we exceed the minimum disk space limit
            while (expectedFreeDiskSpace > GetMinFreeDiskSpace(diskToPlot))
            {
                var nextStartingNonce = GetLastCompletedNonce(diskToPlot) + 1 ?? GetRandomStartingNonce();

                await PlotFileAsync(diskToPlot, plotFileSize / NonceSize, nextStartingNonce, stopPlottingToken);

                expectedFreeDiskSpace = diskToPlot.AvailableFreeSpace - plotFileSize;
            }
        }

        private static async Task MonitoredMineAsync(DriveInfo[] disksToMine, MoreSpaceForPlottingEvent moreSpaceForPlottingEvent, InsufficientSpaceForMiningEvent insufficientSpaceForMiningEvent, CancellationToken stoppingToken)
        {
            using var stopMiningDueToMoreSpaceForPlotting = new CancellationTokenSource();
            using var stopMiningDueToLowDiskSpace = new CancellationTokenSource();
            using var stopMining = CancellationTokenSource.CreateLinkedTokenSource(stopMiningDueToMoreSpaceForPlotting.Token, stopMiningDueToLowDiskSpace.Token, stoppingToken);

            moreSpaceForPlottingEvent.DiskGainedPlottingSpace += OnMoreSpaceForPlotting;
            insufficientSpaceForMiningEvent.DiskSpaceTooLowForMining += OnDiskSpaceTooLowForMining;

            void OnMoreSpaceForPlotting(object sender, EventArgs<DriveInfo> e)
            {
                moreSpaceForPlottingEvent.DiskGainedPlottingSpace -= OnMoreSpaceForPlotting;

                // stop mining
                stopMiningDueToMoreSpaceForPlotting.Cancel();

                // plot extra space & re-mine
                _ = PlotDiskAsync(e.Value, insufficientSpaceForMiningEvent, stoppingToken)
                    .ContinueWith(plotTask =>
                    {
                        var newDisksToMine = plotTask.Status == TaskStatus.RanToCompletion ? disksToMine : disksToMine.Except(new[] { e.Value }).ToArray();
                        
                        _ = MonitoredMineAsync(newDisksToMine, moreSpaceForPlottingEvent, insufficientSpaceForMiningEvent, stoppingToken);
                    });
            }

            void OnDiskSpaceTooLowForMining(object sender, EventArgs<DriveInfo> e)
            {
                insufficientSpaceForMiningEvent.DiskSpaceTooLowForMining -= OnDiskSpaceTooLowForMining;

                // Stop mining
                stopMiningDueToLowDiskSpace.Cancel();

                // delete plots until under free space minimum
                DeletePlotFilesUntilFreeSpaceAboveMinimum(e.Value);

                // resume mining
                _ = MonitoredMineAsync(disksToMine.ToList().Except(new[] { e.Value }).ToArray(), moreSpaceForPlottingEvent, insufficientSpaceForMiningEvent, stoppingToken);
            }

            if (disksToMine.Any())
            {
                try
                {
                    await MineAsync(disksToMine, stopMining.Token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while mining");
                }
            }
        }

        private static async Task MineAsync(DriveInfo[] drivesToMine, CancellationToken cancelMiningToken)
        {
            var baseConfig = Resource1.config_base;
            var yaml = new YamlStream();
            using var reader = new StringReader(baseConfig);
            var finalConfigPath = Path.GetTempFileName();
            var serializer = new Serializer();
            var plotPaths = drivesToMine.Select(GetPlotDirectory);

            yaml.Load(reader);

            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            var plotDirectories = (YamlSequenceNode)mapping.Children[new YamlScalarNode("plot_dirs")];

            plotDirectories.Children.Clear();
            foreach (var plotPath in plotPaths)
            {
                plotDirectories.Add(new YamlScalarNode(plotPath) { Style = ScalarStyle.SingleQuoted });
            }

            await using (var writer = new StreamWriter(finalConfigPath))
            {
                serializer.Serialize(writer, yaml.Documents[0].RootNode);
            }

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\chocolatey\lib\scavenger\scavenger.exe",
                    Arguments = $"--config {finalConfigPath}",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogInformation($"Starting mine with config '{finalConfigPath}'");

            _logger.LogDebug($"********************starting mining - {cancelMiningToken.GetHashCode()}");

            if (proc.Start())
            {
                _logger.LogDebug($"********************cancelled: {cancelMiningToken.IsCancellationRequested}, hash: {cancelMiningToken.GetHashCode()}");

                await using var _ = cancelMiningToken.Register(() => proc.Kill());

                var logDelayTask = Task.Delay(10 * 1000);

                string standardOutput;
                while ((standardOutput = await proc.StandardOutput.ReadLineAsync()) != null)
                {
                    if (logDelayTask.IsCompleted)
                    {
                        _logger.LogInformation(standardOutput);
                        logDelayTask = Task.Delay(10 * 1000);
                    }
                }

                await Task.Run(() => proc.WaitForExit(), cancelMiningToken);
            }
        }

        private static string GetPlotDirectory(DriveInfo diskInfo) => $@"{diskInfo.Name}\{AppDirectoryName}\plots";

        private static async Task PlotFileAsync(DriveInfo diskInfo, long numberOfNonces, long startingNonce, CancellationToken stopPlottingToken)
        {
            var memoryGb = Math.Max(Process.GetProcesses().Max(process => process.PeakWorkingSet64) / 1024 / 1024 / 1024, 1);

            var args = $@"-id 12209047155150467438 -sn {startingNonce} -n {numberOfNonces} -t {Math.Max(Environment.ProcessorCount - 1, 1)} -path {GetPlotDirectory(diskInfo)} -mem {memoryGb}G";

            _logger.LogInformation($"Plotting with arguments: {args}");

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\chocolatey\lib\xplotter\xplotter_avx.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            if (proc.Start())
            {
                stopPlottingToken.Register(() => { try { proc.Kill(); } catch (Exception) { } });

                var logDelayTask = Task.Delay(10 * 1000);

                string standardOutput;
                while ((standardOutput = await proc.StandardOutput.ReadLineAsync()) != null)
                {
                    if(logDelayTask.IsCompleted)
                    {
                        _logger.LogInformation($"SN: {startingNonce}, {standardOutput}");
                        logDelayTask = Task.Delay(10 * 1000);
                    }
                }

                await Task.Run(() => proc.WaitForExit(), stopPlottingToken);
            }
        }

        private static long GetRandomStartingNonce() => new Random().Next(0, (int) (TargetPoolCapacity / AverageIndividualCapacity));

        private static long? GetLastCompletedNonce(DriveInfo diskInfo)
        {
            long? lastCompletedNonce = null;

            var newestPlotFile = GetNewestPlotFile(diskInfo);
            
            if(newestPlotFile != null)
            {
                var newestPlotFileComponents = newestPlotFile.Split('_');
                lastCompletedNonce = long.Parse(newestPlotFileComponents[PlotFileNameNonceIndex]) + long.Parse(newestPlotFileComponents[PlotFileNameNumberOfNoncesIndex]) - 1;
            }

            return lastCompletedNonce;
        }
        
        private static string GetNewestPlotFile(DriveInfo diskInfo)
        {
            string newestPlotFile = null;
            
            var plotFiles = Directory.GetFiles(GetPlotDirectory(diskInfo));

            if (plotFiles.Any())
            {
                var largestStartingNonce = plotFiles.Max(plotFile => long.Parse(plotFile.Split('_')[PlotFileNameNonceIndex])).ToString();
                newestPlotFile = plotFiles.FirstOrDefault(plotFile => plotFile.Contains($"_{largestStartingNonce}_"));
            }

            return newestPlotFile;
        }

        private static void DeletePlotFilesUntilFreeSpaceAboveMinimum(DriveInfo diskInfo)
        {
            string newestPlotFile;

            while (diskInfo.AvailableFreeSpace < GetMinFreeDiskSpace(diskInfo) && (newestPlotFile = GetNewestPlotFile(diskInfo)) != null)
            {
                File.Delete(newestPlotFile);
            }
        }
    }
}
