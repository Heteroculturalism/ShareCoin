$origDir = $pwd

# Package Dev Core
cd $PSScriptRoot\devops\sharecash-dev-core
choco pack
copy *.nupkg $PSScriptRoot\devops\chocoPackages

# Package VS2019 Community
cd $PSScriptRoot\devops\sharecash-dev-vs2019community
choco pack
copy *.nupkg $PSScriptRoot\devops\chocoPackages

cd $origDir