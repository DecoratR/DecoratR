# DecoratR Source Generator – Analyse und Verbesserungsplan

> **Status (2026-09-16): umgesetzt.** Alle Phasen 0–5 sind in diesem Branch implementiert. Entscheidungen aus
> Abschnitt 6: (1) Attribute und `DecoratROptions` liegen in `DecoratR.Abstractions`, **ohne** Fallback-Generierung;
> (2) `TResponse`-Constraints werden vollständig unterstützt; (3) doppelte Service-Typen sind ein Error (DCTR007);
> (4) Records sind erlaubt, Werttypen nicht (DCTR010); (5) `AddDecoratR()` liegt in
> `Microsoft.Extensions.DependencyInjection`, ohne Obsolete-Weiterleitung; (6) Zielplattform ist nur das .NET 10 SDK,
> die Roslyn-Referenz ist auf 5.0.0 gepinnt. Abweichend vom Vorschlag in Abschnitt 5 wurde DCTR009 für die
> fehlende DI-Referenz vergeben; die Diagnostics DCTR004–DCTR013 sind in `architecture.md` und `docs/guide.md`
> dokumentiert. R2 (Pipeline pro Service-Typ in einem Durchgang) wurde zusammen mit R1 umgesetzt: Apply-Methoden
> transformieren jetzt `ServiceDescriptor → ServiceDescriptor`. Der Rest des Dokuments beschreibt den Zustand
> **vor** der Umsetzung und bleibt als Begründung erhalten.

Stand: 2026-09-16 · Branch `claude/source-generator-analysis-d61c0a` · Basis-Commit `0372356`

Dieses Dokument fasst die Analyse von `src/DecoratR.Generator` zusammen (Inkonsistenzen, Code Smells,
Korrektheits- und Performance-Probleme) und leitet daraus einen phasenweisen Umsetzungsplan ab.
Jedes Finding hat eine ID, einen Beleg (Datei:Zeile bzw. Probe-Szenario), eine Lösung, Priorität und Aufwand.

Aufwandsskala: **S** ≤ 2 h · **M** ≤ 1 Tag · **L** > 1 Tag

---

## 1. Zusammenfassung

Der Generator ist strukturell sauber aufgebaut (klare Trennung Detector / Metadata / Emitter, `ForAttributeWithMetadataName`
für Decorators, equatable Metadata-Modelle, funktionierendes Incremental-Caching des Outputs). Die Kernpfade
(Happy Path, Cross-Assembly, Stream-Pipeline) sind durch 67 Tests abgedeckt und grün.

Die Probleme liegen fast ausschließlich an den Rändern: **fehlende Validierung** führt dazu, dass der Generator in vielen
plausiblen Konstellationen **nicht kompilierbaren Code emittiert** oder **Handler/Decorators still verliert**. Die
bestehende Test-Suite kann das nicht sehen, weil sie den generierten Code nie kompiliert, sondern nur auf Teilstrings prüft.

| Schwere | Anzahl | Beispiele |
|---|---|---|
| Kritisch (generierter Code kompiliert nicht / Generator lädt nicht) | 5 | Roslyn-5.9-Referenz, Where-Klausel mit nur einer Constraint, Assembly-Name als Namespace, fehlende Decorator-Validierung, unzugreifbare Typen |
| Hoch (stilles Fehlverhalten) | 8 | Partial-Klassen doppelt registriert, Records nicht erkannt, Mehrfach-Interfaces, Tie-Break-Reihenfolge widerspricht Doku |
| Mittel (Inkonsistenzen) | 10 | DCTR001-Text, Location.None, Sortierung an 4 Stellen, Doku vs. Code |
| Code Smells | 11 | 95 % Duplikat in Detectors, 10-Parameter-Methode, tote Schleifen, 11× Header-Block |
| Performance | 4 Generator + 3 Runtime | doppelter Syntax-Scan, Referenz-Scan ohne Gate, Reflection pro Resolve |
| Tests / Infrastruktur | 6 | keine Kompilierprüfung, keine Incremental-Tests, Renovate bumpt Roslyn |

---

## 2. Vorgehen und Verifikation

1. Vollständiges Lesen aller Dateien in `src/DecoratR.Generator`, `src/DecoratR.Abstractions`, Tests, Benchmarks, Docs, CI.
2. Baseline: `dotnet build` (0 Warnungen) und `dotnet test` (67/67 grün).
3. Verifikation der Hypothesen mit temporären Probe-Tests, die den Generator ausführen **und den generierten Code
   anschließend kompilieren** (Referenzen: Runtime-Facades, `DecoratR.Abstractions`,
   `Microsoft.Extensions.DependencyInjection.Abstractions`). Die Probe-Tests wurden nach der Auswertung wieder entfernt.

### Ergebnis der Probe-Szenarien

| # | Szenario | Ergebnis | Finding |
|---|---|---|---|
| A | Handler als `partial class` (2 Deklarationen) | 2 Handler erkannt, `services.Add(typeof(H))` **zweimal** emittiert | H1 |
| B | Decorator `where TRequest : ICommand, IAuditable` (Metadata-Pfad) | Apply-Methode emittiert nur `where TRequest : global::ICommand` → **CS0314** | K2 |
| C | Decorator `where TRequest : IRequest, IAuditable` (Metadata-Pfad) | Apply-Methode emittiert nur `where TRequest : global::IAuditable` → **2× CS0314** | K2 |
| D | Handler und Decorator als `record` | **nicht erkannt**, zusätzlich DCTR001 mit falschem Attributnamen | H3, M1 |
| E | Assembly-Name `My-App.Web` | `namespace My-App.Web;` → **4 Compile-Fehler** | K3 |
| F | Handler implementiert `IRequestHandler<A,string>` und `IRequestHandler<B,int>` | nur `A` registriert, `B` **still verworfen** | H2 |
| G | Decorator mit `where TResponse : IResult` | für `IRequestHandler<Cmd,string>` angewendet → **CS0311** | K4 |
| H | `class Base : IRequestHandler<Cmd,string>` + `class Derived : Base` | **beide registriert** für denselben Service-Typ, keine Diagnostic | H4 |
| I | `private` nested Handler; `[Decorator]` auf nicht-generischer Klasse | Handler emittiert → **CS0122**; Decorator **still ignoriert** | K5, H7 |
| J | Decorator `where TRequest : IQuery<TResponse>` | Decorator **nie angewendet**, keine Diagnostic | H5 |
| K | `[Decorator]` ohne Handler-Interface; Typparameter `<TResponse, TRequest>` vertauscht | beides emittiert → **3× CS0311** | K4 |
| L | Unabhängige Änderung in anderer Datei (Incremental-Caching) | `SourceOutput: Cached` – **Caching funktioniert** | (positiv) |
| M | `internal record Cmd` in referenzierter Lib, Decorator im Host | Host emittiert `DecorateService<global::Cmd, ...>` → **CS0122** | K5 |
| N | Lokaler `Aaa.Dec` und referenzierter `Zzz.Dec`, gleiche `Order` | `Aaa` wird **innen** angewendet – Doku verspricht alphabetisch = außen | H6 |

---

## 3. Findings

### 3.1 Kritisch – Generator lädt nicht oder emittiert nicht kompilierbaren Code

#### K1 · Roslyn-Referenz 5.9.0 macht den Generator für .NET 8/9 SDKs unbrauchbar
- **Beleg:** `src/DecoratR.Generator/DecoratR.Generator.csproj:18` referenziert `Microsoft.CodeAnalysis.CSharp` 5.9.0.
  Commit `0372356` (Renovate-Bump) hob von 5.3.0 auf 5.9.0. `DecoratR.Abstractions` targetet `net8.0;net9.0;net10.0`,
  d. h. Konsumenten auf .NET 8/9 SDKs sind explizit Zielgruppe.
- **Problem:** Ein Generator, der gegen eine neuere `Microsoft.CodeAnalysis`-Version kompiliert ist als der laufende Compiler,
  wird vom Compiler mit **CS9057** übersprungen. Auf .NET 8 SDK (Roslyn 4.8–4.11) und .NET 9 SDK (4.12–4.14) wird
  `AddDecoratR()` schlicht nicht generiert. Der Fehler ist für Nutzer kaum diagnostizierbar.
- **Lösung:**
  1. Im Generator-Projekt auf die **niedrigste** benötigte Version pinnen. Die genutzten APIs
     (`ForAttributeWithMetadataName`, `GeneratorAttributeSyntaxContext`) existieren seit 4.3.1; empfohlen **4.8.0**
     (= .NET 8 SDK 8.0.100).
  2. `.github/renovate.json`: packageRule für `Microsoft.CodeAnalysis.*` im Generator-Projekt deaktivieren
     (Testprojekt darf neuer sein).
  3. CI-Matrix: Tests zusätzlich gegen die niedrigste unterstützte SDK laufen lassen (`global.json` pro Matrix-Eintrag).
  4. Doku: unterstützte SDK-Versionen im README nennen.
- **Priorität:** Kritisch · **Aufwand:** S

#### K2 · Apply-Methoden übernehmen nur eine einzige Constraint
- **Beleg:** `DecoratorRegistryEmitter.cs:126-140` (`EmitWhereClause`) nimmt die **erste** Constraint, die nicht
  `IRequest`/`IStreamRequest` ist, und verwirft alle anderen. Probe B und C → CS0314 im Metadata-Pfad.
- **Problem:** Jeder Decorator mit ≥ 2 Typ-Constraints oder mit `IRequest` + weiterer Constraint erzeugt in Library-Projekten
  nicht kompilierbaren Code. Sonder-Constraints (`class`, `struct`, `notnull`, `new()`) werden ebenfalls ignoriert.
- **Lösung:** Constraints des Typparameters vollständig übernehmen: `ITypeParameterSymbol.HasReferenceTypeConstraint`,
  `HasValueTypeConstraint`, `HasNotNullConstraint`, `HasUnmanagedTypeConstraint`, `ConstraintTypes`, `HasConstructorConstraint`
  in korrekter Reihenfolge (`class`/`struct` zuerst, `new()` zuletzt) emittieren. Sicherstellen, dass `IRequest`
  bzw. `IStreamRequest` immer enthalten ist (nötig für den Aufruf von `DecorateService`). Die Serialisierung ins
  Attribut (`RequestConstraintTypes`) muss die Sonder-Constraints mit aufnehmen (z. B. Präfix `!class`, `!new`).
- **Priorität:** Kritisch · **Aufwand:** M

#### K3 · Assembly-Name wird unsaniert als Namespace und Klassenpfad verwendet
- **Beleg:** `DecoratRIncrementalGenerator.cs:37` (`c.AssemblyName ?? "Unknown"`), verwendet in
  `HandlerRegistryEmitter.cs:12,65`, `DecoratorRegistryEmitter.cs:33,48`, `FullRegistrationEmitter.cs:33`. Probe E.
- **Problem:** Assembly-Namen mit `-`, Leerzeichen, führender Ziffer oder C#-Schlüsselwörtern (`My-App`, `1Foo`, `class`)
  sind gültige Assembly-Namen, aber ungültige Namespaces → Compile-Fehler in jedem Projekt mit solchem Namen.
- **Lösung:** Einen `IdentifierSanitizer` einführen (ungültige Zeichen → `_`, führende Ziffer → `_`-Präfix, Keywords
  per `SyntaxFacts.GetKeywordKind` → `@`-Präfix oder `_`-Suffix), einmal in der Pipeline berechnen
  (`Select`) und **denselben** Wert für Namespace, Registry-Klassenname und Attribut-Inhalt verwenden. Optional:
  `RootNamespace` aus `AnalyzerConfigOptionsProvider` (`build_property.RootNamespace`) bevorzugen.
- **Priorität:** Kritisch · **Aufwand:** S

#### K4 · Decorators werden nicht validiert
- **Beleg:** `DecoratorDetector.cs:12` (nur `TypeParameters.Length == 0` wird geprüft), `:28-33` (Interface nur für
  `isStream`-Erkennung), `:36` (`TypeParameters[0]` wird blind als `TRequest` angenommen). Probe G und K.
- **Problem:** Folgende Fälle emittieren kompilierende Aufrufe `DecorateService<Req, Resp, Dec<Req, Resp>>`, die dann
  fehlschlagen (CS0311):
  - Decorator implementiert weder `IRequestHandler<,>` noch `IStreamRequestHandler<,>`.
  - Typparameter sind vertauscht (`Dec<TResponse, TRequest>`) oder es gibt ≠ 2 Typparameter.
  - Constraints auf `TResponse` (`where TResponse : IResult`) werden ignoriert; der Decorator wird auf alle Handler angewendet.
- **Lösung:**
  1. Handler-Interface des Decorators bestimmen; daraus `iface.TypeArguments[0]`/`[1]` als `ITypeParameterSymbol` lesen
     und über `Ordinal` auf die Klassen-Typparameter abbilden. Kein Interface → **DCTR004** (Error), überspringen.
  2. Anzahl Typparameter ≠ 2 oder Typargumente des Interfaces sind nicht genau die beiden Klassen-Typparameter →
     **DCTR005** (Error), überspringen.
  3. `TResponse`-Constraints: Phase 1 **DCTR009** (Warning) und Decorator überspringen; Phase 4 vollständig unterstützen
     (Response-Hierarchie ebenfalls serialisieren und in `SatisfiesConstraints` matchen).
- **Priorität:** Kritisch · **Aufwand:** M (Validierung) / L (TResponse-Support)

#### K5 · Zugreifbarkeit von Typen wird nicht geprüft
- **Beleg:** `HandlerDetector.cs:29` prüft nur `IsAbstract/IsStatic/TypeParameters`. Probe I (private nested Handler →
  CS0122) und Probe M (`internal` Request-Typ in referenzierter Lib → CS0122 im Host).
- **Problem:** Der Guide behauptet, interne Handler und Decorators funktionierten cross-assembly. Das stimmt für die
  Handler-Klasse (via Registry), aber **nicht** für Request-/Response-Typen: der Host referenziert sie als Typargumente
  in `DecorateService<...>` und `ApplyXxx<...>`. Ebenso brechen `private`/`protected`/`file`-scoped Handler lokal.
- **Lösung:**
  - Lokal: effektive Sichtbarkeit ≥ `internal` fordern (alle Containing Types durchlaufen; `file`-Typen ausschließen),
    sonst **DCTR008** (Warning) und überspringen.
  - Metadata-Pfad: Request- und Response-Typ müssen `public` sein, wenn Decorators im Host angewendet werden sollen →
    **DCTR008** (Warning) mit klarer Meldung; Doku korrigieren.
  - Langfristig (Phase 4): Alternative, bei der die Library ihre Handler selbst dekoriert
    (`DecoratRDecoratorRegistry.ApplyAll(services, applyDelegates)`), damit interne Requests möglich werden.
- **Priorität:** Kritisch · **Aufwand:** S (Diagnostics) / L (Alternative)

### 3.2 Hoch – stilles Fehlverhalten

#### H1 · `partial class` Handler werden mehrfach registriert
- **Beleg:** `HandlerDetector.cs:14,25` – `CreateSyntaxProvider` feuert pro Deklaration, `GetDeclaredSymbol` liefert
  dasselbe Symbol → zwei identische `HandlerMetadata`. Probe A: `services.Add(typeof(H))` zweimal.
- **Problem zur Laufzeit:** `DecorateService` ersetzt nur den **ersten** passenden Descriptor
  (`DecoratorRegistryEmitter.cs:178-186`, `break`). `IServiceProvider.GetService` liefert den **letzten** Descriptor →
  der Aufrufer bekommt den **undekorierten** Handler. Kein Compile-Fehler, kein Hinweis.
- **Lösung:** Nur die erste `DeclaringSyntaxReference` verarbeiten
  (`symbol.DeclaringSyntaxReferences[0].GetSyntax(ct) == classDeclaration`) und zusätzlich nach `Collect()`
  deduplizieren. Kombiniert mit H4 und H8.
- **Priorität:** Hoch · **Aufwand:** S

#### H2 · Handler mit mehreren Handler-Interfaces verlieren alle bis auf das erste
- **Beleg:** `HandlerDetector.cs:36-58` und `StreamHandlerDetector.cs:36-58` – `return` in der Interface-Schleife. Probe F.
- **Lösung:** Alle passenden Interfaces sammeln und mehrere `HandlerMetadata` emittieren (Provider mit `SelectMany`),
  oder bewusst nur ein Interface erlauben und **Diagnostic** ausgeben. Empfehlung: alle registrieren – das ist DI-konform.
- **Priorität:** Hoch · **Aufwand:** S

#### H3 · `record`-Handler und `record`-Decorators werden nicht erkannt
- **Beleg:** Prädikate `node is ClassDeclarationSyntax` in `HandlerDetector.cs:14`, `StreamHandlerDetector.cs:14`,
  `DecoratRIncrementalGenerator.cs:25`. `RecordDeclarationSyntax` ist kein `ClassDeclarationSyntax`. Probe D.
- **Lösung:** Prädikat auf `TypeDeclarationSyntax` (`ClassDeclarationSyntax or RecordDeclarationSyntax`) erweitern und
  semantisch `symbol.TypeKind == TypeKind.Class` prüfen. `struct`/`record struct` bewusst ablehnen und mit
  **DCTR011** (Warning) melden. Guide („The handler is a concrete class") entsprechend präzisieren.
- **Priorität:** Hoch · **Aufwand:** S

#### H4 · Mehrere Handler für denselben Service-Typ ohne Diagnostic
- **Beleg:** Probe H (`Base` + `Derived`). Ebenso zwei unabhängige Klassen für `IRequestHandler<X,Y>`. Kein Check in
  Generator oder Emitter; `FullRegistrationEmitter.cs:240-252` dedupliziert nur für die Decorator-Anwendung.
- **Problem:** Wie bei H1: letzter Descriptor gewinnt, nur der erste ist dekoriert.
- **Lösung:** Im Full-Pfad lokale + referenzierte Service-Typen auf Duplikate prüfen → **DCTR007** (Error) mit beiden
  Handler-Namen. Im Metadata-Pfad lokal prüfen.
- **Priorität:** Hoch · **Aufwand:** S

#### H5 · Constraints mit Typparametern (`IQuery<TResponse>`) matchen nie
- **Beleg:** `DecoratorDetector.cs:48-49` serialisiert `global::IQuery<TResponse>` (Name des Typparameters);
  `FullRegistrationEmitter.cs:319-334` vergleicht Strings gegen `global::IQuery<string>`. Probe J: Decorator nie angewendet.
- **Problem:** Das CQRS-typische Muster `IQuery<TResult>` + `where TRequest : IQuery<TResponse>` ist damit stillschweigend
  wirkungslos.
- **Lösung:** Constraint-Strings als Templates speichern (Typparameter durch Platzhalter `{TRequest}`/`{TResponse}`
  ersetzen) und beim Matching mit den konkreten Request-/Response-Namen substituieren. Für lokale Decorators zusätzlich
  symbolbasiert prüfbar (`Compilation.ClassifyConversion`), Cross-Assembly bleibt string-basiert. Zwischenlösung:
  **DCTR009** (Warning), wenn eine Constraint Typparameter enthält.
- **Priorität:** Hoch · **Aufwand:** M

#### H6 · Tie-Break-Reihenfolge widerspricht der Dokumentation (lokal vs. referenziert)
- **Beleg:** `FullRegistrationEmitter.cs:263-277` sortiert lokale Decorators nach `DecoratorFullyQualifiedName`
  (`global::Aaa.Dec`) und referenzierte nach `ApplyMethodName` (`Lib.DecoratRDecoratorRegistry.ApplyZzz_Dec`).
  Ordinal ist `L` < `g`, daher landen referenzierte Decorators bei gleichem `Order` immer vor lokalen. Probe N.
  README/Guide versprechen „alphabetisch nach fully qualified type name".
- **Lösung:** Decorator-FQN zusätzlich im Registrierungsattribut ablegen (`DecoratorTypeName`), `ReferencedDecoratorInfo`
  entsprechend erweitern und überall denselben Sortierschlüssel verwenden (ein `DecoratorOrderComparer`). Fallback
  für alte Metadaten: `ApplyMethodName` rückrechnen.
- **Priorität:** Hoch · **Aufwand:** S (+ Metadaten-Format, siehe Phase 4)

#### H7 · `[Decorator]` auf nicht-generischer Klasse wird still ignoriert
- **Beleg:** `DecoratorDetector.cs:12` `return null` ohne Diagnostic. Probe I.
- **Lösung:** **DCTR006** (Warning) „Decorator muss open generic sein“.
- **Priorität:** Hoch · **Aufwand:** S

#### H8 · Laufzeit-Dekoration ist nicht robust
- **Beleg:** `DecoratorRegistryEmitter.cs:172-211` (`EmitDecorateLogic`).
- **Probleme:** (a) nur der erste Descriptor wird dekoriert (siehe H1/H4); (b) Keyed Services (`IsKeyedService`) werden nicht
  übersprungen – Zugriff auf `ImplementationFactory`/`ImplementationType` eines keyed Descriptors wirft
  `InvalidOperationException`; (c) `wrappedDescriptor.ImplementationType!` ist ein Null-Forgiving auf einen ggf. leeren Wert.
- **Lösung:** Alle passenden, nicht-keyed Descriptoren dekorieren (Rückwärts-Iteration), keyed überspringen, klare
  Exception, falls kein Descriptor gefunden. Zusammen mit R1 umsetzen, da dieselbe Methode betroffen ist.
- **Priorität:** Hoch · **Aufwand:** S

### 3.3 Mittel – Inkonsistenzen

| ID | Problem | Beleg | Lösung | Aufwand |
|---|---|---|---|---|
| M1 | DCTR001-Text nennt immer `[GenerateDecoratRMetadata]`, wird aber auch im Registrations-Pfad gemeldet | `Diagnostics.cs:12`, `DecoratRIncrementalGenerator.cs:224-226`, Probe D | Attributname als Format-Argument übergeben oder zwei Descriptoren | S |
| M2 | Alle Diagnostics mit `Location.None` → nicht anklickbar; Info-Diagnostics DCTR002/003 sind Build-Rauschen | `Diagnostics.cs`, `DecoratRIncrementalGenerator.cs:183-234` | Location des Assembly-Attributs als equatable `LocationInfo` durch die Pipeline reichen; DCTR002/003 auf `Hidden` oder nur bei `EmitCompilerGeneratedFiles` | S |
| M3 | Sortierung an vier Stellen und mehrfach: Metadata-Pfad `SortDecorators` → `MergeDecorators` sortiert erneut; Handler im Metadata-Pfad im Emit sortiert, im Full-Pfad davor; `EmitDecoratorApplicationsCore` sortiert bereits sortierte Listen | `DecoratRIncrementalGenerator.cs:94-96,132-135,193-194,247-264`, `FullRegistrationEmitter.cs:257-277` | Einmal in der Pipeline sortieren (`Select` → `EquatableArray<T>`), Emitter erhalten sortierte Eingaben; ein gemeinsamer Comparer | S |
| M4 | Doku widerspricht Code: `DecoratorAttribute.Order` sagt „discovery order“, README „alphabetisch“; `architecture.md` „Five bootstrap attributes“ (es sind 7); copilot-instructions „sealed record types throughout“ (Metadata-Typen sind handgeschriebene Klassen); Guide behauptet Cross-Assembly-Support für interne Typen (gilt nicht für Requests) | `DecoratorAttribute.cs:12`, `architecture.md`, `.github/copilot-instructions.md`, `docs/guide.md` | Doku angleichen, nachdem H6/K5 entschieden sind | S |
| M5 | `HandlerMetadata` wird für referenzierte Service-Typen mit `HandlerFullyQualifiedName = ""` missbraucht | `ReferencedAssemblyScanner.cs:42,58` | Eigener Typ `ServiceTypeInfo(Request, Response, Hierarchy)`; `HandlerMetadata` enthält `ServiceTypeInfo` | S |
| M6 | Marker-Strings `"global::DecoratR.IRequest"`/`IStreamRequest` an 6 Stellen, `"global::"` an 3 Stellen, dazu `OrdinalIgnoreCase`-Vergleich für einen konstanten Kleinbuchstaben-Präfix | `DecoratorRegistryEmitter.cs:100,118,132,218,234,236`, `FullRegistrationEmitter.cs:285,327`, `StringBuilderExtensions.cs:24` | `WellKnownTypeNames`-Klasse; Helper `IsMarkerInterface(string, bool isStream)`; `Ordinal` | S |
| M7 | Generierte öffentliche Typen (`DecoratRHandlerRegistry`, `DecoratRDecoratorRegistry`, `DecoratRServiceCollectionExtensions`) landen im Namespace der Assembly und werden Teil der öffentlichen API des Nutzers; `DecoratROptions` ist `public` in jedem Host → CS0433, wenn zwei Hosts sich referenzieren | `HandlerRegistryEmitter.cs:65-70`, `AttributeEmitter.cs:146` | Registries mit `[EditorBrowsable(Never)]` + `[Obsolete("Infrastructure")]`-frei halten; `DecoratROptions` nach `DecoratR.Abstractions` verschieben (Breaking) oder `internal` mit `internal AddDecoratR` | M |
| M8 | Bootstrap-Attribute werden als `internal` in **jede** Assembly generiert; bei `InternalsVisibleTo` zwischen zwei DecoratR-Projekten entsteht CS0436 (mit `TreatWarningsAsErrors` in CI ein Fehler) | `AttributeEmitter.cs`, `DecoratRIncrementalGenerator.cs:45-64` | Attribute in `DecoratR.Abstractions` definieren (public); Generator emittiert sie nur noch, wenn `compilation.GetTypeByMetadataName("DecoratR.GenerateDecoratRMetadataAttribute") is null` | M |
| M9 | Implizite Abhängigkeit des generierten Codes auf `Microsoft.Extensions.DependencyInjection.Abstractions` (`IServiceCollection`, `ActivatorUtilities`) wird weder deklariert noch geprüft → kryptische CS0234 im Host | `FullRegistrationEmitter.cs`, `DecoratR.Generator.csproj` (`SuppressDependenciesWhenPacking`) | **DCTR010** (Error), wenn `Microsoft.Extensions.DependencyInjection.IServiceCollection` nicht auflösbar; README-Hinweis | S |
| M10 | `RequestTypeHierarchy` enthält `IRequest`/`IStreamRequest` selbst und bei Records `System.IEquatable<T>` → unnötig große Assembly-Attribute | `HandlerDetector.cs:63-84` | Marker-Interfaces herausfiltern (werden in `SatisfiesConstraints` ohnehin gesondert behandelt) | S |

### 3.4 Code Smells und Wartbarkeit

| ID | Smell | Beleg | Lösung | Aufwand |
|---|---|---|---|---|
| S1 | `HandlerDetector` und `StreamHandlerDetector` sind zu ~95 % identisch; der `DecoratorAttribute`-Check ist 2× dupliziert, der `DecoratR`-Namespace-Check 3× | `HandlerDetector.cs`, `StreamHandlerDetector.cs`, `DecoratorDetector.cs` | Ein `HandlerDetector`, der pro Klasse beide Interface-Arten prüft und `HandlerMetadata` mit `HandlerKind`/`IsStream` liefert; `SymbolExtensions.IsDecoratRType(symbol, metadataName)` | M |
| S2 | Long Parameter List: `FullRegistrationEmitter.Generate` hat 10 Parameter, `EmitFullRegistrations` 7, `RegisterFullPath` 6; 7-fach verschachtelte Tupel-Dekonstruktion | `FullRegistrationEmitter.cs:8-18`, `DecoratRIncrementalGenerator.cs:91,129` | `GenerationModel` (Local/Referenced × Regular/Stream) als equatable Record; Pipeline liefert ein Objekt | M |
| S3 | Toter/redundanter Code: Schleifen, die konstruktionsbedingt immer dasselbe ergeben, davon eine doppelt; redundante Bedingung; ungenutzte Helfer | `FullRegistrationEmitter.cs:84-94`, `:75-76`; `StringBuilderExtensions.cs:16-20` (`indent` ungenutzt, Methode ungenutzt); `EquatableArray.cs:12-15,21-30` (`AsImmutableArray`, `Contains` ungenutzt) | Entfernen; `hasLocalRegularDecorators = localDecorators.Count > 0` | S |
| S4 | Duplikation im Emit: Header-Block 11×; `AppendSemicolonDelimited` 2×; Handler-/Stream-Handler-Blöcke in `HandlerRegistryEmitter` 2× (Z. 28-62 vs. 73-136); Attribut-Paare `HandlerServiceType`/`StreamHandlerServiceType` und `DecoratorRegistration`/`StreamDecoratorRegistration` identisch bis auf den Namen | `AttributeEmitter.cs`, `HandlerRegistryEmitter.cs`, `DecoratorRegistryEmitter.cs:242-249` | `SourceWriter.AppendFileHeader()`; ein Helper für Listen; `EmitHandlerBlock(kind)`; ein Attribut mit `IsStream`-Property (Metadaten-Format v2, Phase 4) | M |
| S5 | Gemischte Einrück-Strategie: `AppendIndentedLine(2, …)` neben literalen `"        "`-Präfixen | alle Emitter, z. B. `FullRegistrationEmitter.cs:166-174` | `IndentedStringBuilder`/`CodeWriter` mit `Indent()`/`Dedent()`-Scopes; keine literalen Leerzeichen | M |
| S6 | `GenerateOptions` baut den Quelltext aus drei String-Fragmenten mit `+` zusammen und enthält eine Zeile aus reinen Leerzeichen; Options gehören nicht in einen „AttributeEmitter“ | `AttributeEmitter.cs:127-155` (Z. 142) | Interpolierter Raw-String `$$"""…"""`; eigener `OptionsEmitter` | S |
| S7 | Vier handgeschriebene `Equals`/`GetHashCode`-Implementierungen mit zwei verschiedenen Hash-Stilen; `ReferencedRegistrationData` hält `ImmutableArray` statt `EquatableArray` und wirft bei `default` in `Equals` | `DecoratorMetadata.cs`, `HandlerMetadata.cs`, `ReferencedDecoratorInfo.cs`, `ReferencedRegistrationData.cs` | `PolySharp` (liefert `IsExternalInit` für netstandard2.0) und `sealed record`/`readonly record struct`; alle Sammlungen als `EquatableArray<T>`; `static Empty` | S |
| S8 | Open-Generic-Name per `IndexOf('<')` abgeschnitten – bricht bei generischen Containing-Types (`Outer<T>.Dec<,>`) | `DecoratorDetector.cs:22-24` | `SymbolDisplayFormat.FullyQualifiedFormat.WithGenericsOptions(SymbolDisplayGenericsOptions.None)` | S |
| S9 | Versionsstring wird in CI per `sed` in `Version.cs` gepatcht | `Version.cs`, `.github/workflows/release.yml` | `typeof(DecoratRIncrementalGenerator).Assembly.GetName().Version` bzw. `AssemblyInformationalVersionAttribute` zur Laufzeit lesen; `/p:Version` reicht dann aus | S |
| S10 | `ReferencedAssemblyScanner` matcht Attribute nur über `AttributeClass.Name` (Namespace ignoriert); 5-fache `if/else`-Kette mit wiederholtem Argument-Parsing | `ReferencedAssemblyScanner.cs:29-67` | Namespace `DecoratR` prüfen; `switch` auf Name + kleine Parse-Helfer pro Attributtyp | S |
| S11 | Zwei Quellen der Wahrheit für den Decorator-Ausschluss: Handler-Detector prüft `DecoratorAttribute` per Attribut-Scan, Decorator-Detector über `ForAttributeWithMetadataName` | `HandlerDetector.cs:41-46` | Nach S1 zentraler Helper; alternativ Handler mit `[Decorator]` als Diagnostic melden statt still auszuschließen | S |

### 3.5 Performance

#### Generator / IDE

| ID | Problem | Beleg | Lösung | Aufwand |
|---|---|---|---|---|
| P1 | Zwei `CreateSyntaxProvider` über **alle** `ClassDeclarationSyntax` → pro Klasse 2× `GetDeclaredSymbol` + 2× `AllInterfaces`. Das Prädikat filtert nichts Syntaktisches. Kommentar „both paths reuse the same syntax tree scan“ ist irreführend | `DecoratRIncrementalGenerator.cs:18-20`, `HandlerDetector.cs:12-18`, `StreamHandlerDetector.cs:12-18` | Ein Provider (S1). Syntaktischer Vorfilter: `BaseList is { Types.Count: > 0 }`, keine `abstract`/`static` Modifier, `TypeParameterList is null`. Erst dann Semantik | S |
| P2 | `ReferencedAssemblyScanner.Scan` läuft bei **jeder** Compilation-Änderung, auch in reinen Metadata-Bibliotheken, und decodiert die Assembly-Attribute **aller** Referenzen inkl. BCL | `DecoratRIncrementalGenerator.cs:116-117`, `ReferencedAssemblyScanner.cs:23-27` | (a) Gate: `hasFullAttribute.Combine(CompilationProvider).Select(...)` mit Early-Return `ReferencedRegistrationData.Empty`; (b) pro Assembly Vorfilter `assembly.GetTypeByMetadataName("DecoratR.DecoratRHandlerRegistrationAttribute") is null && …DecoratorRegistrationAttribute is null → continue` | S |
| P3 | Sortieren/Kopieren/Mergen im Output-Callback statt in gecachten Pipeline-Stufen (siehe M3); `MergeDecorators` sortiert zwei bereits sortierte Arrays komplett neu | `DecoratRIncrementalGenerator.cs:142-168,247-264` | Sortierung in `Select`-Stufen, Ergebnis als `EquatableArray<T>` | S |
| P4 | `EmitDecoratorApplicationsCore` baut pro Aufruf Dictionary + `HashSet` pro Service-Typ + Tupel-Liste und sortiert erneut | `FullRegistrationEmitter.cs:236-277` | Sortierung entfernen (Eingaben sind sortiert); Rest ist für Generator-Skala akzeptabel | S |

**Positiv:** Probe L zeigt, dass der Output-Schritt bei unabhängigen Änderungen `Cached` bleibt. Die Metadata-Modelle
sind korrekt equatable; das Design ist grundsätzlich incremental-tauglich.

#### Laufzeit (generierter Code)

| ID | Problem | Beleg | Lösung | Aufwand |
|---|---|---|---|---|
| R1 | Pro Auflösung eines (transienten) Handlers wird für **jeden** Decorator und den Handler `ActivatorUtilities.CreateInstance(provider, Type, …)` aufgerufen → reflektionsbasierte Konstruktor-Auswahl bei jedem Resolve, multipliziert mit der Pipeline-Tiefe | `DecoratorRegistryEmitter.cs:200-208` | Einmalig pro Registrierung `ActivatorUtilities.CreateFactory(typeof(TDecorator), [typeof(IRequestHandler<TRequest,TResponse>)])` erzeugen und den `ObjectFactory`-Delegate im Closure halten; ebenso für den inneren Handler (`CreateFactory(ImplementationType, [])`). Ein Reflection-Durchlauf beim Start, danach Delegate-Aufruf. AOT-Annotationen bleiben erhalten | S |
| R2 | `DecorateService` durchsucht `services` linear pro (Service-Typ × Decorator) beim Start: O(n · h · d) | `DecoratorRegistryEmitter.cs:178-186` | Pro Service-Typ Descriptor einmal suchen und alle Decorators in einem Durchgang wrappen (Apply-Methoden als `ServiceDescriptor → ServiceDescriptor`-Transformation statt Collection-Mutation) | M |
| R3 | Für jede referenzierte Registry werden zwei `foreach` (Handlers, StreamHandlers) emittiert, auch wenn eine Liste leer ist | `FullRegistrationEmitter.cs:113-153` | Optional: Registry-Attribut trägt Zähler; vernachlässigbar | S |

### 3.6 Tests, Infrastruktur, Distribution

| ID | Problem | Beleg | Lösung | Aufwand |
|---|---|---|---|---|
| T1 | Tests kompilieren den generierten Code **nie** (nur `Contains`-Assertions) → K2–K5 unsichtbar | `tests/…/Infrastructure/GeneratorTestBase.cs:38-46` | `RunGenerator` gibt `outputCompilation` zurück und asserted `GetDiagnostics()` ohne Errors; `Microsoft.Extensions.DependencyInjection.Abstractions` als Referenz; `Verify.SourceGenerators` für Snapshots | M |
| T2 | Keine Incremental-Tests (`trackIncrementalGeneratorSteps`) → Caching-Regressionen unbemerkt | – | Test analog Probe L: unabhängige Änderung → alle Output-Schritte `Cached`; Handler-Änderung → nur betroffene Schritte `Modified` | S |
| T3 | Kein Laufzeit-/Integrationstest (Container bauen, Handler auflösen, Reihenfolge prüfen) → H1/H4/H8 unbemerkt | – | Testprojekt, das den Generator als Analyzer referenziert (`OutputItemType="Analyzer"`) und die Pipeline mit `ServiceCollection` real ausführt; Reihenfolge über Trace-Liste verifizieren | M |
| T4 | Edge-Cases fehlen komplett (partial, record, Mehrfach-Interfaces, Constraints, Sichtbarkeit, Assembly-Name, Tie-Break) | – | Probe-Szenarien A–N als Regressionstests übernehmen | M |
| T5 | Renovate bumpt `Microsoft.CodeAnalysis.CSharp` im Generator-Projekt (Ursache K1) | `.github/renovate.json`, Commit `0372356` | packageRule: `matchPackageNames: ["Microsoft.CodeAnalysis.*"]`, `matchFileNames: ["src/**"]`, `enabled: false` | S |
| T6 | `#pragma warning disable RS2008` statt Release-Tracking; Diagnostics ohne `helpLinkUri` | `Diagnostics.cs:5` | `AnalyzerReleases.Shipped.md`/`Unshipped.md` anlegen; `helpLinkUri` auf `docs/guide.md#diagnostics` | S |

---

## 4. Umsetzungsplan

Die Phasen sind so geschnitten, dass jede Phase einzeln mergebar ist und die nachfolgende absichert.

### Phase 0 – Absicherung (vor jeder Änderung am Generator)
Ziel: Bugs sichtbar machen, Distribution reparieren.

| Schritt | Findings | Abhängigkeit |
|---|---|---|
| 0.1 Roslyn auf 4.8.0 pinnen, Renovate-Regel, SDK-Matrix in CI | K1, T5 | – |
| 0.2 `GeneratorTestBase` kompiliert generierten Code und liefert `outputCompilation` | T1 | – |
| 0.3 Regressionstests für Szenarien A–N anlegen (zunächst mit `Skip`-Grund „bekannt, Phase 1/2“) | T4 | 0.2 |
| 0.4 Incremental-Caching-Test | T2 | – |
| 0.5 Snapshot-Tests (Verify) für die drei Emitter auf dem heutigen Output → Refactoring-Netz für Phase 3 | T1 | 0.2 |

### Phase 1 – Kritische Fixes im generierten Code
| Schritt | Findings | Hinweis |
|---|---|---|
| 1.1 Vollständige Constraint-Emission in Apply-Methoden | K2 | Serialisierungsformat für Sonder-Constraints festlegen |
| 1.2 `IdentifierSanitizer` für Assembly-Name | K3 | Einmal in Pipeline berechnen |
| 1.3 Decorator-Validierung + DCTR004/005/006/009 | K4, H7 | TResponse-Constraint vorerst Warning + Skip |
| 1.4 Sichtbarkeitsprüfung + DCTR008 | K5 | Guide-Abschnitt „internal“ korrigieren |
| 1.5 DCTR010 (fehlende DI-Abstractions), DCTR001-Text | M9, M1 | |
| 1.6 Skips aus 0.3 für die gefixten Szenarien entfernen | | |

### Phase 2 – Discovery- und Laufzeit-Korrektheit
| Schritt | Findings | Hinweis |
|---|---|---|
| 2.1 Dedup partieller Klassen + Mehrfach-Interfaces | H1, H2 | `SelectMany` im Provider |
| 2.2 Records erkennen, structs mit DCTR011 ablehnen | H3 | Prädikat + `TypeKind`-Check |
| 2.3 DCTR007 für doppelte Service-Typen | H4 | im Full-Pfad lokal + referenziert |
| 2.4 `DecorateService` robust (alle Descriptoren, keyed skip) + `CreateFactory` statt `CreateInstance` | H8, R1 | eine Änderung am `EmitDecorateLogic`; T3 Integrationstest ergänzen |
| 2.5 Constraint-Templates mit Typparameter-Substitution | H5 | zunächst Warning DCTR009, dann Support |

### Phase 3 – Refactoring (verhaltensneutral, durch Snapshots aus 0.5 abgesichert)
| Schritt | Findings |
|---|---|
| 3.1 Ein `HandlerDetector` mit `IsStream`, syntaktischer Vorfilter, zentraler Symbol-Helper | S1, S11, P1 |
| 3.2 `GenerationModel` statt 10 Parametern; Sortierung in Pipeline; `EquatableArray` überall | S2, M3, P3, P4, S7 |
| 3.3 `SourceWriter`/`CodeWriter` mit Header, Einrück-Scopes, Listen-Helfer; `OptionsEmitter` | S4 (Teil), S5, S6 |
| 3.4 Toten Code, ungenutzte Helfer, Marker-Konstanten, `Ordinal`, Display-Format für Open Generics | S3, M6, S8 |
| 3.5 `ServiceTypeInfo` statt leerem `HandlerMetadata`; Scanner mit Namespace-Check und Gate | M5, S10, P2 |
| 3.6 Versionsermittlung ohne `sed` | S9 |

### Phase 4 – Metadaten-Format v2 (Breaking für Cross-Assembly-Metadaten)
Erfordert Versionierung: Scanner liest v1 **und** v2, Emitter schreibt nur v2. Major-Release.

| Schritt | Findings |
|---|---|
| 4.1 Ein `DecoratRHandlerServiceType`- und ein `DecoratRDecoratorRegistration`-Attribut mit `IsStream`, `DecoratorTypeName`, vollständigen Constraints (inkl. Sonder-Constraints und Response-Hierarchie) | H6, S4 (Rest), K4 (TResponse-Vollsupport), M10 |
| 4.2 Einheitlicher Sortierschlüssel lokal/referenziert | H6 |
| 4.3 Optional: Library dekoriert eigene Handler selbst, damit interne Request-Typen möglich werden | K5 (Alternative) |

### Phase 5 – Distribution und Design
| Schritt | Findings | Entscheidung nötig |
|---|---|---|
| 5.1 Bootstrap-Attribute und `DecoratROptions` nach `DecoratR.Abstractions`; Generator emittiert nur Fallback | M7, M8 | Ja (Host braucht dann Abstractions) |
| 5.2 `[EditorBrowsable(Never)]` auf Registries; Namespace-Konvention für `AddDecoratR` | M7 | Ja |
| 5.3 `AnalyzerReleases.*.md`, `helpLinkUri`, Diagnostics-Tabelle in Guide/Architecture | T6, M4 | – |
| 5.4 Doku angleichen (Order-Tie-Break, Attributanzahl, Records, interne Requests, SDK-Support) | M4 | – |

---

## 5. Vorgeschlagene neue Diagnostics

| ID | Severity | Meldung (Kurzform) | Finding |
|---|---|---|---|
| DCTR004 | Error | Decorator `{0}` implementiert weder `IRequestHandler<,>` noch `IStreamRequestHandler<,>` | K4 |
| DCTR005 | Error | Typparameter von Decorator `{0}` entsprechen nicht `<TRequest, TResponse>` des Handler-Interfaces | K4 |
| DCTR006 | Warning | `[Decorator]` auf nicht-generischem Typ `{0}` wird ignoriert | H7 |
| DCTR007 | Error | Mehrere Handler für `{0}`: `{1}`, `{2}` | H4 |
| DCTR008 | Warning | Typ `{0}` ist für generierten Code nicht zugreifbar (Sichtbarkeit `{1}`) und wird übersprungen | K5 |
| DCTR009 | Warning | Constraint `{0}` auf Decorator `{1}` wird nicht unterstützt (TResponse-Constraint / Typparameter in Constraint) | K4, H5 |
| DCTR010 | Error | `Microsoft.Extensions.DependencyInjection.Abstractions` wird nicht referenziert; `AddDecoratR()` kann nicht generiert werden | M9 |
| DCTR011 | Warning | Handler `{0}` ist ein Werttyp und wird nicht unterstützt | H3 |

Bestehende Anpassungen: DCTR001 mit dynamischem Attributnamen und Location; DCTR002/003 auf `Hidden`.

---

## 6. Offene Entscheidungen

1. **Attribute nach Abstractions verschieben (M8/5.1)?** Löst CS0436 und die Duplikation, macht aber `DecoratR.Abstractions`
   auch im Host zur Pflicht. Empfehlung: ja, mit Fallback-Generierung für eine Übergangszeit.
2. **TResponse-Constraints unterstützen oder ablehnen?** Empfehlung: Phase 1 ablehnen (DCTR009), Phase 4 unterstützen.
3. **Doppelte Service-Typen: Error oder Warning + „letzter gewinnt“?** Empfehlung: Error – das Verhalten ist sonst
   still falsch (nur der erste ist dekoriert).
4. **Records als Handler zulassen?** Empfehlung: ja (Klassen-Records), Werttypen ablehnen.
5. **Namespace für `AddDecoratR`:** Assembly-Namespace (heute) oder `Microsoft.Extensions.DependencyInjection`
   (Ökosystem-Konvention, kein zusätzliches `using`). Empfehlung: Konvention übernehmen, alte Position eine Version lang
   als `[Obsolete]`-Weiterleitung behalten.
6. **Roslyn-Minimum:** 4.8.0 (.NET 8 SDK 8.0.100) oder 4.11 (8.0.400)? Empfehlung: 4.8.0, solange net8.0 unterstützt wird.
