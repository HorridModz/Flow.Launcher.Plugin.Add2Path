:: Hacked (skidded?) together with ChatGPT

@echo off
setlocal

:: Define the path to the Flow.Launcher executable
set "exePath=%LocalAppData%\FlowLauncher\Flow.Launcher.exe"

:: Check if the executable exists
if exist "%exePath%" (
    echo Flow.Launcher executable found at %exePath%
) else (
    echo Flow.Launcher executable not found at %exePath%
    set /p "exePath=Please enter the path to the Flow.Launcher executable (the file itself, not the folder): "
)


    :: Check if the file at the provided path exists
    if not exist "%exePath%" (
        echo file not found at this path
        pause
        goto :end
    )
)

:: Display the path to the user
echo Using executable: %exePath%

:: Use PowerShell to set the executable to always run as administrator
powershell -command "Set-ItemProperty -Path 'Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers' -Name '%exePath%' -Value 'RUNASADMIN'"

echo The Flow.Launcher executable has been set to always run as administrator.
pause
:end