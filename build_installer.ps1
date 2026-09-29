# Build script for MessengeR standalone installer
Write-Host "==> Publikuji samostatnou aplikaci s .NET runtime..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish_standalone

if ($LASTEXITCODE -ne 0) {
    Write-Host "Chyba pri publikaci aplikace!" -ForegroundColor Red
    exit 1
}

$iscc = "C:\Users\Tom\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $found = Get-Command iscc -ErrorAction SilentlyContinue
    if ($found) { $iscc = $found.Source }
}

Write-Host "==> Kompiluji instalator pomoci Inno Setup..." -ForegroundColor Cyan
& $iscc "D:\!Documents\VSCode\Messenger\installer.iss"

if ($LASTEXITCODE -eq 0) {
    Write-Host "==> Kompiluji online web instalator (MessengeR_Online_Installer.exe)..." -ForegroundColor Cyan
    dotnet publish web_installer/MessengeR.WebInstaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./output_web_temp
    if (Test-Path "./output_web_temp/MessengeR_WebInstaller.exe") {
        Copy-Item "./output_web_temp/MessengeR_WebInstaller.exe" -Destination "./output/MessengeR_Online_Installer.exe" -Force
        Remove-Item "./output_web_temp" -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "VSE HOTOVO!" -ForegroundColor Green
    Write-Host "  1. Plny offline instalator: output\MessengeR_Setup.exe" -ForegroundColor Green
    Write-Host "  2. Online web instalator (stahuje z GitHubu): output\MessengeR_Online_Installer.exe" -ForegroundColor Green
} else {
    Write-Host "Chyba pri kompilaci instalatoru!" -ForegroundColor Red
}
