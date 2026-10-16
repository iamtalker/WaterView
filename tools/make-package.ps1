# Builds the release layout in the repo root (WaterView.exe + app\) and zips it.
# Local folder first, upload second: the zip is made from what is on disk here.
#   powershell -ExecutionPolicy Bypass -File tools\make-package.ps1 [-Zip <path>]
param([string]$Zip = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ($Zip -eq "") { $Zip = Join-Path $root "WaterView.zip" }

if (Get-Process WaterView -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$root\*" }) {
    throw "WaterView is running from this folder. Close it first."
}

# app\ = main program (Profile\ is the user's settings and stays untouched)
dotnet publish "$root\NeeView\NeeView.csproj" -c Release -r win-x64 --self-contained false -p:DebugType=none -o "$root\app"
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# WaterView.exe = launcher
$tmp = Join-Path ([IO.Path]::GetTempPath()) "wv-launcher"
dotnet build "$root\WaterViewLauncher\WaterViewLauncher.csproj" -c Release -o $tmp
if ($LASTEXITCODE -ne 0) { throw "launcher build failed" }
Copy-Item "$tmp\WaterView.exe" "$root\WaterView.exe" -Force

Copy-Item "$root\LICENSE.md", "$root\THIRDPARTY_LICENSES.md" "$root\app\" -Force

# zip = WaterView.exe + app\ (without Profile* and *.pdb)
$stage = Join-Path ([IO.Path]::GetTempPath()) "wv-stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory "$stage\app" | Out-Null
Get-ChildItem "$root\app" | Where-Object { $_.Name -notlike "Profile*" -and $_.Name -notlike "*.pdb" } |
    ForEach-Object { Copy-Item $_.FullName "$stage\app" -Recurse -Force }
Copy-Item "$root\WaterView.exe" $stage
if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $Zip
Remove-Item $stage -Recurse -Force

"version : " + (Get-Item "$root\app\WaterView.exe").VersionInfo.ProductVersion
"zip     : $Zip (" + [math]::Round((Get-Item $Zip).Length / 1MB, 1) + " MB)"
"sha256  : " + (Get-FileHash $Zip -Algorithm SHA256).Hash.ToLower()
