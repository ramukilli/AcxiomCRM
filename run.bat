@echo off
set "PATH=C:\Users\ramuk\AppData\Local\Microsoft\dotnet;%PATH%"
echo ========================================================
echo  Starting AcxiomCRM Enterprise on http://localhost:5164
echo ========================================================
dotnet run --launch-profile http
pause
