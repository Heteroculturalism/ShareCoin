$origDir = $pwd

& "$PSScriptRoot\createShareCashPackage.ps1"

choco install -y git virtualbox vagrant
choco upgrade -y git virtualbox vagrant

$chocoTestDir = "$PSScriptRoot\..\chocolatey-test-environment"
$repoUrl = "https://github.com/chocolatey-community/chocolatey-test-environment.git"

if ($(test-path $chocoTestDir -pathtype container) -eq $false)
{
	cd $PSScriptRoot\..

	# clone repo
	git clone $repoUrl
 
	cd chocolatey-test-environment
 
	vagrant plugin install sahara

	# bring up test VM
	vagrant up

	# create base line snapshot
	vagrant sandbox on
}

# copy over packages
copy $PSScriptRoot\devops\xplotter\*.nupkg $chocoTestDir\packages\
copy $PSScriptRoot\devops\scavenger\*.nupkg $chocoTestDir\packages\
copy $PSScriptRoot\devops\sharecash\*.nupkg $chocoTestDir\packages\
((Get-Content -path $chocoTestDir\Vagrantfile -Raw) -replace '#choco.exe install -fdvy INSERT_NAME  --allow-downgrade','choco.exe install -fdvy ShareCash  --allow-downgrade --pre') | Set-Content -Path $chocoTestDir\Vagrantfile

# test run of package install
cd $chocoTestDir
vagrant sandbox rollback
vagrant provision

cd $origDir