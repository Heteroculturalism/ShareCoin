$origDir = $pwd

cd $PSScriptRoot\Installer

dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true

cd $origDir