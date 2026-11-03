using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace dDrive.App;

public static class LocalizationService
{
    private sealed record Translation(string German, string Mandarin, string Hindi, string Spanish, string French);

    private static readonly Dictionary<string, string> EnglishOverrides = new(StringComparer.Ordinal)
    {
        ["Importieren"] = "Import",
        ["Exportieren"] = "Export",
        ["Verbindungen"] = "Connections",
        ["Verbindungen suchen"] = "Search connections",
        ["0 Speicherorte"] = "0 locations",
        ["Status"] = "Status",
        ["Name"] = "Name",
        ["Server"] = "Server",
        ["Bereit"] = "Ready",
        ["Verbunden"] = "Connected",
        ["Getrennt"] = "Disconnected",
        ["Passwort fehlt"] = "Password missing",
        ["Verbinde ..."] = "Connecting ...",
        ["Fehler"] = "Error",
        ["Verbindung fehlgeschlagen"] = "Connection failed",
        ["Neue Verbindung"] = "New connection",
        ["Verbindung"] = "Connection",
        ["Verbinden"] = "Connect",
        ["Trennen"] = "Disconnect",
        ["Bearbeiten"] = "Edit",
        ["Ansehen"] = "View",
        ["Als Standard setzen"] = "Set as default",
        ["Duplizieren"] = "Duplicate",
        ["Löschen"] = "Delete",
        ["Schließen"] = "Close",
        ["Verbindung ansehen"] = "View connection",
        ["Die Verbindung ist aktiv. Zum Ändern zuerst trennen."] = "The connection is active. Disconnect it first to make changes.",
        ["Verbindung löschen"] = "Delete connection",
        ["Verbindung testen"] = "Test connection",
        ["Anzeigename"] = "Display name",
        ["Protokoll"] = "Protocol",
        ["Serveradresse"] = "Server address",
        ["Remote-Pfad"] = "Remote path",
        ["Laufwerk"] = "Drive",
        ["Benutzername"] = "Username",
        ["Passwort"] = "Password",
        ["Passwort eingeben, um es im Windows Credential Manager zu setzen"] = "Enter a password to store it in Windows Credential Manager.",
        ["Schreibgeschützt"] = "Read-only",
        ["Beim Start des Programms verbinden"] = "Connect when the program starts",
        ["Als Standardverbindung festlegen"] = "Set as default connection",
        ["Standard"] = "Default",
        ["Icon"] = "Icon",
        ["Icon wählen …"] = "Choose icon …",
        ["Icon zurücksetzen"] = "Reset icon",
        ["Webseite im Browser öffnen"] = "Open website in browser",
        ["Webseite mobil anzeigen"] = "Show website in mobile view",
        ["Im Browser öffnen"] = "Open in browser",
        ["dDrive Webansicht (Mobil)"] = "dDrive web view (mobile)",
        ["Laufwerk öffnen"] = "Open drive",
        ["In dDrive anzeigen"] = "Show in dDrive",
        ["Die Webansicht benötigt die Microsoft WebView2-Laufzeit. Die Seite wird stattdessen im Standardbrowser geöffnet."] = "The web view requires the Microsoft WebView2 runtime. The page will open in the default browser instead.",
        ["TLS-Zertifikat prüfen"] = "Verify TLS certificate",
        ["TLS verwenden (HTTPS/FTPS)"] = "Use TLS (HTTPS/FTPS)",
        ["Über dDrive"] = "About dDrive",
        ["Cloud Netzwerklaufwerk für Windows"] = "Cloud network drive for Windows",
        ["dDrive verbindet deine Netzwerkdienste als Laufwerke in Windows."] = "dDrive connects your network services as drives in Windows.",
        ["WebDAV kann nativ genutzt werden; Rclone erweitert die unterstützten Protokolle."] = "WebDAV works natively; Rclone expands the supported protocols.",
        ["Zugangsdaten werden im Windows Credential Manager gespeichert."] = "Credentials are stored in Windows Credential Manager.",
        ["Netzwerkspeicher"] = "Network storage",
        ["Globale Einstellungen"] = "Global settings",
        ["Settings"] = "Settings",
        ["Global settings"] = "Global settings",
        ["Start with Windows"] = "Start with Windows",
        ["Minimiert starten"] = "Start minimized",
        ["Enable Rclone support"] = "Enable Rclone support",
        ["Language"] = "Language",
        ["Color scheme"] = "Color scheme",
        ["Ozean"] = "Ocean",
        ["Wald"] = "Forest",
        ["Graphit"] = "Graphite",
        ["Cancel"] = "Cancel",
        ["Ich habe gespendet (Dialog beim Start nicht mehr anzeigen)"] = "I have donated (do not show this dialog at startup)",
        ["Unterstütze dDrive mit Buy Me a Coffee"] = "Support dDrive with Buy Me a Coffee",
        ["GitHub-Projekt"] = "GitHub project",
        ["dDrive öffnen"] = "Open dDrive",
        ["Beenden"] = "Exit"
    };

    private static readonly Dictionary<string, string> GermanOverrides = new(StringComparer.Ordinal)
    {
        ["Settings"] = "Einstellungen",
        ["Global settings"] = "Globale Einstellungen",
        ["Start with Windows"] = "Mit Windows starten",
        ["Start minimized"] = "Minimiert starten",
        ["Enable Rclone support"] = "Rclone-Unterstützung aktivieren",
        ["Language"] = "Sprache",
        ["Color scheme"] = "Farbschema",
        ["Default website popup size (width × height)"] = "Standard-Größe des Website-Popups (Breite × Höhe)",
        ["Width in pixels (300–2000)"] = "Breite in Pixeln (300–2000)",
        ["Height in pixels (360–2000)"] = "Höhe in Pixeln (360–2000)",
        ["Default website popup position"] = "Standard-Position des Website-Popups",
        ["Bottom right, above the taskbar"] = "Unten rechts, über der Taskleiste",
        ["Center of the screen"] = "Mitte des Bildschirms",
        ["Save"] = "Speichern",
        ["Cancel"] = "Abbrechen",
        ["I have donated (do not show this dialog at startup)"] = "Ich habe gespendet (Dialog beim Start nicht mehr anzeigen)"
    };

    private static readonly Translation[] Entries =
    [
        new("Importieren", "导入", "आयात करें", "Importar", "Importer"),
        new("Exportieren", "导出", "निर्यात करें", "Exportar", "Exporter"),
        new("Verbindungen", "连接", "कनेक्शन", "Conexiones", "Connexions"),
        new("Verbindungen suchen", "搜索连接", "कनेक्शन खोजें", "Buscar conexiones", "Rechercher des connexions"),
        new("0 Speicherorte", "0 个位置", "0 स्थान", "0 ubicaciones", "0 emplacements"),
        new("Status", "状态", "स्थिति", "Estado", "État"),
        new("Name", "名称", "नाम", "Nombre", "Nom"),
        new("Server", "服务器", "सर्वर", "Servidor", "Serveur"),
        new("Bereit", "就绪", "तैयार", "Listo", "Prêt"),
        new("Verbunden", "已连接", "कनेक्टेड", "Conectado", "Connecté"),
        new("Getrennt", "已断开", "डिस्कनेक्टेड", "Desconectado", "Déconnecté"),
        new("Passwort fehlt", "缺少密码", "पासवर्ड गायब है", "Falta la contraseña", "Mot de passe manquant"),
        new("Verbinde ...", "正在连接…", "कनेक्ट हो रहा है…", "Conectando…", "Connexion…"),
        new("Fehler", "错误", "त्रुटि", "Error", "Erreur"),
        new("Verbindung fehlgeschlagen", "连接失败", "कनेक्शन विफल", "Conexión fallida", "Échec de la connexion"),
        new("Verbindung getrennt", "连接已断开", "कनेक्शन डिस्कनेक्ट किया गया", "Conexión desconectada", "Connexion déconnectée"),
        new("Diese Verbindung ist nicht eingebunden", "此连接未挂载", "यह कनेक्शन माउंट नहीं है", "Esta conexión no está montada", "Cette connexion n'est pas montée"),
        new("Änderungen gespeichert", "更改已保存", "परिवर्तन सहेजे गए", "Cambios guardados", "Modifications enregistrées"),
        new("Speichern fehlgeschlagen", "保存失败", "सहेजना विफल", "Error al guardar", "Échec de l'enregistrement"),
        new("Neue Verbindung", "新建连接", "नया कनेक्शन", "Nueva conexión", "Nouvelle connexion"),
        new("Verbindung", "连接", "कनेक्शन", "Conexión", "Connexion"),
        new("Verbinden", "连接", "कनेक्ट करें", "Conectar", "Connecter"),
        new("Trennen", "断开", "डिस्कनेक्ट करें", "Desconectar", "Déconnecter"),
        new("Bearbeiten", "编辑", "संपादित करें", "Editar", "Modifier"),
        new("Ansehen", "查看", "देखें", "Ver", "Afficher"),
        new("Als Standard setzen", "设为默认", "डिफ़ॉल्ट के रूप में सेट करें", "Establecer como predeterminada", "Définir par défaut"),
        new("Duplizieren", "复制", "डुप्लिकेट करें", "Duplicar", "Dupliquer"),
        new("Löschen", "删除", "हटाएं", "Eliminar", "Supprimer"),
        new("Schließen", "关闭", "बंद करें", "Cerrar", "Fermer"),
        new("Verbindung ansehen", "查看连接", "कनेक्शन देखें", "Ver conexión", "Voir la connexion"),
        new("Die Verbindung ist aktiv. Zum Ändern zuerst trennen.", "连接已激活。请先断开后再修改。", "कनेक्शन सक्रिय है। बदलाव से पहले इसे डिसकनेक्ट करें।", "La conexión está activa. Desconéctala primero para hacer cambios.", "La connexion est active. Déconnectez-la avant de la modifier."),
        new("Verbindung löschen", "删除连接", "कनेक्शन हटाएं", "Eliminar conexión", "Supprimer la connexion"),
        new("Verbindung testen", "测试连接", "कनेक्शन जांचें", "Probar conexión", "Tester la connexion"),
        new("Anzeigename", "显示名称", "प्रदर्शित नाम", "Nombre mostrado", "Nom affiché"),
        new("Protokoll", "协议", "प्रोटोकॉल", "Protocolo", "Protocole"),
        new("Serveradresse", "服务器地址", "सर्वर पता", "Dirección del servidor", "Adresse du serveur"),
        new("Remote-Pfad", "远程路径", "रिमोट पथ", "Ruta remota", "Chemin distant"),
        new("Laufwerk", "驱动器", "ड्राइव", "Unidad", "Lecteur"),
        new("Benutzername", "用户名", "उपयोगकर्ता नाम", "Nombre de usuario", "Nom d'utilisateur"),
        new("Passwort", "密码", "पासवर्ड", "Contraseña", "Mot de passe"),
        new("Passwort eingeben, um es im Windows Credential Manager zu setzen", "输入密码以将其存储在 Windows 凭据管理器中", "Windows Credential Manager में सहेजने के लिए पासवर्ड दर्ज करें", "Introduce una contraseña para guardarla en el Administrador de credenciales de Windows", "Saisissez un mot de passe pour l’enregistrer dans le Gestionnaire d’identifiants Windows"),
        new("Schreibgeschützt", "只读", "केवल पढ़ने के लिए", "Solo lectura", "Lecture seule"),
        new("Beim Start des Programms verbinden", "程序启动时连接", "प्रोग्राम शुरू होने पर कनेक्ट करें", "Conectar al iniciar el programa", "Se connecter au démarrage du programme"),
        new("TLS-Zertifikat prüfen", "验证 TLS 证书", "TLS प्रमाणपत्र सत्यापित करें", "Verificar certificado TLS", "Vérifier le certificat TLS"),
        new("TLS verwenden (HTTPS/FTPS)", "使用 TLS（HTTPS/FTPS）", "TLS का उपयोग करें (HTTPS/FTPS)", "Usar TLS (HTTPS/FTPS)", "Utiliser TLS (HTTPS/FTPS)"),
        new("Synology NAS", "群晖 NAS", "Synology NAS", "Synology NAS", "Synology NAS"),
        new("WebDAV", "WebDAV", "WebDAV", "WebDAV", "WebDAV"),
        new("FTP", "FTP", "FTP", "FTP", "FTP"),
        new("SFTP", "SFTP", "SFTP", "SFTP", "SFTP"),
        new("SMB", "SMB", "SMB", "SMB", "SMB"),
        new("Über dDrive", "关于 dDrive", "dDrive के बारे में", "Acerca de dDrive", "À propos de dDrive"),
        new("Cloud Netzwerklaufwerk für Windows", "Windows 云网络驱动器", "Windows के लिए क्लाउड नेटवर्क ड्राइव", "Unidad de red en la nube para Windows", "Lecteur réseau cloud pour Windows"),
        new("Netzwerkspeicher", "网络存储", "नेटवर्क स्टोरेज", "Almacenamiento de red", "Stockage réseau"),
        new("dDrive verbindet deine Netzwerkdienste als Laufwerke in Windows.", "dDrive 将网络服务连接为 Windows 驱动器。", "dDrive आपके नेटवर्क सेवाओं को Windows ड्राइव के रूप में जोड़ता है।", "dDrive conecta tus servicios de red como unidades en Windows.", "dDrive connecte vos services réseau comme des lecteurs Windows."),
        new("WebDAV kann nativ genutzt werden; Rclone erweitert die unterstützten Protokolle.", "WebDAV 可原生使用；Rclone 扩展支持的协议。", "WebDAV का मूल रूप से उपयोग किया जा सकता है; Rclone समर्थित प्रोटोकॉल बढ़ाता है।", "WebDAV puede usarse de forma nativa; Rclone amplía los protocolos compatibles.", "WebDAV peut être utilisé nativement ; Rclone étend les protocoles pris en charge."),
        new("Zugangsdaten werden im Windows Credential Manager gespeichert.", "凭据存储在 Windows 凭据管理器中。", "क्रेडेंशियल Windows Credential Manager में संग्रहीत हैं।", "Las credenciales se guardan en el Administrador de credenciales de Windows.", "Les identifiants sont stockés dans le Gestionnaire d’identifiants Windows."),
        new("Unterstütze dDrive mit Buy Me a Coffee", "通过 Buy Me a Coffee 支持 dDrive", "Buy Me a Coffee से dDrive का समर्थन करें", "Apoya dDrive con Buy Me a Coffee", "Soutenez dDrive avec Buy Me a Coffee"),
        new("Globale Einstellungen", "全局设置", "वैश्विक सेटिंग्स", "Configuración global", "Paramètres globaux"),
        new("Settings", "设置", "सेटिंग्स", "Configuración", "Paramètres"),
        new("Global settings", "全局设置", "वैश्विक सेटिंग्स", "Configuración global", "Paramètres globaux"),
        new("Start with Windows", "随 Windows 启动", "Windows के साथ प्रारंभ करें", "Iniciar con Windows", "Démarrer avec Windows"),
        new("Minimiert starten", "最小化启动", "न्यूनतम करके प्रारंभ करें", "Iniciar minimizado", "Démarrer réduit"),
        new("Enable Rclone support", "启用 Rclone 支持", "Rclone समर्थन सक्षम करें", "Activar compatibilidad con Rclone", "Activer la prise en charge de Rclone"),
        new("Language", "语言", "भाषा", "Idioma", "Langue"),
        new("Color scheme", "配色方案", "रंग योजना", "Esquema de color", "Palette de couleurs"),
        new("Ozean", "海洋", "महासागर", "Océano", "Océan"),
        new("Wald", "森林", "वन", "Bosque", "Forêt"),
        new("Graphit", "石墨", "ग्रेफाइट", "Grafito", "Graphite"),
        new("Default website popup size (width × height)", "默认网站弹出窗口大小（宽 × 高）", "डिफ़ॉल्ट वेबसाइट पॉपअप आकार (चौड़ाई × ऊँचाई)", "Tamaño predeterminado de la ventana de sitio web (ancho × alto)", "Taille par défaut de la fenêtre du site web (largeur × hauteur)"),
        new("Width in pixels (300–2000)", "宽度（像素，300–2000）", "चौड़ाई पिक्सल में (300–2000)", "Ancho en píxeles (300–2000)", "Largeur en pixels (300–2000)"),
        new("Height in pixels (360–2000)", "高度（像素，360–2000）", "ऊँचाई पिक्सल में (360–2000)", "Alto en píxeles (360–2000)", "Hauteur en pixels (360–2000)"),
        new("Default website popup position", "默认网站弹出窗口位置", "डिफ़ॉल्ट वेबसाइट पॉपअप स्थिति", "Posición predeterminada de la ventana de sitio web", "Position par défaut de la fenêtre du site web"),
        new("Bottom right, above the taskbar", "右下角，任务栏上方", "नीचे दाएँ, टास्कबार के ऊपर", "Abajo a la derecha, sobre la barra de tareas", "En bas à droite, au-dessus de la barre des tâches"),
        new("Center of the screen", "屏幕中央", "स्क्रीन के बीच में", "Centro de la pantalla", "Centre de l’écran"),
        new("Save", "保存", "सहेजें", "Guardar", "Enregistrer"),
        new("Cancel", "取消", "रद्द करें", "Cancelar", "Annuler"),
        new("Ich habe gespendet (Dialog beim Start nicht mehr anzeigen)", "我已捐赠（启动时不再显示此对话框）", "मैंने दान किया है (स्टार्टअप पर यह संवाद फिर न दिखाएं)", "He donado (no mostrar este diálogo al iniciar)", "J'ai fait un don (ne plus afficher cette boîte au démarrage)"),
        new("Buy Me a Coffee", "请我喝咖啡", "मुझे कॉफी खरीदें", "Invítame a un café", "Offrez-moi un café"),
        new("Unterstütze dDrive mit Buy Me a Coffee", "通过 Buy Me a Coffee 支持 dDrive", "Buy Me a Coffee से dDrive का समर्थन करें", "Apoya dDrive con Buy Me a Coffee", "Soutenez dDrive avec Buy Me a Coffee"),
        new("GitHub-Projekt", "GitHub 项目", "GitHub प्रोजेक्ट", "Proyecto GitHub", "Projet GitHub"),
        new("Support and Ideas", "支持与想法", "समर्थन और विचार", "Soporte e ideas", "Support et idées"),
        new("dDrive öffnen", "打开 dDrive", "dDrive खोलें", "Abrir dDrive", "Ouvrir dDrive"),
        new("Beenden", "退出", "बाहर निकलें", "Salir", "Quitter"),
        new("Als Standardverbindung festlegen", "设为默认连接", "डिफ़ॉल्ट कनेक्शन के रूप में सेट करें", "Establecer como conexión predeterminada", "Définir comme connexion par défaut"),
        new("Standard", "默认", "डिफ़ॉल्ट", "Predeterminada", "Par défaut"),
        new("Icon", "图标", "आइकन", "Icono", "Icône"),
        new("Icon wählen …", "选择图标…", "आइकन चुनें …", "Elegir icono …", "Choisir l’icône …"),
        new("Icon zurücksetzen", "重置图标", "आइकन रीसेट करें", "Restablecer icono", "Réinitialiser l’icône"),
        new("Webseite im Browser öffnen", "在浏览器中打开网站", "वेबसाइट ब्राउज़र में खोलें", "Abrir sitio web en el navegador", "Ouvrir le site dans le navigateur"),
        new("Webseite mobil anzeigen", "以移动版显示网站", "वेबसाइट मोबाइल में दिखाएं", "Mostrar sitio web móvil", "Afficher le site en mode mobile"),
        new("Im Browser öffnen", "在浏览器中打开", "ब्राउज़र में खोलें", "Abrir en el navegador", "Ouvrir dans le navigateur"),
        new("dDrive Webansicht (Mobil)", "dDrive 移动网页视图", "dDrive मोबाइल वेब व्यू", "Vista web móvil de dDrive", "Vue web mobile de dDrive"),
        new("Laufwerk öffnen", "打开驱动器", "ड्राइव खोलें", "Abrir unidad", "Ouvrir le lecteur"),
        new("In dDrive anzeigen", "在 dDrive 中显示", "dDrive में दिखाएं", "Mostrar en dDrive", "Afficher dans dDrive"),
        new("Die Webansicht benötigt die Microsoft WebView2-Laufzeit. Die Seite wird stattdessen im Standardbrowser geöffnet.", "网页视图需要 Microsoft WebView2 运行时。页面将改为在默认浏览器中打开。", "वेब व्यू के लिए Microsoft WebView2 रनटाइम आवश्यक है। इसके बजाय पेज डिफ़ॉल्ट ब्राउज़र में खुलेगा।", "La vista web requiere el entorno de ejecución Microsoft WebView2. La página se abrirá en el navegador predeterminado.", "La vue web nécessite le runtime Microsoft WebView2. La page sera ouverte dans le navigateur par défaut.")
    ];

    public static void Apply(DependencyObject root, string language)
    {
        foreach (var element in Enumerate(root))
        {
            if (element is Window window)
            {
                window.Title = Translate(window.Title, language);
            }

            if (element is TitleBar titleBar)
            {
                titleBar.Title = Translate(titleBar.Title, language);
            }

            if (element is System.Windows.Controls.TextBlock textBlock)
            {
                textBlock.Text = Translate(textBlock.Text, language);
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
            {
                contentControl.Content = Translate(content, language);
            }

            if (element is Wpf.Ui.Controls.TextBox textBox)
            {
                textBox.PlaceholderText = Translate(textBox.PlaceholderText, language);
            }
        }
    }

    private static string Translate(string? value, string language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var sourceValue = EnglishOverrides.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal)).Key;
        var entry = Entries.FirstOrDefault(item =>
            string.Equals(item.German, value, StringComparison.Ordinal) ||
            string.Equals(item.Mandarin, value, StringComparison.Ordinal) ||
            string.Equals(item.Hindi, value, StringComparison.Ordinal) ||
            string.Equals(item.Spanish, value, StringComparison.Ordinal) ||
            string.Equals(item.French, value, StringComparison.Ordinal) ||
            string.Equals(item.German, sourceValue, StringComparison.Ordinal));
        if (entry is null)
        {
            return value;
        }

        return language switch
        {
            "German" => GermanOverrides.TryGetValue(value, out var german) ? german : entry.German,
            "Mandarin-Chinese" => entry.Mandarin,
            "Hindi" => entry.Hindi,
            "Spanish" => entry.Spanish,
            "French" => entry.French,
            _ => EnglishOverrides.TryGetValue(entry.German, out var english) ? english : entry.German
        };
    }

    public static string TranslateText(string value, string language) => Translate(value, language);

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }
}
