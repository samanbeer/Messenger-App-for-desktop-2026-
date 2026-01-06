import sys
import os

os.environ["QTWEBENGINE_CHROMIUM_FLAGS"] = (
    # GPU Akcelerace (aby to nezatěžovalo CPU)
    "--enable-gpu-rasterization "
    "--enable-zero-copy "
    "--ignore-gpu-blocklist "
    "--enable-features=VaapiVideoDecoder,CanvasOopRasterization "
    # OMEZENÍ PAMĚTI (Tohle srazí těch 1.2 GB dolů)
    "--renderer-process-limit=1 "             # Pouze jeden renderovací proces (největší úspora)
    "--js-flags='--max_old_space_size=256' "  # Omezí JS heap (paměť skriptů)
    "--discard-unused-memory "                # Agresivní úklid
    "--disable-logging "                      # Vypne logování (ušetří pár MB)
    "--disable-background-networking "        # Omezí aktivitu na pozadí
)

from PyQt6.QtCore import QUrl, Qt, pyqtSlot
from PyQt6.QtWidgets import (QApplication, QMainWindow, QVBoxLayout, QWidget, 
                             QSystemTrayIcon, QMenu, QMessageBox)
from PyQt6.QtGui import QAction, QIcon, QPixmap, QPainter, QColor, QPainterPath
from PyQt6.QtWebEngineCore import QWebEnginePage, QWebEngineProfile, QWebEngineSettings
from PyQt6.QtWebEngineWidgets import QWebEngineView

# fake identita
USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"

def create_messenger_icon():
    size = 64
    pixmap = QPixmap(size, size)
    pixmap.fill(Qt.GlobalColor.transparent)
    
    painter = QPainter(pixmap)
    painter.setRenderHint(QPainter.RenderHint.Antialiasing)
    
    painter.setBrush(QColor("#0084FF"))
    painter.setPen(Qt.PenStyle.NoPen)
    painter.drawEllipse(0, 0, size, size)
    
    path = QPainterPath()
    path.moveTo(size * 0.5, size * 0.2)
    path.lineTo(size * 0.35, size * 0.55)
    path.lineTo(size * 0.48, size * 0.55)
    path.lineTo(size * 0.42, size * 0.80)
    path.lineTo(size * 0.60, size * 0.45)
    path.lineTo(size * 0.48, size * 0.45)
    path.closeSubpath()
    
    painter.setBrush(QColor("white"))
    painter.drawPath(path)
    painter.end()
    
    return QIcon(pixmap)

class MessengerPage(QWebEnginePage):
    def __init__(self, profile, parent=None):
        super().__init__(profile, parent)
        self._child_windows = [] 

    def on_feature_permission_requested(self, securityOrigin, feature):
        self.setFeaturePermission(securityOrigin, feature, QWebEnginePage.PermissionPolicy.PermissionGrantedByUser)

    def createWindow(self, _type):
        new_window = MainWindow(profile=self.profile(), is_child=True)
        new_window.resize(900, 700)
        self._child_windows.append(new_window)
        new_window.destroyed.connect(lambda: self._cleanup_window(new_window))
        new_window.show()
        return new_window.browser.page()

    def _cleanup_window(self, window):
        if window in self._child_windows:
            self._child_windows.remove(window)

class MainWindow(QMainWindow):
    def __init__(self, profile=None, is_child=False):
        super().__init__()
        self.is_child = is_child
        self.force_close = False 
        self.first_close_notification = True 
        
        self.app_icon = create_messenger_icon()
        self.setWindowIcon(self.app_icon)
        
        if self.is_child:
            self.setWindowTitle("Messenger Hovor")
        else:
            self.setWindowTitle("Messenger Pro")
            
        self.resize(1200, 900)

        # --- SETUP CEST A PROFILU ---
        # Definujeme cesty
        user_home = os.path.expanduser("~")
        self.config_dir = os.path.join(user_home, ".messengerpro")
        
        if not os.path.exists(self.config_dir):
            os.makedirs(self.config_dir, exist_ok=True)

        if profile:
            self.profile = profile
        else:
            self.profile = QWebEngineProfile("MessengerSharedProfile", self)
            self.profile.setCachePath(self.config_dir)
            self.profile.setPersistentStoragePath(self.config_dir)
            self.profile.setHttpUserAgent(USER_AGENT)
            self.profile.setHttpCacheType(QWebEngineProfile.HttpCacheType.DiskHttpCache)
            
            settings = self.profile.settings()
            settings.setAttribute(QWebEngineSettings.WebAttribute.LocalStorageEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.Accelerated2dCanvasEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.WebGLEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.PluginsEnabled, False)

        # VIEW & PAGE
        self.browser = QWebEngineView()
        self.page = MessengerPage(self.profile, self.browser)
        self.page.featurePermissionRequested.connect(self.page.on_feature_permission_requested)
        self.browser.setPage(self.page)

        if not self.is_child: 
            self.browser.setUrl(QUrl("https://www.messenger.com/"))

        central = QWidget()
        lay = QVBoxLayout(central)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.addWidget(self.browser)
        self.setCentralWidget(central)

        if not self.is_child:
            self.setup_tray()
            self.run_first_time_tour()

    def setup_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.app_icon)

        tray_menu = QMenu()
        
        action_show = QAction("Open", self)
        action_show.triggered.connect(self.show_window)
        
        action_quit = QAction("Close Messenger", self)
        action_quit.triggered.connect(self.app_quit)

        tray_menu.addAction(action_show)
        tray_menu.addSeparator()
        tray_menu.addAction(action_quit)
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()
        
        self.tray_icon.activated.connect(self.on_tray_click)

    # --- NOVÁ FUNKCE: TOUR ---
    def run_first_time_tour(self):
        # Cesta k souboru, který značí, že tour už proběhla
        marker_file = os.path.join(self.config_dir, "opened")
        
        if not os.path.exists(marker_file):
            # Soubor neexistuje -> První spuštění
            print("První spuštění detekováno. Zobrazuji tour.")
            
            # Vytvoříme jednoduchý dialog
            msg = QMessageBox(self)
            msg.setWindowTitle("Welcome to Messenger Pro")
            msg.setIconPixmap(self.app_icon.pixmap(64, 64))
            msg.setText("<h3>App Is Ready!</h3>")
            msg.setInformativeText(
                "This app is running on the background even after you close it.<br><br>"
                "<b>Controls:</b><br>"
                "• You can find messenger icon in the taskbar menu near the clock.<br>"
                "• Clicking on icon, you can open the app.<br>"
                "• Right click on icon in taskbar to shut messenger down."
            )
            msg.setStandardButtons(QMessageBox.StandardButton.Ok)
            msg.exec() 

            try:
                with open(marker_file, 'w') as f:
                    f.write("Tour completed.")
            except Exception as e:
                print(f"Nepodařilo se vytvořit marker file: {e}")

    @pyqtSlot(QSystemTrayIcon.ActivationReason)
    def on_tray_click(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.Trigger:
            if self.isVisible():
                self.hide()
            else:
                self.show_window()

    def show_window(self):
        self.show()
        self.setWindowState(self.windowState() & ~Qt.WindowState.WindowMinimized | Qt.WindowState.WindowActive)
        self.activateWindow()

    def app_quit(self):
        self.force_close = True
        self.tray_icon.hide()
        QApplication.instance().quit()

    def closeEvent(self, event):
        if self.is_child:
            event.accept()
            return

        if self.force_close:
            event.accept()
        else:
            event.ignore()
            self.hide()

def main():
    os.environ["QT_ENABLE_HIGHDPI_SCALING"] = "1"
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    
    main_win = MainWindow()
    main_win.show()

    sys.exit(app.exec())

if __name__ == "__main__":
    main()
