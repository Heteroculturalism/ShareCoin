$shareCoinService = get-service sharecoin -ErrorAction SilentlyContinue;

# stop service
if ($shareCoinService -ne $null)
{
	stop-service -name sharecoin
	$shareCoinService.WaitForStatus('Stopped','00:00:10')
}
