# Discord-Overlay

*[English](README.md) · Français*

Une application Windows en barre d'état qui garde automatiquement votre overlay
vocal **OBS Discord StreamKit** pointé sur le salon vocal où vous êtes
réellement. Changez de serveur ou de salon en plein live : l'overlay vous suit,
sans jamais éditer d'URL à la main.

> Projet renaissant. L'ancienne approche par fenêtre de capture DirectX 11 a été
> abandonnée au profit d'une app en barre d'état + OBS WebSocket, inspirée de
> [voice-channel-grabber](https://github.com/dichternebel/voice-channel-grabber),
> avec l'accent mis sur une installation sans friction et une correction :
> **les déplacements forcés sont maintenant détectés** (quand un modérateur vous
> déplace dans un autre salon, l'overlay se met bien à jour).

## Comment ça marche

1. L'app dialogue avec votre client Discord local via son canal IPC et écoute le
   salon vocal où vous êtes. Si Discord redémarre, l'IPC se reconnecte tout seul
   avec un backoff exponentiel.
2. Quand le salon change, elle construit l'URL Discord StreamKit correspondante
   et la pousse vers OBS via le protocole obs-websocket, en mettant directement
   à jour l'URL de votre Browser Source.
3. Un filet de sécurité par sondage rattrape les cas où Discord n'émet pas
   l'événement de changement de salon (typiquement les déplacements décidés par
   un modérateur).

Pas d'onglet de navigateur, pas de plugin OBS, aucune édition manuelle.

## Installation

1. Téléchargez `Discord-Overlay-win-Setup.exe` depuis la page
   [Releases](https://github.com/Kenshin9977/Discord-Overlay/releases).
2. Lancez-le. Il s'installe dans `%LocalAppData%\Discord-Overlay` (aucun droit
   administrateur requis) et démarre dans la barre d'état.
3. Le runtime .NET 10 est embarqué, rien d'autre à installer.

### « Windows a protégé votre PC » — c'est attendu, et voici pourquoi

Windows affichera très probablement un écran bleu **SmartScreen** au premier
lancement de l'installateur. **C'est normal pour une release récente et cela ne
veut pas dire que l'application est dangereuse.** Cliquez sur **Informations
complémentaires** puis **Exécuter quand même**.

Voici ce qui se passe réellement, pour que vous puissiez juger par vous-même :

- L'installateur **est** signé, avec un certificat Certum « Open Source
  Developer ». Windows affiche donc un éditeur nommé, et non *Éditeur inconnu*.
  Vérifiez-le : clic droit sur le `.exe` → **Propriétés** → **Signatures
  numériques** → **Détails**.
- SmartScreen n'avertit pas parce qu'un fichier n'est pas signé. Il avertit
  parce qu'un fichier n'est **pas encore connu**. La réputation se gagne au
  volume de téléchargements, dans le temps, et une release fraîchement signée
  part de zéro. Il n'existe aucun moyen de demander ni d'acheter une dispense —
  pas même avec un certificat plus cher.
- L'avertissement disparaît donc de lui-même à mesure que les gens installent,
  et il s'affiche sur chaque release tant que la réputation n'est pas acquise.

**Vérifier que le fichier vient bien d'ici.** L'empreinte SHA-1 du certificat de
signature est :

```
80C0A61E3A5E10199070235AE95A9A0DB6971A94
```

Dans PowerShell :

```powershell
(Get-AuthenticodeSignature .\Discord-Overlay-win-Setup.exe).SignerCertificate.Thumbprint
```

Si elle ne correspond pas, ou si la signature ne ressort pas comme `Valid`,
**n'exécutez pas** le fichier : il a été altéré, et il ne vient pas d'ici.

## Préparation d'OBS (une seule fois, avant de lancer l'app)

1. Dans **OBS**, allez dans **Outils** puis **Paramètres du serveur WebSocket**
   et cochez **Activer le serveur WebSocket**. Cliquez sur **Afficher les
   informations de connexion** et gardez cette fenêtre ouverte : le mot de passe
   vous servira à la configuration.
2. Dans votre scène, ajoutez une **Browser Source** nommée exactement
   **`Discord-Overlay`**. Largeur/hauteur au choix (par ex. 350x500). Laissez
   l'URL vide, l'app la remplit.

La Browser Source doit exister dans votre collection de scènes courante **avant**
que l'app tente de pousser l'URL : si elle est absente, OBS bloque la requête et
l'URL n'est jamais mise à jour.

## Première configuration

Au premier lancement, l'app ouvre la fenêtre **Paramètres** (la même que celle
accessible ensuite depuis le menu de la barre d'état). Elle a trois sections :
commencez par Discord — la section OBS et le bouton **Enregistrer** restent
désactivés tant que Discord n'est pas connecté.

### Discord

1. Cliquez sur **Ouvrir le portail développeur Discord**.
2. **New Application**, nommez-la (par ex. `Discord-Overlay`), créez-la.
3. **OAuth2** puis **Redirects** : ajoutez cette URI exacte (le bouton *Copier
   l'URI de redirection* la copie pour vous) :
   ```
   http://localhost:3000/callback
   ```
4. Copiez vos **Client ID** et **Client Secret** dans les champs.
5. Cliquez sur **Tester et sauver**. Discord affiche une fenêtre de consentement
   dans le client Discord lui-même ; cliquez sur **Autoriser**. La section
   Discord se réduit alors à une ligne d'état avec un bouton de déconnexion.

Les identifiants sont chiffrés via Windows DPAPI, liés à votre compte
utilisateur, dans `%LocalAppData%\DiscordOverlay\credentials.bin`.

### Connexion OBS

1. Confirmez l'hôte (`localhost`) et le port (`4455`).
2. Collez le mot de passe du WebSocket OBS récupéré à l'étape de préparation.
3. Vérifiez que le nom de la Browser Source correspond à celui créé dans OBS
   (`Discord-Overlay` par défaut).
4. Vous pouvez cliquer sur **Tester la connexion** — un statut vert signifie
   qu'OBS a accepté le mot de passe.

### Démarrage

Cochez **Démarrer avec Windows** si vous voulez que l'app se lance
silencieusement dans la barre d'état au démarrage, puis cliquez sur
**Enregistrer**. L'app redémarre une fois pour appliquer les réglages OBS.

La fenêtre s'ouvre automatiquement tant que Discord n'est pas connecté et que
les réglages OBS n'existent pas dans `settings.json` ; ensuite l'app démarre
silencieusement et vous retrouvez la même fenêtre via **Paramètres…** dans le
menu de la barre d'état.

## Menu de la barre d'état

- **Salon :** le salon vocal actuellement suivi.
- **OBS :** l'état de la connexion WebSocket.
- **Paramètres… :** déconnexion Discord, connexion OBS, démarrage automatique.
- **Rechercher des mises à jour :** récupère la dernière release Velopack depuis
  GitHub et redémarre pour l'appliquer.
- **Ouvrir le dossier des logs :** `%LocalAppData%\DiscordOverlay\logs\`
  (rotation quotidienne, conservation 14 jours).
- **Quitter :** arrêt propre.

## Démarrage automatique

Dans **Paramètres…**, cochez **Démarrer avec Windows (silencieusement dans la
barre d'état)**. Cela écrit une entrée
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointant sur le binaire
installé. Décocher la supprime.

## Fichier de configuration

Les réglages non sensibles vivent dans
`%LocalAppData%\DiscordOverlay\settings.json` (c'est la fenêtre Paramètres qui
l'écrit ; vous pouvez l'éditer à la main si vous voulez). Schéma :

```json
{
  "Obs": {
    "Hostname": "localhost",
    "Port": 4455,
    "Password": "...",
    "BrowserSourceName": "Discord-Overlay",
    "AutoReconnect": true
  },
  "Watcher": {
    "PollInterval": "00:00:05",
    "RefreshTimeout": "00:00:05"
  },
  "Streamkit": {
    "ShowIcon": true,
    "OnlineOnly": true,
    "Logo": "white",
    "TextColor": "#ffffff",
    "TextSize": 14,
    "BackgroundColor": "#1e2124",
    "BackgroundOpacity": 0,
    "LimitSpeaking": false,
    "SmallAvatars": false,
    "HideNames": false
  },
  "Update": {
    "GitHubRepository": "https://github.com/Kenshin9977/Discord-Overlay"
  }
}
```

Les changements d'hôte, de port, de mot de passe et de nom de Browser Source
prennent effet après un redémarrage de l'app, car la connexion WebSocket OBS
n'est établie qu'une fois au démarrage. Les options de l'overlay StreamKit, elles,
sont prises en compte à chaud.

## Dépannage

- **« OBS : Déconnecté » dans la barre d'état.** Vérifiez qu'OBS tourne et que
  le serveur WebSocket est activé (Outils → Paramètres du serveur WebSocket →
  Activer le serveur WebSocket). Vérifiez que le mot de passe dans Paramètres
  correspond.
- **La Browser Source reste vide ou bloquée sur `about:blank`.** La Browser
  Source nommée dans Paramètres (`Discord-Overlay` par défaut) doit exister dans
  votre collection de scènes OBS courante. Si vous la créez ou la renommez après
  le lancement, redémarrez l'app.
- **« Salon : Pas en vocal » alors que vous y êtes.** Vérifiez que Discord
  tourne. L'IPC Discord se reconnecte automatiquement avec backoff après un
  redémarrage de Discord ; consultez le log (barre d'état → Ouvrir le dossier
  des logs, cherchez `Discord IPC reconnect`) si ça reste bloqué.
- **L'assistant affiche « Discord a refusé ».** Le client Discord n'était
  probablement pas lancé, ou vous avez refusé la fenêtre de consentement.
  Assurez-vous que Discord est ouvert et réessayez.
- **L'assistant affiche « échec de l'échange du jeton OAuth : invalid_grant ».**
  L'URI de redirection dans le portail développeur Discord doit être exactement
  `http://localhost:3000/callback`. La casse et le slash final comptent.
- **Les changements de salon ont quelques secondes de retard.** C'est
  l'intervalle du sondage de sécurité (5 s par défaut). Baissez-le via
  `Watcher.PollInterval` si vous voulez un rattrapage plus rapide, au prix d'un
  peu plus de trafic IPC.
- **Logs :** menu de la barre d'état → Ouvrir le dossier des logs.

## Compiler depuis les sources

Prérequis :
- Windows 10 ou plus récent.
- [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). Un
  `global.json` épingle la version exacte.
- Pour le packaging Velopack uniquement : le runtime .NET 9
  (`Microsoft.NETCore.App` et `Microsoft.AspNetCore.App`). L'outil CLI `vpk`
  cible net9.0.

```powershell
git clone https://github.com/Kenshin9977/Discord-Overlay
cd Discord-Overlay
dotnet build
dotnet test

# Exe autonome en fichier unique : publish/win-x64/DiscordOverlay.exe
.\build\publish.ps1

# Idem, plus un Setup.exe Velopack dans Releases/
.\build\publish.ps1 -Pack -PackVersion 0.1.0
```

## Structure du projet

```
src/
  DiscordOverlay.App/             App WinForms en barre d'état (entrée, UI,
                                  hébergement)
    Hosting/                      Glue du Generic Host, barre d'état,
                                  dispatcher, AutoStartManager, AppUpdater
    Settings/                     Fenêtre unifiée Paramètres / première config
  DiscordOverlay.Core/            Bibliothèque sans UI
    Auth/                         Flux OAuth, stockage DPAPI, DiscordSession
                                  (avec reconnexion IPC automatique)
    Discord/                      Client IPC, surveillance du salon vocal
    Streaming/                    Constructeur d'URL StreamKit, mise à jour OBS,
                                  testeur de connexion OBS
tests/
  DiscordOverlay.Core.Tests/      Tests xUnit
build/
  publish.ps1                     dotnet publish + vpk pack
  sign-remote.sh                  signature par fichier (déléguée au VPS)
  vps/                            hôte de signature : signer, installeur, patchs
docs/
  SIGNING.md                      Configuration de la signature (Certum SimplySign)
```

Les binaires de release sont signés auprès du cloud Certum, via une commande SSH
forcée et verrouillée sur un hôte de signature, lorsque les secrets CI sont
configurés ; voir [docs/SIGNING.md](docs/SIGNING.md). En leur absence, les
releases restent vertes et non signées.

## Stack technique

- **.NET 10 LTS** avec C# `latest`, nullable + implicit usings activés, gestion
  centralisée des paquets.
- **WinForms** pour la barre d'état et les fenêtres (BCL, aucune dépendance UI
  supplémentaire).
- **Microsoft.Extensions.Hosting** Generic Host avec DI, options et services
  d'arrière-plan.
- **Serilog** avec sink fichier à rotation quotidienne + sink Debug.
- **System.Text.Json**, compatible générateurs de source.
- **System.IO.Pipes** (BCL) pour le transport IPC de Discord (named pipes).
- **System.Security.Cryptography.ProtectedData** (DPAPI) pour le chiffrement des
  identifiants.
- **OBSClient** (tinod) pour la communication obs-websocket v5.
- **Velopack** pour l'installateur et le canal de mise à jour automatique.

## Pourquoi ne pas utiliser directement l'URL Discord StreamKit ?

Les URL StreamKit codent en dur `guild_id` / `channel_id`. Dès que vous changez
de serveur ou de salon vocal, l'overlay pointe au mauvais endroit. Cette app est
le petit bout de glue qui met l'URL à jour automatiquement.

## Pourquoi avoir abandonné l'approche DirectX 11 ?

Le Discord-Overlay d'origine affichait une fenêtre D3D11 invisible pour que
Discord y dessine son overlay natif et qu'OBS puisse le capturer. Ça
fonctionnait, mais c'était sensible aux mises à jour de l'overlay Discord et à
la mise à l'échelle de l'écran. L'approche StreamKit + OBS WebSocket est plus
robuste, contient moins de code, et ne dépend pas de la disponibilité de
l'overlay in-game de Discord.

## Licence

MIT.
