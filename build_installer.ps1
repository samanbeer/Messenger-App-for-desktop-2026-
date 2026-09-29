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
    Write-Host "HOTOVO! Instalator je pripraven v slozce: D:\!Documents\VSCode\Messenger\output\MessengeR_Setup.exe" -ForegroundColor Green
} else {
    Write-Host "Chyba pri kompilaci instalatoru!" -ForegroundColor Red
}
