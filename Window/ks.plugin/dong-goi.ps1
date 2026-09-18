# Đóng gói plugin ký số thành bộ cài mà BE phát cho người dùng.
#
# Sinh ra "Ký số plugin.exe" vào Plugins/ của MỌI backend dùng plugin (ksts.be và kssm.be) — endpoint
# api/core/plugin/bo-cai/noi-dung của từng bên đọc đúng file này. Chạy lại mỗi khi sửa mã nguồn plugin hoặc
# đổi bộ cài middleware.
#
# Bản ra là MỘT file exe: trình cài đặt NSIS có wizard, mang sẵn plugin self-contained bên trong (máy người
# dùng không cần .NET runtime) và bộ cài middleware bit4id. Giữ nguyên tên file cũ nên backend không phải sửa.

$ErrorActionPreference = "Stop"

$goc = $PSScriptRoot
$tam = Join-Path $env:TEMP ("ks-plugin-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$tamPublish = Join-Path $tam "publish"
$tamBoCai = Join-Path $tam "bo-cai"
$raBoCai = Join-Path $goc "bo-cai"
# Mỗi backend tự phát bộ cài của mình, không bên nào đi mượn file của bên kia: máy chủ triển khai
# riêng từng dịch vụ.
$dich = @(
    (Join-Path $goc "..\ksts.be\ksts.be.api\Plugins"),
    (Join-Path $goc "..\kssm.be\kssm.be.api\Plugins")
)

Write-Host "1/5 Kiem tra middleware bit4id..." -ForegroundColor Cyan
# Middleware là phần mềm của hãng token, không nằm trong repo. Có thì bộ cài mang kèm; không có thì vẫn đóng
# gói được, chỉ là người dùng phải tự cài middleware trước.
$nguonVendor = Join-Path $goc "vendor\bit4id"
$boCaiVendor = if (Test-Path $nguonVendor) {
    Get-ChildItem $nguonVendor -File | Where-Object { $_.Extension -in ".exe", ".msi" } | Select-Object -First 1
} else { $null }

if ($null -eq $boCaiVendor) {
    Write-Host "    KHONG THAY bo cai bit4id trong vendor\bit4id." -ForegroundColor Yellow
    Write-Host "    Ban ra se KHONG tu cai duoc middleware. Xem vendor\bit4id\*.md." -ForegroundColor Yellow
}
else {
    Write-Host "    Se kem $($boCaiVendor.Name) ($([math]::Round($boCaiVendor.Length/1MB,1)) MB)." -ForegroundColor Green
}

Write-Host "2/5 Tim makensis..." -ForegroundColor Cyan
$makensis = @(
    "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    "$env:ProgramFiles\NSIS\makensis.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($null -eq $makensis) {
    $lenh = Get-Command makensis -ErrorAction SilentlyContinue
    if ($lenh) { $makensis = $lenh.Source }
}

if ($null -eq $makensis) { throw "Khong tim thay makensis.exe. Cai NSIS 3 roi chay lai." }
Write-Host "    $makensis" -ForegroundColor Green

Write-Host "3/5 Publish plugin..." -ForegroundColor Cyan
$csproj = Join-Path $goc "ks.plugin.api\ks.plugin.api.csproj"
dotnet publish $csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $tamPublish --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish plugin that bai." }

# Lay ten exe tu chinh ban publish thay vi ghi cung: ten co dau, ma PowerShell 5.1 doc .ps1 khong BOM theo
# bang ma ANSI nen chuoi co dau viet thang trong script se ra sai ten file.
$nguon = Get-ChildItem $tamPublish -Filter *.exe -File | Select-Object -First 1
if ($null -eq $nguon) { throw "Khong thay file exe nao trong ban publish." }

$phienBan = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $phienBan) { throw "Khong doc duoc <Version> trong $csproj." }
$phienBanDay = (([string]$phienBan).Split('.') + @('0', '0', '0', '0'))[0..3] -join '.'

Write-Host "4/5 Dong goi bo cai NSIS ($phienBan)..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $tamBoCai | Out-Null
$fileBoCai = Join-Path $tamBoCai $nguon.Name

$thamSo = @(
    "/V2",
    "/DPHIEN_BAN=$phienBan",
    "/DPHIEN_BAN_DAY=$phienBanDay",
    "/DDUONG_EXE=$($nguon.FullName)",
    "/DDUONG_RA=$fileBoCai"
)
if ($null -ne $boCaiVendor) { $thamSo += "/DBO_CAI_MIDDLEWARE=$($boCaiVendor.FullName)" }
$thamSo += (Join-Path $goc "bo-cai.nsi")

& $makensis $thamSo
if ($LASTEXITCODE -ne 0) { throw "Dong goi bo cai NSIS that bai." }

Write-Host "5/5 Chep sang thu muc phat hanh..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $raBoCai | Out-Null
$daChep = @()
$exeRa = Join-Path $raBoCai $nguon.Name
Copy-Item $fileBoCai $exeRa -Force
$daChep += $exeRa

foreach ($d in $dich) {
    $repo = Split-Path (Split-Path $d -Parent) -Parent
    if (-not (Test-Path $repo)) {
        Write-Host "    Bo qua $repo (khong co trong workspace)." -ForegroundColor DarkGray
        continue
    }

    New-Item -ItemType Directory -Force -Path $d | Out-Null
    $exe = Join-Path $d $nguon.Name
    Copy-Item $fileBoCai $exe -Force
    $daChep += $exe
}

Remove-Item $tam -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
foreach ($exe in $daChep) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Xong: $exe ($mb MB)" -ForegroundColor Green
}
Write-Host "Wizard: Tiep -> chon thu muc -> chon moi truong -> tien do -> Finish." -ForegroundColor DarkGray
