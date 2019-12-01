$origDir = $pwd

# Package & install XPlotter
cd $PSScriptRoot\devops\xplotter
choco pack
choco install -y xplotter -s "."

# Package & install Scavenger
cd $PSScriptRoot\devops\scavenger
choco pack
choco install -y scavenger -s "."

# Package & install .NET Core Desktop
cd $PSScriptRoot\devops\dotnetcore-desktop-runtime.install
choco pack
choco install -y dotnetcore-desktop-runtime.install -s ".;https://chocolatey.org/api/v2"

cd $origDir