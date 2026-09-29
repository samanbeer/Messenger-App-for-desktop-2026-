# MessengeR (Desktop Windows)

Lehká a rychlá desktopová aplikace pro Messenger na Windows 10 a 11, postavená na nativním .NET 8 (WPF) a jádru Microsoft Edge WebView2.

Slouží jako energeticky a paměťově úsporná náhrada za ukončenou oficiální desktopovou aplikaci.

## Funkce

- **Nízká spotřeba paměti**: Typicky 40–80 MB RAM na pozadí díky využití sdíleného systémového runtime WebView2 a automatickému uvolňování paměti.
- **Čisté rozhraní bez horní lišty**: Automatické odstranění hlavičky Facebooku a nulová horní mezera pro maximální prostor na chat.
- **Nativní oznámení Windows**: Systémové Toast notifikace s textem zprávy a avatarem odesílatele.
- **Běh na pozadí (System Tray)**: Minimalizace do oznamovací oblasti, červený indikátor nepřečtených zpráv a rychlé kontextové menu.
- **Pamatování velikosti a pozice**: Automatické ukládání a obnova rozměrů a umístění okna.
- **Audio a video hovory**: Integrované udělování oprávnění pro mikrofon a kameru a dedikované okno pro hovory.
- **Spouštění při startu Windows**: Volitelný tichý start minimalizovaný v oznamovací oblasti.
- **Otevírání odkazů**: Externí odkazy se otevírají ve výchozím webovém prohlížeči.
- **Automatická kontrola aktualizací**: Možnost zkontrolovat novou verzi přímo z kontextového menu v oznamovací oblasti. Při nalezení aktualizace se instalační balíček stáhne a automaticky nainstaluje.
- **Tmavý režim**: Tmavé záhlaví ladící se systémovým vzhledem Windows.

## Požadavky a sestavení

- Windows 10 (1809+) nebo Windows 11
- .NET 8 SDK / Desktop Runtime
- Microsoft Edge WebView2 Runtime (standardní součást Windows)

### Příkazy pro sestavení

```powershell
# Běžné spuštění aplikace pro vývoj
dotnet run

# Publikace rychlého spustitelného balíčku pro lokální počítač
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish

# Automatické vytvoření instalátorů (offline setup + online web installer)
.\build_installer.ps1
```

### Výsledné instalační balíčky ve složce `output/`

1. **`MessengeR_Online_Installer.exe`**: Lehký online instalátor, který po spuštění automaticky stáhne nejnovější verzi z GitHubu, umožní nastavit zástupce na ploše a v nabídce Start a rovnou ji nainstaluje.
2. **`MessengeR_Setup.exe`**: Kompletní offline instalační balíček obsahující vše v jednom bez nutnosti připojení k internetu a bez nutnosti instalovat .NET.

Aplikace se instaluje do `%LocalAppData%\Programs\MessengeR` a nevyžaduje administrátorská práva. Lze ji kdykoliv standardně odinstalovat přes *Nainstalované aplikace* ve Windows.
