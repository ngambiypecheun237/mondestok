# Destock

Application Android 100 % hors-ligne de gestion de stock, développée en **.NET MAUI (XAML)** avec un moteur métier **C#** et une base de données **SQLite** locale.

## Fonctionnalités

- **Authentification** – Connexion avec rôles (administrateur / utilisateur), gestion des utilisateurs
- **Catalogue** – Produits, catégories, dépôts (avec dépôt par défaut)
- **Stock** – Mouvements d'entrée/sortie/ajustement, alertes de stock bas
- **Tableau de bord** – Vue synthétique, flux mensuels, top produits
- **Rapports** – Export CSV (inventaire et mouvements)
- **Scan code-barres** – Intégration BarcodeScanning.Native.Maui
- **Paramètres & données de démo** – Configuration locale

## Architecture

- **UI** : .NET MAUI XAML + CommunityToolkit.Mvvm
- **Moteur métier** : Services C# (Auth, Catalogue, Stock, Rapports, etc.)
- **Persistance** : SQLite via Microsoft.Data.Sqlite (WAL, clés étrangères)
- **Sécurité** : Mots de passe hachés avec Argon2
- **100 % hors-ligne** : Données stockées localement dans `AppDataDirectory/destock.db`

## Structure du projet

```
destok/
├── src/
│   ├── Destock.App/      # Application .NET MAUI Android (XAML + MVVM)
│   └── (moteur C# intégré dans Destock.App/Data & Services)
└── README.md
```

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Workload MAUI Android : `dotnet workload install maui-android`
- JDK 21 (requis par Android SDK)
- Android SDK / Android Emulator (ou appareil physique)

## Lancement en développement

```bash
cd src/Destock.App
dotnet build -f net10.0-android
```

## Compte par défaut

- **Utilisateur** : `admin`
- **Mot de passe** : `admin123`

À modifier dès la première connexion.

## Licence

MIT
