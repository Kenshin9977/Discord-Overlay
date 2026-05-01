using System.Globalization;
using System.Runtime.CompilerServices;

namespace DiscordOverlay.App.Resources;

/// <summary>
/// Localized user-facing strings. The active language is picked from
/// CultureInfo.CurrentUICulture (auto-set from the OS UI language).
/// Unsupported cultures fall back to English.
/// </summary>
internal static class Strings
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // App / common
        ["AppName"] = "Discord-Overlay",
        ["AppRestartMessage"] = "Settings saved. Discord-Overlay will now restart so OBS settings take effect.",

        // Wizard - Discord step
        ["WizardDiscordWindowTitle"] = "Discord-Overlay - Setup",
        ["WizardDiscordHeader"] = "One-time Discord setup",
        ["WizardDiscordInstructions"] =
            "Discord-Overlay needs its own Discord developer application. This is a one-time " +
            "setup that takes about 30 seconds.\r\n\r\n" +
            "1. Click \"Open Discord developer portal\" below.\r\n" +
            "2. Click \"New Application\", name it (e.g. Discord-Overlay), and create it.\r\n" +
            "3. In the new app, go to OAuth2 then Redirects and add this exact URL:\r\n" +
            "      {0}\r\n" +
            "4. Copy the Client ID (and reset/copy the Client Secret) into the fields below.\r\n" +
            "5. Click \"Test & save\". Discord will pop up a consent dialog inside the\r\n" +
            "   Discord client; click Authorize to finish.",
        ["WizardDiscordOpenPortalButton"] = "Open Discord developer portal",
        ["WizardDiscordCopyRedirectButton"] = "Copy redirect URI",
        ["WizardDiscordRedirectCopied"] = "Copied: {0}",
        ["WizardDiscordClientIdLabel"] = "Client ID:",
        ["WizardDiscordClientSecretLabel"] = "Client Secret:",
        ["WizardDiscordTestSaveButton"] = "Test && save",
        ["WizardDiscordCancelButton"] = "Cancel",
        ["WizardDiscordFieldsRequired"] = "Both Client ID and Client Secret are required.",
        ["WizardDiscordConnecting"] = "Connecting to Discord. Check the consent popup inside the Discord client.",
        ["WizardDiscordRpcError"] =
            "Discord said no: {0}\r\n" +
            "If you saw a consent popup, you may have denied it; if not, check that Discord is running.",
        ["WizardDiscordOAuthError"] =
            "OAuth token exchange failed: {0}\r\n" +
            "Verify the Client ID, Client Secret, and that the redirect URI is registered in the developer portal.",
        ["WizardDiscordSetupFailed"] = "Setup failed: {0}",

        // Wizard - OBS step
        ["WizardObsWindowTitle"] = "Discord-Overlay - Setup (step 2 of 2)",
        ["WizardObsHeader"] = "Connect to OBS",
        ["WizardObsInstructions"] =
            "Discord-Overlay drives an OBS Browser Source via the WebSocket server bundled with OBS 28+.\r\n\r\n" +
            "1. In OBS: Tools, WebSocket Server Settings, tick \"Enable WebSocket server\".\r\n" +
            "2. Click \"Show Connect Info\" in OBS to copy the Server Password and paste it below.\r\n" +
            "3. In your scene, add a Browser Source named exactly the value below (default \"Discord-Overlay\").\r\n" +
            "   Width/height as you like (e.g. 350x500). Leave the URL empty, this app fills it in.",
        ["WizardObsHostLabel"] = "Host:",
        ["WizardObsPortLabel"] = "Port:",
        ["WizardObsPasswordLabel"] = "Password:",
        ["WizardObsBrowserSourceLabel"] = "Browser source name:",
        ["WizardObsTestButton"] = "Test connection",
        ["WizardObsSkipButton"] = "Skip - configure later",
        ["WizardObsSaveButton"] = "Save && finish",
        ["WizardObsInitialStatus"] = "Click \"Test connection\" to verify your settings, then \"Save & finish\".",
        ["WizardObsRetestNeeded"] = "Settings changed. Re-test the connection.",
        ["WizardObsHostRequired"] = "Host is required.",
        ["WizardObsTesting"] = "Testing connection to OBS.",
        ["WizardObsTestSuccess"] = "Connected. Authentication accepted. Click \"Save & finish\" to continue.",
        ["WizardObsSaveFailed"] = "Save failed: {0}",

        // Settings dialog
        ["SettingsWindowTitle"] = "Discord-Overlay - Settings",
        ["SettingsDiscordHeader"] = "Discord",
        ["SettingsObsHeader"] = "OBS WebSocket",
        ["SettingsStartupHeader"] = "Startup",
        ["SettingsSignOutButton"] = "Sign out and reset credentials",
        ["SettingsObsHint"] = "In OBS: Tools, WebSocket Server Settings, Enable. Add a Browser Source named below.",
        ["SettingsHostLabel"] = "Host:",
        ["SettingsPortLabel"] = "Port:",
        ["SettingsPasswordLabel"] = "Password:",
        ["SettingsBrowserSourceLabel"] = "Browser source:",
        ["SettingsAutoStartCheckbox"] = "Start with Windows (silently in tray)",
        ["SettingsSaveButton"] = "Save",
        ["SettingsCancelButton"] = "Cancel",
        ["SettingsSignedIn"] = "Signed in via your developer app (client {0}).",
        ["SettingsNotSignedIn"] = "Not signed in. Run the setup wizard to connect to Discord.",
        ["SettingsSignOutPrompt"] = "Sign out and clear stored credentials? The app will exit; relaunch it to run setup again.",
        ["SettingsSignOutTitle"] = "Sign out",
        ["SettingsSignOutFailed"] = "Sign out failed: {0}",
        ["SettingsSaveSuccess"] = "Saved. Some changes (host/port/password) take effect after restart.",
        ["SettingsSaveFailed"] = "Save failed: {0}",
        ["SettingsAutoStartFailed"] = "Settings saved, but auto-start could not be updated: {0}",

        // Tray menu / status
        ["TrayInitialTooltip"] = "Discord-Overlay - starting",
        ["TrayChannelStarting"] = "Channel: starting",
        ["TrayObsStarting"] = "OBS: starting",
        ["TrayChannelFormat"] = "Channel: {0}",
        ["TrayObsFormat"] = "OBS: {0}",
        ["TrayTooltipFormat"] = "Discord-Overlay - {0} | OBS {1}",
        ["TrayMenuSettings"] = "Settings...",
        ["TrayMenuCheckUpdates"] = "Check for updates",
        ["TrayMenuOpenLogFolder"] = "Open log folder",
        ["TrayMenuQuit"] = "Quit",
        ["TrayChannelNotInVoice"] = "Not in voice",
        ["TrayChannelUnknown"] = "(unknown channel)",
        ["TrayObsConnected"] = "Connected",
        ["TrayObsConnecting"] = "Connecting",
        ["TrayObsDisconnected"] = "Disconnected",

        // Updates
        ["UpdatesNotInstalledMessage"] =
            "Updates are only available when Discord-Overlay is installed via Setup.exe (Velopack).\n\n" +
            "You appear to be running an unpacked or developer build.",
        ["UpdatesCheckingBalloon"] = "Checking for updates",
        ["UpdatesUpToDateBalloon"] = "You're up to date (v{0}).",
        ["UpdatesAvailablePrompt"] = "A new version is available: v{0}.\n\nDownload and restart now?",
        ["UpdatesAvailableTitle"] = "Discord-Overlay update",
        ["UpdatesCheckFailed"] = "Update check failed: {0}",
    };

    private static readonly IReadOnlyDictionary<string, string> Fr = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // App / common
        ["AppName"] = "Discord-Overlay",
        ["AppRestartMessage"] = "Paramètres enregistrés. Discord-Overlay va redémarrer pour appliquer la configuration OBS.",

        // Wizard - étape Discord
        ["WizardDiscordWindowTitle"] = "Discord-Overlay - Configuration",
        ["WizardDiscordHeader"] = "Configuration Discord (une seule fois)",
        ["WizardDiscordInstructions"] =
            "Discord-Overlay a besoin de sa propre application développeur Discord. C'est une " +
            "configuration unique qui prend environ 30 secondes.\r\n\r\n" +
            "1. Cliquez sur \"Ouvrir le portail développeur Discord\" ci-dessous.\r\n" +
            "2. Cliquez sur \"New Application\", nommez-la (par ex. Discord-Overlay), puis créez-la.\r\n" +
            "3. Dans la nouvelle app, allez dans OAuth2 puis Redirects et ajoutez cette URL exacte :\r\n" +
            "      {0}\r\n" +
            "4. Copiez le Client ID (et réinitialisez/copiez le Client Secret) dans les champs ci-dessous.\r\n" +
            "5. Cliquez sur \"Tester et sauver\". Discord affichera une fenêtre de consentement\r\n" +
            "   à l'intérieur du client Discord ; cliquez sur Authorize pour terminer.",
        ["WizardDiscordOpenPortalButton"] = "Ouvrir le portail développeur Discord",
        ["WizardDiscordCopyRedirectButton"] = "Copier l'URI de redirection",
        ["WizardDiscordRedirectCopied"] = "Copié : {0}",
        ["WizardDiscordClientIdLabel"] = "Client ID :",
        ["WizardDiscordClientSecretLabel"] = "Client Secret :",
        ["WizardDiscordTestSaveButton"] = "Tester et sauver",
        ["WizardDiscordCancelButton"] = "Annuler",
        ["WizardDiscordFieldsRequired"] = "Le Client ID et le Client Secret sont tous deux requis.",
        ["WizardDiscordConnecting"] = "Connexion à Discord. Vérifiez la fenêtre de consentement dans le client Discord.",
        ["WizardDiscordRpcError"] =
            "Discord a refusé : {0}\r\n" +
            "Si vous avez vu une fenêtre de consentement, vous l'avez peut-être refusée ; sinon, vérifiez que Discord est lancé.",
        ["WizardDiscordOAuthError"] =
            "L'échange du token OAuth a échoué : {0}\r\n" +
            "Vérifiez le Client ID, le Client Secret, et que l'URI de redirection est bien enregistrée dans le portail développeur.",
        ["WizardDiscordSetupFailed"] = "Échec de la configuration : {0}",

        // Wizard - étape OBS
        ["WizardObsWindowTitle"] = "Discord-Overlay - Configuration (étape 2 sur 2)",
        ["WizardObsHeader"] = "Connexion à OBS",
        ["WizardObsInstructions"] =
            "Discord-Overlay pilote une Browser Source d'OBS via le serveur WebSocket inclus dans OBS 28+.\r\n\r\n" +
            "1. Dans OBS : Outils, Paramètres du serveur WebSocket, cochez \"Activer le serveur WebSocket\".\r\n" +
            "2. Cliquez sur \"Afficher les infos de connexion\" dans OBS pour copier le mot de passe et collez-le ci-dessous.\r\n" +
            "3. Dans votre scène, ajoutez une Browser Source nommée exactement comme la valeur ci-dessous (par défaut \"Discord-Overlay\").\r\n" +
            "   Largeur/hauteur libres (par ex. 350x500). Laissez l'URL vide, l'app la remplira.",
        ["WizardObsHostLabel"] = "Hôte :",
        ["WizardObsPortLabel"] = "Port :",
        ["WizardObsPasswordLabel"] = "Mot de passe :",
        ["WizardObsBrowserSourceLabel"] = "Nom de la Browser Source :",
        ["WizardObsTestButton"] = "Tester la connexion",
        ["WizardObsSkipButton"] = "Ignorer - configurer plus tard",
        ["WizardObsSaveButton"] = "Enregistrer && terminer",
        ["WizardObsInitialStatus"] = "Cliquez sur \"Tester la connexion\" pour vérifier vos paramètres, puis \"Enregistrer et terminer\".",
        ["WizardObsRetestNeeded"] = "Paramètres modifiés. Retestez la connexion.",
        ["WizardObsHostRequired"] = "L'hôte est requis.",
        ["WizardObsTesting"] = "Test de connexion à OBS en cours.",
        ["WizardObsTestSuccess"] = "Connecté. Authentification acceptée. Cliquez sur \"Enregistrer et terminer\" pour continuer.",
        ["WizardObsSaveFailed"] = "Échec de l'enregistrement : {0}",

        // Paramètres
        ["SettingsWindowTitle"] = "Discord-Overlay - Paramètres",
        ["SettingsDiscordHeader"] = "Discord",
        ["SettingsObsHeader"] = "WebSocket OBS",
        ["SettingsStartupHeader"] = "Démarrage",
        ["SettingsSignOutButton"] = "Se déconnecter et réinitialiser les identifiants",
        ["SettingsObsHint"] = "Dans OBS : Outils, Paramètres du serveur WebSocket, Activer. Ajoutez une Browser Source du nom indiqué ci-dessous.",
        ["SettingsHostLabel"] = "Hôte :",
        ["SettingsPortLabel"] = "Port :",
        ["SettingsPasswordLabel"] = "Mot de passe :",
        ["SettingsBrowserSourceLabel"] = "Browser Source :",
        ["SettingsAutoStartCheckbox"] = "Démarrer avec Windows (silencieusement dans la barre d'état)",
        ["SettingsSaveButton"] = "Enregistrer",
        ["SettingsCancelButton"] = "Annuler",
        ["SettingsSignedIn"] = "Connecté via votre application développeur (client {0}).",
        ["SettingsNotSignedIn"] = "Non connecté. Lancez l'assistant de configuration pour vous connecter à Discord.",
        ["SettingsSignOutPrompt"] = "Se déconnecter et effacer les identifiants enregistrés ? L'app se fermera ; relancez-la pour reprendre la configuration.",
        ["SettingsSignOutTitle"] = "Déconnexion",
        ["SettingsSignOutFailed"] = "Échec de la déconnexion : {0}",
        ["SettingsSaveSuccess"] = "Enregistré. Certaines modifications (hôte/port/mot de passe) prennent effet au redémarrage.",
        ["SettingsSaveFailed"] = "Échec de l'enregistrement : {0}",
        ["SettingsAutoStartFailed"] = "Paramètres enregistrés, mais le démarrage automatique n'a pas pu être mis à jour : {0}",

        // Barre d'état
        ["TrayInitialTooltip"] = "Discord-Overlay - démarrage",
        ["TrayChannelStarting"] = "Salon : démarrage",
        ["TrayObsStarting"] = "OBS : démarrage",
        ["TrayChannelFormat"] = "Salon : {0}",
        ["TrayObsFormat"] = "OBS : {0}",
        ["TrayTooltipFormat"] = "Discord-Overlay - {0} | OBS {1}",
        ["TrayMenuSettings"] = "Paramètres...",
        ["TrayMenuCheckUpdates"] = "Rechercher des mises à jour",
        ["TrayMenuOpenLogFolder"] = "Ouvrir le dossier des logs",
        ["TrayMenuQuit"] = "Quitter",
        ["TrayChannelNotInVoice"] = "Pas en vocal",
        ["TrayChannelUnknown"] = "(salon inconnu)",
        ["TrayObsConnected"] = "Connecté",
        ["TrayObsConnecting"] = "Connexion",
        ["TrayObsDisconnected"] = "Déconnecté",

        // Mises à jour
        ["UpdatesNotInstalledMessage"] =
            "Les mises à jour ne sont disponibles que lorsque Discord-Overlay est installé via Setup.exe (Velopack).\n\n" +
            "Vous semblez exécuter une build non installée ou de développement.",
        ["UpdatesCheckingBalloon"] = "Recherche de mises à jour",
        ["UpdatesUpToDateBalloon"] = "Vous êtes à jour (v{0}).",
        ["UpdatesAvailablePrompt"] = "Une nouvelle version est disponible : v{0}.\n\nTélécharger et redémarrer maintenant ?",
        ["UpdatesAvailableTitle"] = "Mise à jour Discord-Overlay",
        ["UpdatesCheckFailed"] = "La recherche de mises à jour a échoué : {0}",
    };

    private static IReadOnlyDictionary<string, string> Active =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "fr" => Fr,
            _ => En,
        };

    private static string Get([CallerMemberName] string key = "") =>
        Active.TryGetValue(key, out var value) ? value : key;

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture,
            Active.TryGetValue(key, out var v) ? v : key, args);

    // App / common
    public static string AppName => Get();
    public static string AppRestartMessage => Get();

    // Wizard - Discord
    public static string WizardDiscordWindowTitle => Get();
    public static string WizardDiscordHeader => Get();
    public static string WizardDiscordInstructions => Get();
    public static string WizardDiscordOpenPortalButton => Get();
    public static string WizardDiscordCopyRedirectButton => Get();
    public static string WizardDiscordClientIdLabel => Get();
    public static string WizardDiscordClientSecretLabel => Get();
    public static string WizardDiscordTestSaveButton => Get();
    public static string WizardDiscordCancelButton => Get();
    public static string WizardDiscordFieldsRequired => Get();
    public static string WizardDiscordConnecting => Get();
    public static string WizardDiscordSetupFailed(string message) => Format(nameof(WizardDiscordSetupFailed), message);
    public static string WizardDiscordRpcError(string message) => Format(nameof(WizardDiscordRpcError), message);
    public static string WizardDiscordOAuthError(string message) => Format(nameof(WizardDiscordOAuthError), message);
    public static string WizardDiscordRedirectCopied(string url) => Format(nameof(WizardDiscordRedirectCopied), url);

    // Wizard - OBS
    public static string WizardObsWindowTitle => Get();
    public static string WizardObsHeader => Get();
    public static string WizardObsInstructions => Get();
    public static string WizardObsHostLabel => Get();
    public static string WizardObsPortLabel => Get();
    public static string WizardObsPasswordLabel => Get();
    public static string WizardObsBrowserSourceLabel => Get();
    public static string WizardObsTestButton => Get();
    public static string WizardObsSkipButton => Get();
    public static string WizardObsSaveButton => Get();
    public static string WizardObsInitialStatus => Get();
    public static string WizardObsRetestNeeded => Get();
    public static string WizardObsHostRequired => Get();
    public static string WizardObsTesting => Get();
    public static string WizardObsTestSuccess => Get();
    public static string WizardObsSaveFailed(string message) => Format(nameof(WizardObsSaveFailed), message);

    // Settings
    public static string SettingsWindowTitle => Get();
    public static string SettingsDiscordHeader => Get();
    public static string SettingsObsHeader => Get();
    public static string SettingsStartupHeader => Get();
    public static string SettingsSignOutButton => Get();
    public static string SettingsObsHint => Get();
    public static string SettingsHostLabel => Get();
    public static string SettingsPortLabel => Get();
    public static string SettingsPasswordLabel => Get();
    public static string SettingsBrowserSourceLabel => Get();
    public static string SettingsAutoStartCheckbox => Get();
    public static string SettingsSaveButton => Get();
    public static string SettingsCancelButton => Get();
    public static string SettingsNotSignedIn => Get();
    public static string SettingsSignOutPrompt => Get();
    public static string SettingsSignOutTitle => Get();
    public static string SettingsSaveSuccess => Get();
    public static string SettingsSignedIn(string clientId) => Format(nameof(SettingsSignedIn), clientId);
    public static string SettingsSignOutFailed(string message) => Format(nameof(SettingsSignOutFailed), message);
    public static string SettingsSaveFailed(string message) => Format(nameof(SettingsSaveFailed), message);
    public static string SettingsAutoStartFailed(string message) => Format(nameof(SettingsAutoStartFailed), message);

    // Tray
    public static string TrayInitialTooltip => Get();
    public static string TrayChannelStarting => Get();
    public static string TrayObsStarting => Get();
    public static string TrayMenuSettings => Get();
    public static string TrayMenuCheckUpdates => Get();
    public static string TrayMenuOpenLogFolder => Get();
    public static string TrayMenuQuit => Get();
    public static string TrayChannelNotInVoice => Get();
    public static string TrayChannelUnknown => Get();
    public static string TrayObsConnected => Get();
    public static string TrayObsConnecting => Get();
    public static string TrayObsDisconnected => Get();
    public static string TrayChannelFormat(string channel) => Format(nameof(TrayChannelFormat), channel);
    public static string TrayObsFormat(string state) => Format(nameof(TrayObsFormat), state);
    public static string TrayTooltipFormat(string channel, string obs) => Format(nameof(TrayTooltipFormat), channel, obs);

    // Updates
    public static string UpdatesNotInstalledMessage => Get();
    public static string UpdatesCheckingBalloon => Get();
    public static string UpdatesAvailableTitle => Get();
    public static string UpdatesUpToDateBalloon(string version) => Format(nameof(UpdatesUpToDateBalloon), version);
    public static string UpdatesAvailablePrompt(string version) => Format(nameof(UpdatesAvailablePrompt), version);
    public static string UpdatesCheckFailed(string message) => Format(nameof(UpdatesCheckFailed), message);
}
