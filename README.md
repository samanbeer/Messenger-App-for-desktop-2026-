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
- **Tmavý režim**: Tmavé záhlaví ladící se systémovým vzhledem Windows.

## Požadavky a sestavení

- Windows 10 (1809+) nebo Windows 11
- .NET 8 SDK / Desktop Runtime
- Microsoft Edge WebView2 Runtime (standardní součást Windows)

### Příkazy pro sestavení

```powershell
# Běžné spuštění
dotnet run

# Publikace samostatného spustitelného .exe
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```
