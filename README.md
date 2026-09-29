# Messenger App for Desktop (2026 Edition) 💬

Moderní, lehká a rychlá desktopová aplikace pro **Messenger** pro systém Windows 10 a 11, postavená na nativním **.NET 8 (WPF) + Microsoft Edge WebView2**.

> Facebook oficiálně ukončil podporu původního Messengeru pro desktop. Tato aplikace slouží jako plnohodnotná, energeticky i paměťově úsporná alternativa.

---

## ✨ Klíčové funkce

- ⚡ **Extrémně nízká spotřeba RAM (~40–70 MB)**: Využívá systémové jádro Microsoft Edge WebView2, které už ve Windows běží, namísto těžkého Chromium bundle.
- 🔔 **Nativní Windows Toast Notifikace**: Skutečná systémová oznámení Windows s textem zprávy a avatarem odesílatele. Kliknutím na notifikaci se aplikace okamžitě otevře.
- 📍 **Ikona v oznamovací oblasti (System Tray)**:
  - Běh na pozadí při zavření křížkem nebo minimalizaci.
  - Vizuální červený badge na ikoně při nepřečtených zprávách (`(1)`).
  - Kontextové menu pro rychlé otevření, ztlumení oznámení, přepnutí autostartu a ukončení.
- 🚀 **Automatické spuštění se systémem**: Možnost spustit Messenger přímo při startu Windows (minimalizovaný na pozadí).
- 🛡️ **Integrovaný AdBlocker / Telemetrie filter**: Blokuje sledovací skripty a zbytečnou zátěž sítě pro ještě větší plynulost.
- 📞 **Podpora audio a video hovorů**: Automatické udělování oprávnění pro mikrofon a kameru, samostatné okno pro hovory.
- 🌐 **Chytré otevírání odkazů**: Externí odkazy se otevírají ve vašem výchozím prohlížeči, takže vám nenarušují rozhraní chatu.
- 🌙 **Windows Dark Mode**: Tmavé záhlaví okna plně ladící s moderním vzhledem Windows 10/11.

---

## 🛠️ Požadavky a spuštění

- **OS**: Windows 10 (1809+) nebo Windows 11
- **Runtime**: .NET 8 Desktop Runtime (součástí většiny moderních instalací Windows)
- **WebView2 Runtime**: Standardně předinstalován ve Windows 10 a 11 (součást Microsoft Edge)

### Sestavení ze zdrojových kódů:

```powershell
# Vývojářské sestavení a spuštění
dotnet run

# Publikace samostatného optimalizovaného .exe souboru
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```
