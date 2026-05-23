#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$AppProject    = "ClassIsland.Desktop\ClassIsland.Desktop.csproj"
$PluginProject = "ClassIsland.PortableImport\ClassIsland.PortableImport.csproj"
$AppOut        = "ClassIsland.Desktop\bin\Debug\net8.0-windows10.0.19041.0"
$PluginOut     = "ClassIsland.PortableImport\bin\Debug\net8.0"
$PluginDest    = "$AppOut\Plugins\ClassIsland.PortableImport"

Write-Host "=== ClassIsland Dev Build ===" -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET 8 SDK not found. Please install it first."
}

Write-Host "[1/2] Building main app..."
dotnet build $AppProject -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Main app build failed." }
Write-Host "      OK" -ForegroundColor Green

Write-Host "[2/2] Building plugin..."
dotnet build $PluginProject -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Error "Plugin build failed." }
Write-Host "      OK" -ForegroundColor Green

Write-Host "Deploying plugin..."
New-Item -ItemType Directory -Force -Path $PluginDest | Out-Null
Copy-Item -Path "$PluginOut\*" -Destination $PluginDest -Recurse -Force
Write-Host "Copied to $PluginDest" -ForegroundColor Green

$launch = Read-Host "Launch ClassIsland? [Y/n]"
if ($launch -ne 'n' -and $launch -ne 'N') {
    Start-Process "$AppOut\ClassIsland.Desktop.exe"
}
