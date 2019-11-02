$origDir = $pwd

# Package Dev Core
cd $PSScriptRoot\devops\sharecash-dev-core
choco pack

# Package VS2019 Community
cd $PSScriptRoot\devops\sharecash-dev-vs2019community
choco pack

cd $origDir