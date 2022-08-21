dotnet build

$dir = Split-Path -Path (Get-Location) -Leaf
# Edit $NeosDir to be the path to the neos directory on your own system //  "-DontAutoOpenCloudHome",
$NeosDir = "C:\Program Files (x86)\Steam\steamapps\common\NeosVR"
$NeosExe = "$NeosDir\Neos.exe"
$AssemblyLocation = "$(Get-Location)\WebsocketServer\bin\Debug\$dir.dll"
$Libraries = "$NeosDir\Libraries\"

Copy-Item -Force -Path $AssemblyLocation -Destination $Libraries

$LogJob = Start-Job {Start-Sleep -Seconds 8
    Get-Content "$NeosDir\Logs$(Get-ChildItem -Path C:\Program Files (x86)\Steam\steamapps\common\NeosVR\Logs | Sort-Object LastWriteTime | Select-Object -last 1)" -Wait
}

$NeosProc = Start-Process -FilePath $NeosExe -WorkingDirectory $NeosDir -ArgumentList "-Screen", "-DontAutoOpenCloudHome", "-SkipIntroTutorial", "-LoadAssembly `"$NeosDir\Libraries\NeosModLoader.dll`"" -passthru

while(!$NeosProc.HasExited) {
    Start-Sleep -Seconds 1
    Receive-Job $LogJob.Id
}

Stop-Job $LogJob.Id