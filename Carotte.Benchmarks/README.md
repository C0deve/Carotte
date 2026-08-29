# Carotte.Benchmarks 🥕⚡

Ce projet contient la suite complète de benchmarks de performance et d'allocations mémoire pour la bibliothèque **Carotte**, basée sur [BenchmarkDotNet](https://benchmarkdotnet.org/).

Il permet d'évaluer, de profiler et de prévenir les régressions de performance sur les chemins critiques (*hot paths*) de la bibliothèque : résolution de types de messages, dispatch de consommateurs, exécution des pipelines de middlewares, sérialisation JSON et analyse de topologie RabbitMQ au démarrage.

---

## 🚀 Exécution des Benchmarks

> **Important** : Les benchmarks doivent toujours être exécutés en configuration **Release** et sans débogueur attaché afin de garantir la validité des mesures.

### 1. Menu interactif / Lancement global

Exécuter le projet pour afficher le menu interactif `BenchmarkSwitcher` permettant de choisir les benchmarks à exécuter :

```bash
dotnet run -c Release --project Carotte.Benchmarks
```

### 2. Lister tous les benchmarks disponibles

Pour afficher la liste complète des benchmarks sans les exécuter :

```bash
dotnet run -c Release --project Carotte.Benchmarks -- --list flat
```

### 3. Exécuter un benchmark spécifique (par filtre)

Vous pouvez filtrer par classe ou par méthode grâce au flag `--filter` :

```bash
# Résolution de types de messages
dotnet run -c Release --project Carotte.Benchmarks -- --filter *MessageTypeResolver*

# Invocation et dispatch de consommateurs
dotnet run -c Release --project Carotte.Benchmarks -- --filter *ConsumerMediator*

# Pipelines de middlewares (Publisher / Consumer)
dotnet run -c Release --project Carotte.Benchmarks -- --filter *PipelineExecution*

# Sérialisation et désérialisation
dotnet run -c Release --project Carotte.Benchmarks -- --filter *Serialization*

# Scan d'assemblies et construction de topologie
dotnet run -c Release --project Carotte.Benchmarks -- --filter *TopologyScanning*
```

---

## 📊 Suites de Benchmarks

| Catégorie | Classe | Description & Scénarios couverts |
| :--- | :--- | :--- |
| **Résolution de Types** | `MessageTypeResolverBenchmarks` | Mesure du coût de résolution (`ResolveType`) et d'identification (`GetTypeIdentifier`).<br>• Formats : nom simple, `FullName`, alias `[MessageType]`, AQN (*Assembly Qualified Name*), URN (`urn:message:...`).<br>• Paramétrage : 1, 5 et 20 types candidats pour tester le cache de métadonnées. |
| **Médiation & Dispatch** | `ConsumerMediatorBenchmarks` | Évalue le dispatch des messages reçus vers les consommateurs enregistrés.<br>• Résolution du type de message depuis `BasicDeliverEventArgs`.<br>• Création d'Async Service Scope (`IServiceScope`).<br>• Invocation par réflexion (`MethodInfo.Invoke`) vs appel direct d'interface. |
| **Pipelines de Middlewares** | `PipelineExecutionBenchmarks` | Mesure de l'impact des middlewares sur l'émission et la réception de messages.<br>• Pipeline Publisher : Complet (Tracing + Metrics + Sérialisation) vs Minimal.<br>• Pipeline Consumer : Complet (Désérialisation + Tracing + Metrics + Invocation) vs Minimal. |
| **Sérialisation JSON** | `SerializationBenchmarks` | Mesure des débits et allocations mémoire pour différentes tailles de charge utile :<br>• **Small** : Message texte simple.<br>• **Medium** : Commande e-commerce avec plusieurs lignes d'articles.<br>• **Large** : Payload volumineux (64 KB de données brutes et métadonnées). |
| **Scan de Topologie** | `TopologyScanningBenchmarks` | Mesure des opérations au démarrage de l'application :<br>• Scan complet des assemblies vs scan avec filtre de namespace (`CarotteScanner`).<br>• Génération des topologies d'échanges et de files RabbitMQ avec ou sans surcharges d'options (`TopologyProvider`). |

---

## ⚙️ Configuration & Diagnostics

La suite utilise une configuration globale standardisée définie dans `Config/BenchmarkConfig.cs` :

* **Diagnostic Mémoire** : `MemoryDiagnoser` est activé sur l'ensemble des benchmarks pour rapporter précisément les allocations d'objets sur le tas managé (`Allocated`) et les passages du Garbage Collector (`Gen0`, `Gen1`, `Gen2`).
* **Exportateurs automatiques** :
  * **Markdown GitHub** : `BenchmarkDotNet.Artifacts/results/*-report-github.md` (idéal pour les Pull Requests).
  * **Markdown standard** : `BenchmarkDotNet.Artifacts/results/*-report.md`.
  * **JSON** : `BenchmarkDotNet.Artifacts/results/*-report-brief.json` (pour l'intégration et le suivi CI/CD).

---

## 📁 Structure du Projet

```text
Carotte.Benchmarks/
├── Benchmarks/
│   ├── ConsumerMediatorBenchmarks.cs      # Benchmarks d'invocation et de dispatch
│   ├── MessageTypeResolverBenchmarks.cs   # Benchmarks de résolution de types
│   ├── PipelineExecutionBenchmarks.cs     # Benchmarks de chaîne de middlewares
│   ├── SerializationBenchmarks.cs         # Benchmarks de sérialisation JSON
│   └── TopologyScanningBenchmarks.cs      # Benchmarks de démarrage et scan
├── Config/
│   └── BenchmarkConfig.cs                 # Configuration globale BenchmarkDotNet
├── Messages/
│   └── BenchmarkMessages.cs               # Modèles de messages et consommateurs de test
├── Program.cs                             # Point d'entrée BenchmarkSwitcher
├── Carotte.Benchmarks.csproj
└── README.md
```

---

## 🔄 Intégration dans la CI / GitHub Actions

Un workflow GitHub Actions automatisé est configuré dans [`.github/workflows/benchmarks.yml`](../.github/workflows/benchmarks.yml) pour surveiller en continu les performances des chemins critiques (`MessageTypeResolver`, `ConsumerMediator`, `PipelineExecution`).

### Déclencheurs du workflow
- **Push & Pull Requests** : Déclenché lors de modifications dans `Carotte/**` ou `Carotte.Benchmarks/**`.
- **Workflow Dispatch** : Déclenchement manuel à la demande avec personnalisation des filtres (`--filter`) et du profil d'exécution (`--job`).
- **Suivi des régressions** : Utilisation de `github-action-benchmark` pour comparer les performances par rapport à la branche `main` et archiver les rapports (JSON, Markdown, GitHub Summary).
