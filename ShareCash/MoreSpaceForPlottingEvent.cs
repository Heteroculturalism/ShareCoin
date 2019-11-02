using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareCash
{
    internal static class EventExtentionMethods
    {
        public static void Fire<TPayload>(this EventHandler<EventArgs<TPayload>> @event, object firingObject, TPayload payload)
        {
            @event?.Invoke(firingObject, new EventArgs<TPayload>(payload));
        }
    }

    internal class EventArgs<TPayload> : EventArgs
    {
        public EventArgs(TPayload payload)
        {
            this.Value = payload;
        }

        public TPayload Value { get; }
    }

    internal class MoreSpaceForPlottingEvent
    {
        public event EventHandler<EventArgs<DriveInfo>> DiskGainedPlottingSpace;

        public MoreSpaceForPlottingEvent(DriveInfo[] disks, CancellationToken stoppingToken)
        {
            _ = Task.Run(async () => 
                {
                    // keep checking until free disk space goes above the size of a small plot file plus the minimum free disk space 
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        foreach (var disk in disks)
                        {
                            if (disk.AvailableFreeSpace - ShareCash.GetSmallPlotSize(disk) >= ShareCash.GetMinFreeDiskSpace(disk))
                            {
                                this.DiskGainedPlottingSpace.Fire(this, disk);
                            }
                        }

                        await Task.Delay(ShareCash.DiskCapacityCheckInterval, stoppingToken);
                    }
                });
        }
    }

    internal class InsufficientSpaceForMiningEvent
    {
        public event EventHandler<EventArgs<DriveInfo>> DiskSpaceTooLowForMining;

        public InsufficientSpaceForMiningEvent(DriveInfo[] disks, CancellationToken stoppingToken)
        {
            _ = Task.Run(async () =>
            {
                // keep checking until free disk space goes above the size of a small plot file plus the minimum free disk space 
                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (var disk in disks)
                    {
                        if (disk.AvailableFreeSpace < ShareCash.GetMinFreeDiskSpace(disk))
                        {
                            this.DiskSpaceTooLowForMining.Fire(this, disk);
                        }
                    }

                    await Task.Delay(ShareCash.DiskCapacityCheckInterval, stoppingToken);
                }
            });
        }
    }
}
