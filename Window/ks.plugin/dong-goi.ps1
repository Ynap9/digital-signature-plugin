# Đóng gói plugin ký số thành bộ cài mà BE phát cho người dùng.
#
# Sinh ra "Ký số plugin.exe" vào Plugins/ của MỌI backend dùng plugin (ksts.be và kssm.be) — endpoint
# api/core/plugin/bo-cai/noi-dung của từng bên đọc đúng file này. Chạy lại mỗi khi sửa mã nguồn plugin hoặc
# đổi bộ cài trình điều khiển token.
#
# Bản ra là MỘT file exe: trình cài đặt NSIS có wizard, mang sẵn plugin self-contained bên trong (máy người
# dùng không cần .NET runtime) cùng bộ cài bit4id và TokenManager. Giữ nguyên tên file cũ nên backend không phải sửa.

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$tempDir = Join-Path $env:TEMP ("ks-plugin-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$publishDir = Join-Path $tempDir "publish"
$setupDir = Join-Path $tempDir "bo-cai"
$outputDir = Join-Path $root "bo-cai"
# Mỗi backend tự phát bộ cài của mình, không bên nào đi mượn file của bên kia: máy chủ triển khai
# riêng từng dịch vụ.
$targets = @(
    (Join-Path $root "..\..\..\ksts\ksts.be\ksts.be.api\Plugins"),
    (Join-Path $root "..\..\..\ksts\kssm.be\kssm.be.api\Plugins")
)

# Trình điều khiển token là phần mềm của hãng, không nằm trong repo. Có thì bộ cài mang kèm; không có thì vẫn
# đóng gói được, chỉ là người dùng phải tự cài trước.
function Find-VendorSetup([string]$folder, [string]$label) {
    $dir = Join-Path $root "vendor\$folder"
    $setup = if (Test-Path $dir) {
        Get-ChildItem $dir -File | Where-Object { $_.Extension -in ".exe", ".msi" } | Select-Object -First 1
    } else { $null }

    if ($null -eq $setup) {
        Write-Host "    KHONG THAY bo cai $label trong vendor\$folder." -ForegroundColor Yellow
        Write-Host "    Ban ra se KHONG tu cai duoc $label. Xem vendor\$folder\*.md." -ForegroundColor Yellow
    }
    else {
        Write-Host "    Se kem $($setup.Name) ($([math]::Round($setup.Length/1MB,1)) MB)." -ForegroundColor Green
    }

    return $setup
}

# Co chay ngam doc tu dong dau tien (khong phai chu thich #) cua tham-so.txt; khong co file thi bo cai dung co
# mac dinh theo duoi file. Dua sang NSIS qua file include chu khong qua dong lenh: PowerShell 5.1 lam roi dau
# nhay kep khi truyen tham so cho chuong trinh ngoai, ma co InstallShield co dang /s /v"/qn".
function Read-VendorArgs([string]$folder) {
    $file = Join-Path $root "vendor\$folder\tham-so.txt"
    if (-not (Test-Path $file)) { return $null }

    $line = Get-Content $file -Encoding UTF8 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith("#") } |
        Select-Object -First 1
    if (-not $line) { return $null }
    if ($line.Contains('`')) { throw "vendor\$folder\tham-so.txt khong duoc chua dau backtick." }

    Write-Host "    Co chay ngam tu vendor\$folder\tham-so.txt: $line" -ForegroundColor Green
    return $line
}

Write-Host "1/5 Kiem tra trinh dieu khien token..." -ForegroundColor Cyan
$bit4idSetup = Find-VendorSetup "bit4id" "bit4id"
$tokenManagerSetup = Find-VendorSetup "vgca-tokenmanager" "TokenManager"
$bit4idArgs = if ($null -ne $bit4idSetup) { Read-VendorArgs "bit4id" } else { $null }
$tokenManagerArgs = if ($null -ne $tokenManagerSetup) { Read-VendorArgs "vgca-tokenmanager" } else { $null }

Write-Host "2/5 Tim makensis..." -ForegroundColor Cyan
$makensis = @(
    "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    "$env:ProgramFiles\NSIS\makensis.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($null -eq $makensis) {
    $command = Get-Command makensis -ErrorAction SilentlyContinue
    if ($command) { $makensis = $command.Source }
}

if ($null -eq $makensis) { throw "Khong tim thay makensis.exe. Cai NSIS 3 roi chay lai." }
Write-Host "    $makensis" -ForegroundColor Green

Write-Host "3/5 Publish plugin..." -ForegroundColor Cyan
$csproj = Join-Path $root "ks.plugin.api\ks.plugin.api.csproj"
dotnet publish $csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish plugin that bai." }

# Lay ten exe tu chinh ban publish thay vi ghi cung: ten co dau, ma PowerShell 5.1 doc .ps1 khong BOM theo
# bang ma ANSI nen chuoi co dau viet thang trong script se ra sai ten file.
$pluginExe = Get-ChildItem $publishDir -Filter *.exe -File | Select-Object -First 1
if ($null -eq $pluginExe) { throw "Khong thay file exe nao trong ban publish." }

$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Khong doc duoc <Version> trong $csproj." }
$versionFull = (([string]$version).Split('.') + @('0', '0', '0', '0'))[0..3] -join '.'

Write-Host "4/5 Dong goi bo cai NSIS ($version)..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $setupDir | Out-Null
$setupFile = Join-Path $setupDir $pluginExe.Name

$arguments = @(
    "/V2",
    "/DVERSION=$version",
    "/DVERSION_FULL=$versionFull",
    "/DPLUGIN_EXE=$($pluginExe.FullName)",
    "/DOUTPUT_FILE=$setupFile"
)
if ($null -ne $bit4idSetup) {
    $arguments += "/DBIT4ID_SETUP=$($bit4idSetup.FullName)"
    $arguments += "/DBIT4ID_SETUP_EXT=$($bit4idSetup.Extension.ToLowerInvariant())"
}
if ($null -ne $tokenManagerSetup) {
    $arguments += "/DTOKEN_MANAGER_SETUP=$($tokenManagerSetup.FullName)"
    $arguments += "/DTOKEN_MANAGER_SETUP_EXT=$($tokenManagerSetup.Extension.ToLowerInvariant())"
}

$argsDefines = @()
if ($bit4idArgs) { $argsDefines += '!define BIT4ID_SETUP_ARGS `' + $bit4idArgs.Replace('$', '$$') + '`' }
if ($tokenManagerArgs) { $argsDefines += '!define TOKEN_MANAGER_SETUP_ARGS `' + $tokenManagerArgs.Replace('$', '$$') + '`' }
if ($argsDefines.Count -gt 0) {
    $argsInclude = Join-Path $setupDir "vendor-args.nsh"
    [System.IO.File]::WriteAllLines($argsInclude, [string[]]$argsDefines, (New-Object System.Text.UTF8Encoding $true))
    $arguments += "/DVENDOR_ARGS_INCLUDE=$argsInclude"
}
$arguments += (Join-Path $root "bo-cai.nsi")

& $makensis $arguments
if ($LASTEXITCODE -ne 0) { throw "Dong goi bo cai NSIS that bai." }

Write-Host "5/5 Chep sang thu muc phat hanh..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$copied = @()
$outputExe = Join-Path $outputDir $pluginExe.Name
Copy-Item $setupFile $outputExe -Force
$copied += $outputExe

foreach ($target in $targets) {
    $repo = Split-Path (Split-Path $target -Parent) -Parent
    if (-not (Test-Path $repo)) {
        Write-Host "    Bo qua $repo (khong co trong workspace)." -ForegroundColor DarkGray
        continue
    }

    New-Item -ItemType Directory -Force -Path $target | Out-Null
    $exe = Join-Path $target $pluginExe.Name
    Copy-Item $setupFile $exe -Force
    $copied += $exe
}

Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
foreach ($exe in $copied) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Xong: $exe ($mb MB)" -ForegroundColor Green
}
Write-Host "Wizard: Tiep -> chon thu muc -> chon moi truong -> tien do -> Finish." -ForegroundColor DarkGray
