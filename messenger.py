import sys
import os
import ctypes

os.environ["QTWEBENGINE_CHROMIUM_FLAGS"] = (
    "--renderer-process-limit=1 "
    "--disable-gpu-compositing "
    "--disable-extensions "
    "--disable-logging "
    "--disable-background-networking "
    "--js-flags='--max_old_space_size=128' "
    "--discard-unused-memory "
    "--enable-features=VaapiVideoDecoder "
)

from PyQt6.QtCore import QUrl, Qt, pyqtSlot, QTimer
from PyQt6.QtWidgets import (QApplication, QMainWindow, QVBoxLayout, QWidget, 
                             QSystemTrayIcon, QMenu, QMessageBox)
from PyQt6.QtGui import QAction, QIcon, QPixmap, QPainter, QColor, QPainterPath
from PyQt6.QtWebEngineCore import (QWebEnginePage, QWebEngineProfile, 
                                   QWebEngineSettings, QWebEngineUrlRequestInterceptor)
from PyQt6.QtWebEngineWidgets import QWebEngineView

USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"

def trim_memory():
    """Donutí Windows přesunout nepoužívanou paměť na disk (PageFile)."""
    if os.name == 'nt':
        try:
            pid = os.getpid()
            process = ctypes.windll.kernel32.OpenProcess(0x1F0FFF, False, pid)
            ctypes.windll.psapi.EmptyWorkingSet(process)
            ctypes.windll.kernel32.CloseHandle(process)
        except: pass

class AdBlocker(QWebEngineUrlRequestInterceptor):
    def interceptRequest(self, info):
        url = info.requestUrl().toString()
        block_list = [
            "google-analytics", "doubleclick", "fbevents.js", "ad_placements", 
            "logging", "logger", "/ajax/bz"
        ]
        for block in block_list:
            if block in url:
                info.block(True)
                return

def create_messenger_icon(has_notification=False):
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

    if has_notification:
        painter.setBrush(QColor("#FF0000"))
        dot_size = size / 3.2
        painter.drawEllipse(int(size - dot_size), 0, int(dot_size), int(dot_size))

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
        
        # Ikony
        self.icon_normal = create_messenger_icon(has_notification=False)
        self.icon_alert = create_messenger_icon(has_notification=True)
        self.setWindowIcon(self.icon_normal)
        
        if self.is_child:
            self.setWindowTitle("Messenger Hovor")
        else:
            self.setWindowTitle("Messenger Pro")
        self.resize(1200, 900)

        # Config
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
            
            self.interceptor = AdBlocker()
            self.profile.setUrlRequestInterceptor(self.interceptor)

            settings = self.profile.settings()
            settings.setAttribute(QWebEngineSettings.WebAttribute.LocalStorageEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.Accelerated2dCanvasEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.WebGLEnabled, True)
            settings.setAttribute(QWebEngineSettings.WebAttribute.PluginsEnabled, False)
            settings.setAttribute(QWebEngineSettings.WebAttribute.PdfViewerEnabled, False)

        self.browser = QWebEngineView()
        self.page = MessengerPage(self.profile, self.browser)
        self.page.featurePermissionRequested.connect(self.page.on_feature_permission_requested)
        self.browser.setPage(self.page)

        if not self.is_child: 
            self.browser.setUrl(QUrl("https://www.messenger.com/"))
            self.browser.titleChanged.connect(self.on_title_changed)

        central = QWidget()
        lay = QVBoxLayout(central)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.addWidget(self.browser)
        self.setCentralWidget(central)

        if not self.is_child:
            self.setup_tray()
            self.run_first_time_tour()
            
            self.memory_timer = QTimer(self)
            self.memory_timer.timeout.connect(self.check_memory_optimization)
            self.memory_timer.start(60000)

    def check_memory_optimization(self):
        if not self.isVisible():
            trim_memory()

    def setup_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.icon_normal)

        tray_menu = QMenu()
        action_show = QAction("Otevřít", self)
        action_show.triggered.connect(self.show_window)
        action_quit = QAction("Ukončit Messenger", self)
        action_quit.triggered.connect(self.app_quit)

        tray_menu.addAction(action_show)
        tray_menu.addSeparator()
        tray_menu.addAction(action_quit)
        
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.show()
        self.tray_icon.activated.connect(self.on_tray_click)
        self.tray_icon.messageClicked.connect(self.show_window)

    def on_title_changed(self, title):
        if "(" in title:
            if self.windowIcon() != self.icon_alert:
                self.tray_icon.setIcon(self.icon_alert)
                self.setWindowIcon(self.icon_alert)
                
                if not self.isVisible() or not self.isActiveWindow():
                    self.tray_icon.showMessage(
                        "Nová zpráva", 
                        f"{title}", 
                        QSystemTrayIcon.MessageIcon.NoIcon, 
                        4000
                    )
        else:
            if self.windowIcon() != self.icon_normal:
                self.tray_icon.setIcon(self.icon_normal)
                self.setWindowIcon(self.icon_normal)

    def run_first_time_tour(self):
        marker_file = os.path.join(self.config_dir, "opened")
        if not os.path.exists(marker_file):
            msg = QMessageBox(self)
            msg.setWindowTitle("Vítejte v Messenger Pro")
            msg.setIconPixmap(self.icon_normal.pixmap(64, 64))
            msg.setText("<h3>Aplikace připravena!</h3>")
            msg.setInformativeText("Aplikace nyní běží na pozadí a šetří paměť.")
            msg.setStandardButtons(QMessageBox.StandardButton.Ok)
            msg.exec() 
            try:
                with open(marker_file, 'w') as f:
                    f.write("Tour completed.")
            except: pass

    @pyqtSlot(QSystemTrayIcon.ActivationReason)
    def on_tray_click(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.Trigger:
            if self.isVisible():
                self.hide()
                trim_memory()
            else:
                self.show_window()

    def show_window(self):
        self.show()
        self.setWindowState(self.windowState() & ~Qt.WindowState.WindowMinimized | Qt.WindowState.WindowActive)
        self.activateWindow()
        self.raise_()

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
            trim_memory()
def main():
    os.environ["QT_ENABLE_HIGHDPI_SCALING"] = "1"
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    
    main_win = MainWindow()
    main_win.show()

    sys.exit(app.exec())

if __name__ == "__main__":
    main()
