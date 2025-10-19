param(
    [switch]$SkipBuild
)

Write-Host "Running BorsaGPT verification checks..." -ForegroundColor Cyan

if (-not $SkipBuild)
{
    Write-Host "dotnet build" -ForegroundColor Yellow
    dotnet build ..\borsaGPT-proje_1.sln
}

Write-Host "dotnet test --no-build" -ForegroundColor Yellow
dotnet test ..\borsaGPT-proje_1.sln --no-build
