# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p3ppc.cutsceneCaller/*" -Force -Recurse
dotnet publish "./p3ppc.cutsceneCaller.csproj" -c Release -o "$env:RELOADEDIIMODS/p3ppc.cutsceneCaller" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location