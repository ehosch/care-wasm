# This script is cross-platform, supporting all OSes that PowerShell Core/7 runs on.

$currentDirectory = Get-Location
$rootDirectory = git rev-parse --show-toplevel
$infrastructurePrj = Join-Path -Path $rootDirectory -ChildPath 'src/Client.Infrastructure/Care.Wasm.Client.Infrastructure.csproj'

Write-Host "Make sure care-webapi is running (dotnet run --project src/Host, or dockerized on its dev https port). `n"
Write-Host "Press any key to continue... `n"
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

<# Run command #>
dotnet build -t:NSwag $infrastructurePrj

Set-Location -Path $currentDirectory
Write-Host -NoNewLine 'NSwag Regenerated. Press any key to continue...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');
