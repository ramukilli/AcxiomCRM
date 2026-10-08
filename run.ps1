$env:PATH = "C:\Users\ramuk\AppData\Local\Microsoft\dotnet;" + $env:PATH
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Starting AcxiomCRM Enterprise on http://localhost:5164" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
dotnet run --launch-profile http
