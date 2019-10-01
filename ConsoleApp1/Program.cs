using chocolatey.infrastructure.results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            chocolatey.GetChocolatey x = new chocolatey.GetChocolatey();

            var listCommand = new chocolatey.infrastructure.app.configuration.ListCommandConfiguration();
            listCommand.ByIdOnly = true;
            listCommand.Exact = true;
           
            x.Set(c => { c.CommandName = "list";
                c.ListCommand = listCommand;
                c.Input = "git";
                c.Verbose = true;
            });

            x.Run();

            var foo = x.ListCount();
            var results = x.List<PackageResult>();
            var package = results.First();
            var downloadCount = package.Package.DownloadCount;
            var versionDownload = package.Package.VersionDownloadCount;
            
            Console.ReadLine();
        }
    }
}
