#chocolatey will stop service
#chocolatey will download zip
#chocolatey will extract zip to c:\sharecoin
#chocolatey will create service
#chocolatey will start service

$shareCoinService = get-service sharecoin -ErrorAction SilentlyContinue;
$shareCoinPackageName = "sharecoin_0.1.0.zip";
$currentDirectory = $(split-path -parent $MyInvocation.MyCommand.Path);

function WaitUntilServices($searchString, $status)
{
	write-output "Waiting for a service...."
	
    # Get all services where name matches $searchString and loop through each of them.
    foreach($service in (Get-Service -name $searchString))
    {
        # Wait for the service to reach the $status or a maximum of 30 seconds
        $service.WaitForStatus($status, '00:00:30')
    }
}

# download & install package
Install-ChocolateyZipPackage -packagename ShareCoin -unziplocation c:\sharecoin -Url 'https://github.com/CashShareCoin/ShareCoin/releases/download/v0.1.0/sharecoin_0.1.0.zip' -checksum '626bbcccfd53b854c52c7cb4ab74b1a071e7f691d1c97d98e1b37c5c748884a4' -checksumtype 'sha256'

# create service, if needed
if ($shareCoinService -eq $null)
{
	new-service -name ShareCoin -BinaryPathName "C:\sharecoin\Updater.exe"
	$newShareCoinService = get-service -name ShareCoin;

	write-output "before waiting for stopped status"
	$newShareCoinService.WaitForStatus("Stopped");
	write-output "after waiting for stopped status"

	write-output "before starting new service"
	#start-service -name ShareCoin
	#$newShareCoinService.Start();
	sc.exe start ShareCoin
	write-output "after starting new service"

	write-output "starting to wait for new service to run"
	#$newShareCoinService.WaitForStatus("Running", '00:00:30');
	write-output "current status is: " + $newShareCoinService.Status
}

# start service
#start-service -name ShareCoin
#WaitUntilServices "ShareCoin" "Running"
