# Modular Monolith – Clean-Code- und Performance-Analyse

Stand: 2026-10-14 · Branch `claude/code-analysis-clean-performance-771b9d` · Basis-Commit `a857f9c`

> **Status: Phase 0 umgesetzt.** Die Generator-Benchmarks messen jetzt den Registrierungspfad (GP1) und enthalten
> ein Modular-Monolith-Szenario; `tests/DecoratR.Runtime.Benchmarks` misst den heute generierten Code (Resolve,
> `AddDecoratR()`-Startup). Die Probes P1–P6, R2 und R3 sind Regressionstests in
> `tests/DecoratR.Generator.Tests/ModularMonolithRegressionTests.cs` und
> `tests/DecoratR.IntegrationTests/ModularMonolithRegressionTests.cs`; sie prüfen das Zielverhalten und sind bis zur
> jeweiligen Phase mit `Skip` markiert (Skip-Grund nennt Finding und Phase). Phasen 1–4 sind offen.

Dieses Dokument bewertet DecoratR aus Sicht eines **Modular Monolith**: viele Module (je ein Satz Clean-Architecture-
Projekte), ein Shared Kernel mit Cross-Cutting-Decorators und **eine** Composition Root, die über
`[GenerateDecoratRMetadata]`/`[GenerateDecoratRRegistrations]` alles zusammensetzt. Untersucht wurden
`examples/modular-monolith` sowie die Teile von `src/`, die in diesem Szenario auf dem kritischen Pfad liegen
(Cross-Assembly-Metadaten, `AddDecoratR()`, generierter Laufzeitcode, Generator-Skalierung).

Jedes Finding hat eine ID, einen Beleg (Datei:Zeile, Probe oder Messung), eine Lösung, Priorität und Aufwand.
Aufwandsskala: **S** ≤ 2 h · **M** ≤ 1 Tag · **L** > 1 Tag.

---

## 1. Zusammenfassung

Die Architektur trägt den Modular Monolith grundsätzlich gut: Handler und Decorators bleiben `internal` im Modul,
die Composition Root sieht nur Registries und Metadaten, Module sprechen über Contracts miteinander, und der
Generator skaliert auf 10 Module × 100 Handler problemlos (Host kalt 4,6 ms). Die Schwächen liegen an drei Stellen:

1. **Startup und Hot Path des generierten Codes.** `AddDecoratR()` ruft pro Decorator-Schicht und pro Handler
   `ActivatorUtilities.CreateFactory` auf; im JIT ist das ein `Expression.Compile()`. Bei 500 Handlern × 3 Decorators
   kostet `AddDecoratR()` **~150 ms Kaltstart und 15,6 MB**. Jede Auflösung einer Pipeline allokiert pro Schicht
   ein zusätzliches `object[]` (**216 B statt 120 B**), unter NativeAOT ist sie zudem **70 % langsamer** als nötig.
   Eine generierte Direktkonstruktion bringt Startup auf **~5 ms** und Resolve auf das Niveau eines handgeschriebenen
   Factorys.
2. **Modulgrenzen werden vom Generator nicht geschützt.** Decorators eines Moduls greifen still auf Handler anderer
   Module über, ein vergessenes `[GenerateDecoratRMetadata]` verliert alle Handler eines Moduls ohne Diagnostic,
   eine Query mit abweichendem Response-Typ verliert still ihre Decorators, und `ValidateOnBuild` prüft die
   Abhängigkeiten dekorierter Handler **gar nicht** – genau der Fehler „Modul referenziert, `AddXModule()` vergessen“
   fällt erst beim ersten Request auf.
3. **Das Beispiel ist als Blaupause nicht durchgängig vorbildlich**: die öffentlichen Contracts exponieren die
   Schreibseite eines Moduls an alle anderen, Logging/Validierung allokieren auf jedem Request, Endpoints liefern
   untypisierte `Results`.

| Schwere | Anzahl | Beispiele |
|---|---|---|
| Hoch | 6 | Startup-Kosten durch `CreateFactory` (RP1), Allokation/AOT-Kosten pro Resolve (RP2), `ValidateOnBuild` blind (MM1), Decorator-Leak über Modulgrenzen (MM2), vergessener Metadata-Trigger still (MM3), Response-Mismatch still (MM4) |
| Mittel | 8 | CS0121 bei zwei Composition Roots (MM5), `int` vs. `int?` (MM6), quadratischer `Decorate`-Scan (RP3), globale Lifetime (RP4), Benchmarks messen den Registrierungspfad nicht (GP1), Contracts exponieren Schreibseite (EX1), untypisierte Endpoints (EX5), Validierungs-Decorator allokiert (EX3) |
| Niedrig | 10 | `AddDecoratR()` nicht idempotent, globaler Order-Namensraum, Größe des generierten Host-Codes, Logging-Allokationen, Exceptions als Kontrollfluss, … |

---

## 2. Vorgehen und Verifikation

1. Vollständiges Lesen von `examples/modular-monolith`, `src/DecoratR.Abstractions`, `src/DecoratR.Generator` und
   des generierten Codes des Beispiels (`obj/…/generated/DecoratR.Generator/`).
2. Baseline: `dotnet build` des Beispiels (0 Warnungen), `dotnet test` (176/176 grün).
3. Hypothesen mit temporären Probe-Tests verifiziert (Generator: Kompilierung des generierten Codes über
   `GeneratorTestBase`; Laufzeit: echter `ServiceProvider` mit dem generierten `AddDecoratR()` der Integrationstests).
   Die Probe-Tests wurden nach der Auswertung wieder entfernt.
4. Messungen in einem Scratchpad-Projekt (BenchmarkDotNet 0.15.8, .NET 10.0.12, Apple Silicon/arm64,
   `Microsoft.Extensions.DependencyInjection` 10.0.12) plus eine NativeAOT-Konsolenmessung (Stopwatch,
   5 Mio. Auflösungen, 3. Runde). Details in Abschnitt 4.

### Ergebnis der Probe-Szenarien

| # | Szenario | Ergebnis | Finding |
|---|---|---|---|
| P1 | `GetItems : IQuery<IReadOnlyList<int>>`, Handler liefert `List<int>`, Decorator `where TRequest : IQuery<TResponse>` | kompiliert, **kein `Decorate`-Aufruf, keine Diagnostic** | MM4 |
| P2 | Zwei Handler für `Q`: `IRequestHandler<Q, int>` und `IRequestHandler<Q, int?>` | **DCTR007 (Error)** – obwohl es zwei verschiedene Laufzeit-Service-Typen sind | MM6 |
| P3 | `Q : IQuery<int?>`, Handler liefert `int`, Decorator `where TRequest : IQuery<TResponse>` | Decorator wird angewendet → generierter Code **CS0311** | MM6 |
| P4 | Modul-Assembly mit `internal` Handler, **ohne** `[GenerateDecoratRMetadata]`, vom Host referenziert | weder Modul noch Host melden etwas; Handler fehlt zur Laufzeit | MM3 |
| P5 | Modul A: `[Decorator] ATransaction<,> where TRequest : IRequest`; Modul B ohne Decorators | `ATransaction` wird **auch um `BCmd` (Modul B) gelegt** | MM2 |
| P6 | Host `Root2` referenziert eine zweite Composition Root `Root1` und ruft `services.AddDecoratR()` | **CS0121** – Aufruf mehrdeutig | MM5 |
| R1 | `services.AddTransient<NeedsTrace>()` ohne `ExecutionTrace`, `ValidateOnBuild = true` | `AggregateException` beim Build (erwartet) | Referenz |
| R2 | `AddDecoratR()` ohne `ExecutionTrace` (Abhängigkeit aller Handler/Decorators), `ValidateOnBuild = true, ValidateScopes = true` | **Build ok**, Fehler erst beim ersten `GetRequiredService` | MM1 |
| R3 | `AddDecoratR()` zweimal aufgerufen | 2 Registrierungen; die erste ist **doppelt dekoriert** (Decorators laufen 2×, sichtbar über `IEnumerable<>`) | MM7 |
| V1 | Factory-Registrierung + zusätzliche keyed Registrierung `(Handler, "DecoratR.Validation", Handler)`, `ValidateOnBuild = true` | fehlende Abhängigkeit **und** Singleton→Scoped (Captive Dependency) werden beim Build gemeldet; vollständige Konfiguration baut | MM1 (Lösung) |
| G1 | Bestehende Generator-Benchmarks (`CompilationHelper`-Referenzen) mit `[GenerateDecoratRRegistrations]` | **DCTR009**, kein Output – Registrierungspfad wird nie gemessen | GP1 |

---

## 3. Findings

### 3.1 Laufzeit-Performance des generierten Codes

#### RP1 · `AddDecoratR()` kompiliert pro Decorator-Schicht und pro Handler eine Factory (Startup)
- **Beleg:** `src/DecoratR.Generator/Emit/RuntimeHelpersEmitter.cs:69-73` (`ActivatorUtilities.CreateFactory` pro
  `Wrap`), `:115-122` (`CreateFactory` für den inneren Handler in `CreateInnerFactory`). Im JIT-Fall ist
  `CreateFactory` ein `Expression.Compile()` (~50–60 µs pro Aufruf); bei S Service-Typen mit D Decorators sind das
  S × (D + 1) Kompilierungen beim Start – auch für Handler, die nie aufgerufen werden.
- **Messung (Abschnitt 4.2):** 500 Handler × 3 Decorators: **148–163 ms Kaltstart** (JIT), 93 ms (NativeAOT, dort
  Reflection-Pfad), 125 ms / 15,6 MB warm. 2000 Handler: ~0,5–0,7 s und 62 MB. Für Integrationstests mit
  `WebApplicationFactory` fällt das **pro Testhost** an.
- **Lösung:** Konstruktoraufrufe generieren statt sie zur Laufzeit zu kompilieren (Zieldesign Abschnitt 5):
  (a) Apply-Methoden der Decorator-Registries erzeugen die Decorator-Instanz per `new` – sie liegen in der Assembly
  des Decorators und dürfen `internal` Typen benutzen; (b) die Handler-Registry eines Moduls liefert pro Handler
  einen generierten Factory-Delegate. Fallback auf `ActivatorUtilities` nur für Konstruktoren, die der Generator nicht
  1:1 abbilden kann (mehrere öffentliche Konstruktoren, optionale Parameter, `[ServiceKey]`).
- **Wirkung:** 500 Handler: 148–163 ms → **~5 ms** Kaltstart (JIT), 93 ms → **3,4 ms** (AOT).
- **Priorität:** Hoch · **Aufwand:** L (zusammen mit RP2/MM1)

#### RP2 · Pro Auflösung ein `object[]` je Decorator-Schicht; unter AOT Reflection-Invoke
- **Beleg:** `RuntimeHelpersEmitter.cs:81` – `provider => decoratorFactory(provider, new object?[] { innerFactory(provider) })`.
  Mit der Default-Lifetime `Transient` (`src/DecoratR.Abstractions/DecoratROptions.cs:14`) wird die Pipeline bei
  **jedem** Request neu aufgebaut; im Beispiel löst `POST /orders` zwei Pipelines auf (Orders + Catalog-Query im Handler).
- **Messung (Abschnitt 4.1), Pipeline mit 3 Decorators:**

  | | JIT | NativeAOT |
  |---|---|---|
  | heute | 64,7 ns · 216 B | 163,0 ns · 216 B |
  | Direktkonstruktion + generierte Handler-Factory | **58,4 ns · 120 B** | **96,3 ns · 120 B** |
  | Untergrenze (eine handgeschriebene Factory) | 48,9 ns · 120 B | 89,8 ns · 120 B |

  120 B sind exakt die vier Objekte (Handler 24 B + 3 × Decorator 32 B); die 96 B Overhead sind die drei `object[]`.
- **Lösung:** wie RP1. Im JIT sinkt die Zeit um 10 % und die Allokation um 44 %; unter NativeAOT (DecoratR ist
  AOT-first) sinkt die Zeit um 41 %. Zusätzlich entfallen
  die `DynamicallyAccessedMembers`-Anforderungen an Decorator-Typen.
- **Priorität:** Hoch · **Aufwand:** in RP1 enthalten

#### RP3 · `Decorate` scannt die komplette `IServiceCollection` pro Service-Typ (O(N × S))
- **Beleg:** `RuntimeHelpersEmitter.cs:27-38` (lineare Schleife), `RegistrationsEmitter.cs:145` (ein `Decorate`-Aufruf
  pro Service-Typ). N = alle Registrierungen (ASP.NET-Basis allein: 114), S = dekorierte Service-Typen.
- **Messung (Abschnitt 4.2):** heute vom `CreateFactory`-Anteil verdeckt; ohne ihn: 500 Handler 0,77 ms vs.
  0,06 ms (Single Pass), 2000 Handler **11,5 ms vs. 0,36 ms**.
- **Lösung:** Ein Durchgang über `services` mit einem `Dictionary<Type, Func<ServiceDescriptor, ServiceDescriptor>>`
  (Semantik bleibt: jede nicht-keyed Registrierung des Service-Typs wird dekoriert, auch vom Nutzer vorab
  registrierte). Fehlende Treffer werden am Ende gesammelt gemeldet.
- **Priorität:** Mittel (wird relevant, sobald RP1 umgesetzt ist) · **Aufwand:** S

#### RP4 · Eine globale Lifetime für alle Module
- **Beleg:** `DecoratROptions.cs:14`, `RegistrationsEmitter.cs:86-93` (`options.Lifetime` für alle Registries).
- **Problem:** Module haben unterschiedliche Bedürfnisse: ein Modul mit EF-`DbContext` braucht `Scoped`/`Transient`,
  ein zustandsloses Lese-Modul könnte `Singleton` sein und damit die komplette Pipeline-Konstruktion pro Request
  einsparen. Heute zwingt das Modul mit der strengsten Anforderung allen anderen `Transient` auf.
- **Lösung:** Lifetime pro Modul, z. B. `[assembly: GenerateDecoratRMetadata(Lifetime = ServiceLifetime.Singleton)]`
  (wandert als Feld in den `HandlerRegistration`-Eintrag), optional pro Handler über ein Attribut; `DecoratROptions.Lifetime`
  bleibt Default für Module ohne Angabe. Captive-Dependency-Risiko dokumentieren (Singleton-Pipeline mit
  Scoped-Abhängigkeit) – mit MM1 wird das von `ValidateScopes` wieder erkannt.
- **Priorität:** Mittel · **Aufwand:** M

#### RP5 · Generierter Host-Code wächst mit S × D
- **Beleg:** `RegistrationsEmitter.cs:141-161`; jede Pipeline-Zeile wiederholt Apply-Methode und beide Typargumente
  voll qualifiziert. 10 Module × 100 Handler, 13 Decorators: **660 KB / 6 605 Zeilen** in einer Methode
  `AddDecoratR()` (Compile-Zeit des Hosts, Tier-0-JIT einer Riesenmethode beim Start).
- **Lösung (optional):** Pipelines mit identischer Decorator-Kette teilen sich eine generische Helper-Methode
  (`Chain3<TRequest, TResponse>(descriptor)` mit vereinigten `where`-Klauseln); pro Service-Typ bleibt ein
  Dictionary-Eintrag. In einem Modular Monolith gibt es typischerweise wenige Ketten (pro Modul „Command“ und „Query“).
- **Priorität:** Niedrig · **Aufwand:** M

### 3.2 Modulgrenzen und Korrektheit

#### MM1 · `ValidateOnBuild` prüft dekorierte Handler nicht
- **Beleg:** Probe R2. Dekorierte Registrierungen sind Factory-Descriptoren (`RuntimeHelpersEmitter.cs:77-82`); für
  Factories baut MS DI keinen Call-Site-Graphen, also wird nichts validiert. `tests/DecoratR.IntegrationTests/PipelineTests.cs:21`
  setzt `ValidateOnBuild = true` und suggeriert damit eine Prüfung, die nicht stattfindet.
- **Modular-Monolith-Bezug:** Handler werden zentral in `AddDecoratR()` registriert, ihre Infrastruktur aber pro
  Modul (`examples/modular-monolith/Examples.ModularMonolith.Api/Program.cs:15-19`). Wird `AddOrdersModule()`
  vergessen oder fehlt dort ein Repository, startet die App in Development ohne Fehler und scheitert beim ersten Request.
- **Lösung:** Pro Handler zusätzlich eine **reine Validierungsregistrierung**
  `new ServiceDescriptor(implementationType, DecoratRValidationKey, implementationType, lifetime)` (keyed, wird nie
  aufgelöst; `ValidateOnBuild` baut ihren Call-Site-Graphen und meldet fehlende Abhängigkeiten und Captive
  Dependencies). Die Auflösung selbst läuft weiter über die generierte Factory (RP1), weil die Auflösung über MS DI
  unter AOT 24 ns und 32 B teurer ist (Abschnitt 4.1, Varianten B/C). Probe V1 bestätigt, dass `ValidateOnBuild`
  solche nie aufgelösten keyed Registrierungen prüft (fehlende Abhängigkeit und Captive Dependency). Für Decorators
  analog als Folgeschritt.
- **Priorität:** Hoch · **Aufwand:** S (mit RP1)

#### MM2 · Decorators eines Moduls dekorieren Handler aller Module
- **Beleg:** Probe P5. `src/DecoratR.Generator/Emit/DecorationPlanner.cs:65-81` matcht jeden Decorator gegen jeden
  Service-Typ, unabhängig von der Herkunfts-Assembly. Einzige Abgrenzung ist heute die Konvention, Modul-Decorators
  auf einen Modul-Marker zu constrainen (`CatalogAuditDecorator … where TRequest : ICatalogCommand`).
- **Risiko:** Ein Transaktions- oder Autorisierungs-Decorator eines Moduls mit `where TRequest : IRequest` (der
  naheliegende erste Entwurf) läuft plötzlich um die Handler aller anderen Module – inklusive eigener `DbContext`-Abhängigkeiten.
  Umgekehrt kann jedes Modul den öffentlichen Marker `ICatalogCommand` implementieren und so Catalogs Decorators
  „ausleihen“.
- **Lösung:** Sichtbarkeitsbereich am Decorator: `[Decorator(Order = 5, Scope = DecoratorScope.Assembly)]`
  (nur Handler derselben Assembly) bzw. `DecoratorScope.Global` (heutiges Verhalten, Default für Rückwärtskompatibilität).
  Metadaten: `DecoratRDecoratorAttribute` bekommt `Scope`, `DecoratRHandlerAttribute` kennt die Assembly bereits
  implizit über die Registry. Ergänzend eine **Hidden/Info-Diagnostic** in der Composition Root, die pro Decorator
  auflistet, auf Handler welcher Assemblies er angewendet wird (Nachvollziehbarkeit im Build-Log).
- **Priorität:** Hoch · **Aufwand:** M

#### MM3 · Modul ohne `[GenerateDecoratRMetadata]` verliert alle Handler still
- **Beleg:** Probe P4. `DecoratRIncrementalGenerator.cs:124` unterdrückt alle Detection-Diagnostics ohne Trigger,
  `:132` emittiert ohne Trigger nichts; `ReferencedAssemblyScanner.cs:20-51` sieht nur Assemblies mit Metadaten.
- **Modular-Monolith-Bezug:** Neues Modul = neues `*.Application`-Projekt; die `AssemblyInfo.cs` mit dem Trigger
  wird leicht vergessen. Interne Handler-Typen sind in der Composition Root nicht sichtbar (Roslyn importiert
  `internal` Typen aus Referenzen nicht), daher kann nur das Modul selbst den Fehler erkennen.
- **Lösung:** **DCTR015 (Warning)** im Modul: „Assembly X enthält N Handler/Decorators, ist aber weder mit
  `[GenerateDecoratRMetadata]` noch mit `[GenerateDecoratRRegistrations]` markiert.“ Der Generator ist im Projekt
  referenziert – das ist ein starkes Signal, dass Generierung gewollt ist. Codefix: Attribut ergänzen.
- **Priorität:** Hoch · **Aufwand:** S

#### MM4 · Abweichender Response-Typ einer Query verliert still alle `IQuery<TResponse>`-Decorators
- **Beleg:** Probe P1; `src/DecoratR.Generator/Emit/ConstraintMatcher.cs:32-33`. Der Kommentar in
  `SharedKernel/…/Decorators/QueryTimingDecorator.cs:9-11` dokumentiert das Verhalten sogar als Feature.
- **Risiko:** Der C#-Compiler verbindet `IQuery<TResult>` nicht mit dem `TResponse` des Handlers. Ein Handler, der
  `List<T>` statt `IReadOnlyList<T>` liefert, verliert Timing, Caching oder Autorisierung – ohne jede Meldung. In
  einem Modular Monolith mit mehreren Teams ist das ein realistischer Fehler.
- **Lösung:** **DCTR016 (Warning)** in Library und Composition Root, wenn ein Constraint-Template mit
  `{TResponse}`-Platzhalter (z. B. `IQuery<{TResponse}>`) an einem Service-Typ scheitert, dessen Request-Hierarchie
  dieselbe generische Definition mit **anderem** Typargument enthält: „Decorator `QueryTimingDecorator` wird nicht auf
  `GetItemsHandler` angewendet: Request implementiert `IQuery<IReadOnlyList<int>>`, Handler liefert `List<int>`.“
  Die Information steckt bereits in den Hierarchie-Strings (Präfix bis `<`).
- **Priorität:** Hoch · **Aufwand:** S

#### MM5 · Zwei sichtbare Composition Roots → `AddDecoratR()` mehrdeutig (CS0121)
- **Beleg:** Probe P6; `RegistrationsEmitter.cs:31` emittiert eine `public static class` in
  `Microsoft.Extensions.DependencyInjection`.
- **Szenario:** Bootstrapper-Bibliothek + Host, Worker-Host + API-Host mit gemeinsamem Projekt, Testprojekt mit
  eigenen Test-Handlern, das die API referenziert.
- **Lösung:** Klasse standardmäßig `internal` emittieren; Opt-in für öffentliche Sichtbarkeit
  (`[assembly: GenerateDecoratRRegistrations(Public = true)]`) für Bootstrapper-Bibliotheken. Breaking nur für den
  seltenen Fall, dass `AddDecoratR()` assemblyübergreifend aufgerufen wird.
- **Priorität:** Mittel · **Aufwand:** S

#### MM6 · `?` von Nullable-Werttypen wird wie eine NRT-Annotation entfernt
- **Beleg:** Probes P2 (falsches DCTR007) und P3 (CS0311 im generierten Code).
  `src/DecoratR.Generator/Detection/SymbolExtensions.cs:21-22` entfernt jedes `?`; `int?` (= `Nullable<int>`) wird
  so zu `int`. Betroffen: `ServiceTypeInfo.SameService` (`Model/ServiceTypeInfo.cs:48-51`) und `ConstraintMatcher.cs:32`.
- **Lösung:** Für Identität und Matching einen zweiten, NRT-freien Namen verwenden
  (`SymbolDisplayFormat.FullyQualifiedFormat` ohne `IncludeNullableReferenceTypeModifier` liefert `ProductDto` für
  `ProductDto?`, aber `int?` für `Nullable<int>`). Hierarchien und Constraints in den Metadaten in diesem Format
  serialisieren (sie dienen nur dem Matching); Request-/Response-Typ für die Emission behalten ihre Annotationen.
- **Priorität:** Mittel · **Aufwand:** M

#### MM7 · `AddDecoratR()` ist nicht idempotent
- **Beleg:** Probe R3; `RegistrationsEmitter.cs:79-127`.
- **Lösung:** Marker-Registrierung (`services.Any(d => d.ServiceType == typeof(DecoratRMarker))` → früh zurück).
  Mit RP3 (Dictionary) ohnehin billiger.
- **Priorität:** Niedrig · **Aufwand:** S

#### MM8 · `Order` ist ein globaler Namensraum über alle Module
- **Beleg:** `src/DecoratR.Abstractions/DecoratorAttribute.cs:21`; im Beispiel Shared Kernel 0/1/10, Catalog 5. Jedes
  Modul muss die Zahlen der anderen kennen; Gleichstände entscheidet der Typname (deterministisch, aber implizit).
- **Lösung:** Im Beispiel und Guide eine Konvention zeigen: `public static class PipelineOrder { public const int Logging = 0; … }`
  im Shared Kernel, Module verwenden `[Decorator(Order = PipelineOrder.ModuleAudit)]`. Optional Info-Diagnostic bei
  gleichem `Order` aus verschiedenen Assemblies.
- **Priorität:** Niedrig · **Aufwand:** S

### 3.3 Generator, Build und Messinfrastruktur

#### GP1 · Generator-Benchmarks messen den Registrierungs- und Decorator-Pfad nicht
- **Beleg:** Probe G1. `tests/DecoratR.Generator.Benchmarks/CompilationHelper.cs:16-24` referenziert
  `Microsoft.Extensions.DependencyInjection.Abstractions` nicht → `hasServiceCollection = false` → DCTR009, die
  Szenarien „Constrained decorators (full path)“ und „Cross-assembly composition“ (`GeneratorBenchmarks.cs:56-68`)
  enden vor `DecorationPlanner` und Emission; auch Decorator-Registries in Libraries werden nicht erzeugt.
- **Lösung:** DI-Abstractions referenzieren, im Setup sicherstellen, dass der erwartete Output entsteht (Guard), und
  ein Modular-Monolith-Szenario ergänzen (N Module × M Handler, Host kalt + Tastendruck). Zusätzlich ein
  Laufzeit-Benchmark-Projekt (Resolve, `AddDecoratR()`-Startup) als Baseline für RP1–RP3.
- **Priorität:** Mittel · **Aufwand:** S–M
- **Status:** umgesetzt (Phase 0). `CompilationHelper.EnsureGenerates` bricht den Benchmark-Lauf ab, wenn ein Szenario
  nicht den erwarteten Output erzeugt, Generator-Warnungen meldet oder der generierte Code nicht kompiliert.

#### GP2 · Composition Root decodiert bei jedem Tastendruck alle Modul-Metadaten neu
- **Beleg:** `DecoratRIncrementalGenerator.cs:55-60` (`CompilationProvider` → `ReferencedAssemblyScanner.Scan`).
  Die Downstream-Schritte bleiben durch Equality gecacht, aber der Scan selbst läuft bei jeder Änderung im Host.
- **Messung:** 10 Module × 100 Handler: **0,64 ms und 2,2 MB pro Tastendruck** im Host; Host kalt 4,6 ms / 5,6 MB.
- **Bewertung:** unkritisch. Falls Hosts deutlich größer werden: Ergebnis pro `IAssemblySymbol` in einer
  `ConditionalWeakTable` cachen (PE-Assembly-Symbole werden zwischen Compilations wiederverwendet, solange sich die
  Referenzen nicht ändern).
- **Priorität:** Niedrig · **Aufwand:** S

#### GP3 · Kleinere Generator-Smells mit Modular-Monolith-Bezug
| Problem | Beleg | Lösung |
|---|---|---|
| `DecoratRHandlerRegistry.Handlers` ist ein öffentliches, veränderbares Array; jedes Modul kann die Registry eines anderen manipulieren | `HandlerRegistryEmitter.cs:48` | `ReadOnlySpan<HandlerRegistration>`-Property oder `IReadOnlyList<>` |
| `HandlerRegistration` wird pro Modul als eigener Record generiert | `HandlerRegistryEmitter.cs:64-72` | Typ in `DecoratR.Abstractions` (`DecoratR.Metadata`), wie bereits `VoidRequestHandler<T>`; Voraussetzung für das Factory-Feld aus RP1 |
| `DecorationPlanner.AddDistinct` ist O(S²), `ConstraintMatcher` allokiert pro Vergleich gestrippte Strings | `DecorationPlanner.cs:101-108`, `ConstraintMatcher.cs:41-43` | `HashSet` mit Comparer bzw. vorberechnete NRT-freie Namen (fällt mit MM6 ab); heute bei 1 000 Handlern 4,6 ms, daher nur mitnehmen |

### 3.4 Clean Code im Beispiel `examples/modular-monolith`

Das Beispiel ist die Blaupause, die Nutzer kopieren. Abweichungen von „so würde man es produktiv bauen“ sind daher
relevanter als in normalem Beispielcode.

| ID | Problem | Beleg | Lösung | Prio · Aufwand |
|---|---|---|---|---|
| EX1 | `Catalog.Contracts` exponiert die **Schreibseite** (`CreateProductCommand`) und den Decorator-Marker `ICatalogCommand` an alle Module, die Contracts referenzieren. Orders könnte Produkte anlegen oder Catalogs Audit-Decorator „ausleihen“ | `Modules/Catalog/…Contracts/CatalogContracts.cs:9,13` | Contracts auf die modulübergreifende API beschränken (`GetProductQuery`, `ProductDto`); modulinterne Requests in ein eigenes Projekt (`Catalog.Requests`), das nur `Catalog.Presentation` referenziert. Architekturtest (z. B. NetArchTest) als Leitplanke: „Orders.* referenziert von Catalog nur Contracts“ | Mittel · M |
| EX2 | Modul-Komposition ist auf drei Stellen verteilt: `AddDecoratR()` (alle Handler), `AddCatalogModule()` in **Infrastructure**, `MapCatalogEndpoints()` in Presentation. Zusammen mit MM1 entsteht der stille Fehler „Modul vergessen“ | `Api/Program.cs:15-19,27-28`, `…Catalog.Infrastructure/DependencyInjection.cs:10-15` | Pro Modul ein Einstiegspunkt (`Catalog.Module`-Projekt oder `ICatalogModule`-Konvention mit `AddServices`/`MapEndpoints`); in der README erklären, dass `AddDecoratR()` Handler aller referenzierten Module registriert, deren Infrastruktur aber vom Modul kommt | Niedrig · S |
| EX3 | `ValidationDecorator` allokiert pro Command Closure, `SelectMany`-Iterator und `List` – auch ohne Validator und ohne Fehler; Validatoren liefern immer eine neue `List` | `SharedKernel/…/Decorators/ValidationDecorator.cs:17`, `…/CreateProductValidator.cs:10`, `…/PlaceOrderValidator.cs:10` | `foreach` über Validatoren, Fehlerliste erst beim ersten Fehler anlegen; Validatoren geben `[]` zurück, wenn gültig | Mittel · S |
| EX4 | Logging: `LogInformation(...)` mit `params object[]` zweimal pro Request (auch bei deaktiviertem Level), Exception wird im `LoggingDecorator` als Warning **und** vom ASP.NET-Exception-Handler geloggt; `CatalogAuditDecorator` loggt komplette Command-/Response-Payloads auf `Information` | `…/LoggingDecorator.cs:16,20,25`, `…/QueryTimingDecorator.cs:23`, `…/CatalogAuditDecorator.cs:22` | `[LoggerMessage]`-Source-Generator (allokationsfrei, AOT-tauglich); erwartete Fehler (`ValidationException`) nicht als Warning loggen; Audit nur mit Schlüsseln/IDs | Niedrig · S |
| EX5 | Endpoints liefern untypisierte `Results.Ok(...)`/`Results.NotFound()` → die per `AddOpenApi()` erzeugte Doku hat keine Response-Schemas; Catalog mappt Request→Command, Orders bindet den Contract-Typ direkt als Body | `…Catalog.Presentation/CatalogEndpoints.cs:23,34-36,44`, `…Orders.Presentation/OrdersEndpoints.cs:15-21` | `TypedResults` und `Results<Ok<ProductDto>, NotFound>`; einheitlich HTTP-Request-DTOs in Presentation und Mapping auf Contracts | Mittel · S |
| EX6 | „Produkt existiert nicht“ wird im Handler als `ValidationException` geworfen (→ 400 mit Schlüssel `request`), obwohl die Validierung bereits gelaufen ist; Exceptions als Kontrollfluss im Hot Path | `…Orders.Application/Orders/PlaceOrderCommandHandler.cs:20-21` | Eigene fachliche Exception bzw. Result-Typ (→ 404/422); die Validierung bleibt für Eingabeformate zuständig | Niedrig · S |
| EX7 | Asymmetrie `ICommand` (ohne Response) vs. `IQuery<TResult>`; Commands liefern trotzdem DTOs. Der Response-Typ wird an jeder Injektionsstelle wiederholt (`IRequestHandler<GetProductQuery, ProductDto?>`) | `SharedKernel/…/Messaging/ICommand.cs:6`, `…/IQuery.cs:6`, `PlaceOrderCommandHandler.cs:15` | Im Shared Kernel `ICommand<TResult> : ICommand` ergänzen; damit greift MM4 auch für Commands. (Die Kernentscheidung „Response-Typ hängt am Handler, kein `IRequest<T>`“ aus `docs/void-request-plan.md` bleibt unberührt.) | Niedrig · S |
| EX8 | Domänen-Invarianten nur im Validator: `Order.Place`/`Product.Create` akzeptieren Menge 0 oder leeren Namen | `…Orders.Domain/Order.cs:24-25`, `…Catalog.Domain/Product.cs:18` | Guard Clauses in den Factory-Methoden; Validator bleibt für Request-Fehlermeldungen | Niedrig · S |
| EX9 | Repository sortiert und kopiert bei jedem `ListAsync`; Stream wartet künstlich 50 ms pro Element (Demo, aber nicht kommentiert) | `…Catalog.Infrastructure/InMemoryProductRepository.cs:21-31` | Kommentar „Demo-Verzögerung“; Sortierung beim Schreiben | Niedrig · S |

---

## 4. Messwerte

Reproduzierbar mit den Benchmark-Projekten im Repo (die Werte unten stammen aus der Analyse im Scratchpad; die
Varianten A–D existieren dort nur als Prototyp und kommen mit Phase 1 in den generierten Code):

```bash
dotnet run -c Release --project tests/DecoratR.Runtime.Benchmarks -- --filter '*Resolve*'
```

```bash
dotnet run -c Release --project tests/DecoratR.Runtime.Benchmarks -- --filter '*Startup*' --job short
```

```bash
dotnet run -c Release --project tests/DecoratR.Generator.Benchmarks -- --filter '*ModularMonolith*' --job short
```

Umgebung: Apple Silicon (arm64), macOS, .NET SDK 10.0.401 / Runtime 10.0.12, `Microsoft.Extensions.DependencyInjection`
10.0.12, BenchmarkDotNet 0.15.8. Pipeline-Aufbau: ein Handler mit einer Singleton-Abhängigkeit, drei Decorators
mit je einer Singleton-Abhängigkeit, Lifetime `Transient`, Auflösung aus einem Scope.

### 4.1 Auflösung einer Pipeline (Hot Path)

| Variante | JIT (BDN) | Alloc | NativeAOT | Alloc |
|---|---:|---:|---:|---:|
| MS DI, nur Handler, ohne Decorators (Referenz) | 11,4 ns | 24 B | 48,7 ns | 56 B |
| **DecoratR heute** (`Wrap` + `ActivatorUtilities`) | **64,7 ns** | **216 B** | **163,0 ns** | **216 B** |
| A: Decorators per `new`, Handler über `ActivatorUtilities` | 66,5 ns | 120 B | 106,1 ns | 120 B |
| B: Decorators per `new`, Handler als keyed MS-DI-Registrierung | 68,6 ns | 120 B | 129,8 ns | 152 B |
| C: Decorators per `new`, Handler als konkrete MS-DI-Registrierung | 66,8 ns | 120 B | 131,0 ns | 152 B |
| **D: Decorators per `new`, Handler über generierte Factory** | **58,4 ns** | **120 B** | **96,3 ns** | **120 B** |
| Untergrenze: eine handgeschriebene Factory für alles | 48,9 ns | 120 B | 89,8 ns | 120 B |

Lesart: Die Zeit wird im JIT von den `GetRequiredService`-Aufrufen für die Abhängigkeiten dominiert, daher liegen
heute und A–C dort innerhalb des Messrauschens (±2 ns zwischen Läufen); nur D spart auch im JIT Zeit (−10 %),
alle neuen Varianten sparen 96 B. Unter NativeAOT nutzt `ActivatorUtilities` Reflection-Invoke
und MS DI seinen Reflection-Resolver – dort ist die generierte Variante D klar vorne. B/C sind wegen der
MS-DI-Auflösung des Handlers unter AOT langsamer als A; deshalb trennt das Zieldesign „Auflösung“ (Factory, D) von
„Validierung“ (keyed Registrierung, MM1).

### 4.2 `AddDecoratR()`-Startup (Handler × 3 Decorators, ASP.NET-Basis mit 114 Registrierungen)

Warm (BenchmarkDotNet, ShortRun, JIT):

| Variante | 100 Handler | 500 Handler | 2 000 Handler |
|---|---:|---:|---:|
| **heute:** Scan pro Service-Typ + `CreateFactory` pro Schicht | 24,6 ms · 3,1 MB | 125,0 ms · 15,6 MB | ~710 ms · 62,3 MB |
| Single Pass + `CreateFactory` pro Schicht | 24,3 ms | 124,4 ms | ~530 ms |
| Scan pro Service-Typ, Decorators per `new` (Handler weiter `CreateFactory`) | 5,2 ms · 0,8 MB | 27,7 ms · 3,9 MB | 120,3 ms · 15,6 MB |
| keyed Handler + `new`, **Scan** pro Service-Typ | 0,042 ms | 0,77 ms | 11,5 ms |
| keyed Handler + `new`, **Single Pass** | 0,010 ms | 0,062 ms | 0,36 ms · 1,6 MB |
| vorab dekoriert registriert, kein Scan | 0,004 ms | 0,022 ms | 0,13 ms · 0,8 MB |

Kalt (erster Aufruf im frischen Prozess, 500 Handler, drei Läufe):

| | heute | Zieldesign (Direktkonstruktion + Single Pass) |
|---|---:|---:|
| JIT | 148–163 ms | 5,2–5,4 ms |
| NativeAOT | 93,5 ms | 3,4 ms |

### 4.3 Generator (10 Module × 100 Handler, Shared Kernel mit 3 Decorators, 10 Modul-Decorators)

| Schritt | Zeit | Alloc |
|---|---:|---:|
| Composition Root, kalter Lauf | 4,6 ms | 5,6 MB |
| Composition Root, Tastendruck (unabhängige Änderung) | 0,64 ms | 2,2 MB |
| Modul (100 Handler), kalter Lauf | 3,1 ms | 1,3 MB |
| Modul, Tastendruck (Worst Case: alle Handler in einer Datei) | 2,6 ms | 0,4 MB |
| Generierter Host-Code | 660 KB, 6 605 Zeilen | – |

---

## 5. Zieldesign für Registrierung und Auflösung

Ziel: keine Laufzeit-Kompilierung, keine `object[]` pro Resolve, `ValidateOnBuild` greift, Semantik für
vom Nutzer vorab registrierte Service-Typen bleibt erhalten. Das Metadaten-Format bleibt abwärtskompatibel, weil die
Composition Root am referenzierten Registry-Typ erkennt, ob er Factories liefert.

**Modul-Registry (generiert im Modul, darf `internal` Typen benutzen):**

```csharp
public static ReadOnlySpan<HandlerRegistration> Handlers => s_handlers;
private static readonly HandlerRegistration[] s_handlers =
[
    new(typeof(IRequestHandler<CreateProductCommand, ProductDto>),
        typeof(CreateProductCommandHandler),                       // nur noch für die Validierungsregistrierung
        static sp => new CreateProductCommandHandler(sp.GetRequiredService<IProductRepository>())),
];
```

**Decorator-Registry (generiert in der Assembly des Decorators):**

```csharp
public static ServiceDescriptor ApplyLoggingDecorator<TRequest, TResponse>(ServiceDescriptor inner)
    where TRequest : IRequest
{
    var innerFactory = DescriptorFactory.Create(inner);            // Factory/Instanz/Typ, wie heute CreateInnerFactory
    return ServiceDescriptor.Describe(inner.ServiceType,
        sp => new LoggingDecorator<TRequest, TResponse>(
            (IRequestHandler<TRequest, TResponse>)innerFactory(sp),
            sp.GetRequiredService<ILogger<LoggingDecorator<TRequest, TResponse>>>()),
        inner.Lifetime);
}
```

**Composition Root:**

```csharp
foreach (ref readonly var handler in Catalog.Application.DecoratRHandlerRegistry.Handlers)
{
    services.Add(ServiceDescriptor.Describe(handler.ServiceType, handler.Factory, lifetime));
    // Wird nie aufgelöst; ValidateOnBuild/ValidateScopes prüfen darüber die Abhängigkeiten des Handlers (MM1).
    services.Add(new ServiceDescriptor(handler.ImplementationType, DecoratRKeys.Validation, handler.ImplementationType, lifetime));
}

// Ein Durchgang statt eines Scans pro Service-Typ (RP3).
Decorate(services, new Dictionary<Type, Func<ServiceDescriptor, ServiceDescriptor>>(capacity: 6)
{
    [typeof(IRequestHandler<CreateProductCommand, ProductDto>)] = static d => Pipeline_ValidationAuditLogging<CreateProductCommand, ProductDto>(d),
    …
});
```

**Regeln für die Direktkonstruktion (Handler und Decorators):**
- genau ein öffentlicher Konstruktor (oder einer mit `[ActivatorUtilitiesConstructor]`), sonst Fallback auf den
  heutigen `ActivatorUtilities`-Pfad (lazy beim ersten Resolve statt im Startup);
- Parameter vom Typ des Handler-Interfaces → innerer Handler (Decorators);
- `IServiceProvider` → `sp`; `[FromKeyedServices(key)]` → `GetRequiredKeyedService<T>(key)`;
- optionale Parameter, `[ServiceKey]`, `ref`/`out`/`params` → Fallback;
- alles andere → `sp.GetRequiredService<T>()` (deckt `IEnumerable<T>` und `ILogger<T>` ab).

Damit entfallen in Registries, die nur Direktkonstruktion nutzen, `Wrap`, `ActivatorUtilities` und die
`DynamicallyAccessedMembers`-Anforderungen an Decorator-Typen.

---

## 6. Neue Diagnostics

| Code | Severity | Bedeutung | Finding |
|---|---|---|---|
| DCTR015 | Warning | Assembly enthält Handler/Decorators, hat aber keinen DecoratR-Trigger | MM3 |
| DCTR016 | Warning | Decorator nicht angewendet: Request implementiert die Constraint-Definition mit anderem Typargument als der Handler-Response | MM4 |
| DCTR017 | Hidden | Decorator X wird auf Handler aus Assembly Y angewendet (Übersicht pro Composition Root) | MM2 |
| DCTR018 | Info | Decorators aus verschiedenen Assemblies mit gleichem `Order` – Reihenfolge entscheidet der Typname | MM8 |

---

## 7. Umsetzungsplan

Jede Phase ist einzeln mergebar; Phase 0 liefert die Baseline, gegen die Phase 1 gemessen wird.

### Phase 0 – Messbarkeit und Regressionstests
| Schritt | Findings |
|---|---|
| ✅ 0.1 Generator-Benchmarks: DI-Abstractions referenzieren, Output-Guard, Modular-Monolith-Szenario (N × M, eine Datei pro Handler) | GP1 |
| ✅ 0.2 `tests/DecoratR.Runtime.Benchmarks`: Resolve über das echte generierte `AddDecoratR()`, Startup über die echten generierten `Apply…`-Methoden für 100/500/2000 Handler | RP1–RP3 |
| ✅ 0.3 Probes P1–P6, R2, R3 als Regressionstests mit Zielverhalten und `Skip`; jeder Skip-Test schlägt ohne Skip heute aus dem erwarteten Grund fehl. MM2 hat zusätzlich einen aktiven Test für das heutige (künftig `Global`) Verhalten | MM1–MM7 |

### Phase 1 – Generierter Laufzeitcode (größter Hebel)
| Schritt | Findings |
|---|---|
| 1.1 `HandlerRegistration` nach `DecoratR.Abstractions` (`DecoratR.Metadata`), Feld `Factory`; Registry-Property als `ReadOnlySpan` | RP1, GP3 |
| 1.2 Generierte Handler-Factories in Modul-Registries und für lokale Handler; Fallback-Regeln wie in Abschnitt 5 | RP1, RP2 |
| 1.3 Apply-Methoden mit Direktkonstruktion; `Wrap` nur noch als Fallback | RP1, RP2 |
| 1.4 Validierungsregistrierung (keyed) pro Handler; `PipelineTests` um Fehlerfall „fehlende Abhängigkeit“ erweitern | MM1 |
| 1.5 Single-Pass-`Decorate` mit Dictionary; Idempotenz-Marker (die Kopie in `StartupBenchmarks` mitziehen) | RP3, MM7 |
| 1.6 Benchmarks aus Phase 0 erneut laufen lassen, Tabelle in `architecture.md` | – |

### Phase 2 – Modulgrenzen
| Schritt | Findings |
|---|---|
| 2.1 DCTR015 (fehlender Trigger) inkl. Codefix | MM3 |
| 2.2 DCTR016 (Response-Mismatch) | MM4 |
| 2.3 `DecoratorScope` + Metadaten-Feld + DCTR017 | MM2 |
| 2.4 Composition-Root-Klasse `internal` + Opt-in `Public` | MM5 |
| 2.5 NRT-freie Namen für Identität/Matching | MM6 |

### Phase 3 – Erweiterungen
| Schritt | Findings |
|---|---|
| 3.1 Lifetime pro Modul (Attribut-Property, Registry-Feld) | RP4 |
| 3.2 Geteilte Pipeline-Helper pro Decorator-Kette | RP5 |
| 3.3 Scan-Cache pro `IAssemblySymbol` (nur bei Bedarf) | GP2 |

### Phase 4 – Beispiel und Doku
| Schritt | Findings |
|---|---|
| 4.1 Contracts verschlanken, modulinterne Requests trennen, Architekturtest | EX1 |
| 4.2 `ValidationDecorator` allokationsarm, `[LoggerMessage]`, `TypedResults` | EX3–EX5 |
| 4.3 `PipelineOrder`-Konstanten, `ICommand<TResult>`, Guard Clauses, fachliche Exception | MM8, EX6–EX8 |
| 4.4 README des Beispiels: Modul-Checkliste (Trigger, `AddXModule()`, Order-Konvention, Scope) | EX2, MM3 |

---

## 8. Offene Entscheidungen

1. **Validierung über keyed Zusatzregistrierung (MM1):** akzeptabel, dass jede Handler-Implementierung zusätzlich
   unter einem DecoratR-Schlüssel im Container steht? Alternative: generierte Prüfmethode
   (`IServiceProviderIsService`) als Opt-in – mehr Code, aber keine zusätzlichen Descriptoren.
2. **Fallback-Strategie der Direktkonstruktion:** Fallback still (heutiges Verhalten, nur langsamer) oder mit
   Info-Diagnostic, damit Nutzer den schnellen Pfad gezielt erreichen?
3. **API für Decorator-Scope (MM2):** `Scope` am `[Decorator]` oder Default pro Modul
   (`[assembly: GenerateDecoratRMetadata(DecoratorScope = …)]`)? Default `Global` (kompatibel) oder `Assembly`
   (sicherer, breaking)?
4. **Sichtbarkeit von `AddDecoratR()` (MM5):** `internal` als Default ist breaking für assemblyübergreifende Aufrufe –
   vor 1.0 vertretbar?
5. **Lifetime pro Modul (RP4):** am Trigger-Attribut, pro Handler oder beides?
