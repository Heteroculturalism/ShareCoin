$origDir = $pwd

& "$PSScriptRoot\createDevPackages.ps1"

cd $PSScriptRoot\devops\sharecash-dev-vs2019community
choco install -y sharecash-dev-vs2019community -s "." --pre

cd $origDir