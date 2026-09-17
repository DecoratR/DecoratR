# Requests ohne Response – Analyse und Plan

Stand: 2026-09-17, Branch `claude/request-without-response-type-9a8a2d`, Basis `84d5329`.

**Status:** Umgesetzt auf diesem Branch mit den Empfehlungen aus Abschnitt 7 (`Unit`, Fassade `VoidRequestHandler<TRequest>`
in Abstractions, erweiterte DCTR005-Meldung). Tests: `VoidHandlerTests`, erweiterte `PipelineTests`, aktualisierte Snapshots.

## 1. Zusammenfassung

Heute muss ein Handler ohne fachliches Ergebnis `IRequestHandler<TCommand, Unit>` implementieren und
`ValueTask<Unit>` zurückgeben – wobei `Unit` gar nicht von DecoratR kommt, sondern jeder Nutzer selbst definiert
(im Repo gibt es keinen `Unit`-Typ, `grep -rn "Unit"` liefert nichts). Der Aufrufer muss ebenfalls
`IRequestHandler<TCommand, Unit>` auflösen und ein `ValueTask<Unit>` wegwerfen.

**Empfehlung:** Ein neues Interface `IRequestHandler<TRequest>` mit `ValueTask HandleAsync(...)`, das per
Default-Interface-Member (DIM) von `IRequestHandler<TRequest, Unit>` erbt und die Brücke selbst implementiert.
`Unit` wird damit zu einem Implementierungsdetail, das weder Handler-Autor noch Aufrufer sehen:

```csharp
public sealed class CompleteTodoCommandHandler : IRequestHandler<CompleteTodoCommand>
{
    public ValueTask HandleAsync(CompleteTodoCommand command, CancellationToken cancellationToken = default) { … }
}

// Aufrufer
IRequestHandler<CompleteTodoCommand> handler = …;
await handler.HandleAsync(command, cancellationToken);
```

Die Decorators bleiben unverändert generisch über `<TRequest, TResponse>` und greifen automatisch auch für
Void-Handler (mit `TResponse = Unit`). Ganz ohne einen `Unit`-Typ geht es nicht, weil C# keine Möglichkeit bietet,
einen *einzigen* generischen Decorator sowohl über `IRequestHandler<TRequest, TResponse>` als auch über eine
ergebnislose Variante zu schreiben. Der Typ existiert also weiter, aber nur an einer Stelle: als Typargument, das
der Generator und die Decorator-Pipeline intern benutzen.

Der Generator braucht dafür nur eine kleine Erweiterung (Registrierung einer Fassade für
`IRequestHandler<TRequest>`); Detection, Constraint-Matching, Cross-Assembly-Metadaten und Decorator-Anwendung
funktionieren bereits heute unverändert – das habe ich mit einem Probe-Projekt gegen den aktuellen Generator
verifiziert (Abschnitt 3).

## 2. Ausgangslage im Code

| Stelle | Befund |
|--------|--------|
| [IRequestHandler.cs:6-16](../src/DecoratR.Abstractions/IRequestHandler.cs) | Einziges Handler-Interface, immer mit `TResponse`; Rückgabe `ValueTask<TResponse>`. |
| [IRequest.cs:6](../src/DecoratR.Abstractions/IRequest.cs) | Marker ohne Typparameter. Der Response-Typ hängt bewusst nur am Handler, nicht am Request. |
| [DecoratorAttribute.cs:8-11](../src/DecoratR.Abstractions/DecoratorAttribute.cs) | Decorators müssen genau zwei Typparameter für `TRequest`/`TResponse` haben. |
| [DecoratorDetector.cs:43-49](../src/DecoratR.Generator/Detection/DecoratorDetector.cs) | Erzwingt die Zwei-Parameter-Form (DCTR005). Ein Decorator, der `IRequestHandler<TRequest>` implementieren würde, kann heute nicht existieren. |
| [SymbolExtensions.cs:38-55](../src/DecoratR.Generator/Detection/SymbolExtensions.cs) | `IsHandlerInterface` erkennt ausschließlich `IRequestHandler\`2` / `IStreamRequestHandler\`2` und liest über `symbol.AllInterfaces` ([HandlerDetector.cs:50-52](../src/DecoratR.Generator/Detection/HandlerDetector.cs)), also auch geerbte Interfaces. |
| [ServiceTypeInfo.cs:10-16](../src/DecoratR.Generator/Model/ServiceTypeInfo.cs) | Service-Typ ist immer das konstruierte Zwei-Parameter-Interface; `ResponseTypeHierarchy` enthält Typfakten wie `!struct`, die das Constraint-Matching benutzt ([ConstraintMatcher.cs:22-29](../src/DecoratR.Generator/Emit/ConstraintMatcher.cs)). |
| [RegistrationsEmitter.cs:96-110](../src/DecoratR.Generator/Emit/RegistrationsEmitter.cs) | Lokale Handler werden als `(ServiceType, ImplementationType, options.Lifetime)` registriert. |
| [HandlerRegistryEmitter.cs:48-53](../src/DecoratR.Generator/Emit/HandlerRegistryEmitter.cs) | Library-Handler landen als `HandlerRegistration(ServiceType, ImplementationType)` im `Handlers`-Array, das die Composition Root blind durchiteriert ([RegistrationsEmitter.cs:86-93](../src/DecoratR.Generator/Emit/RegistrationsEmitter.cs)). |
| [ReferencedAssemblyScanner.cs:73-94](../src/DecoratR.Generator/Detection/ReferencedAssemblyScanner.cs) | Cross-Assembly-Dekoration liest nur `[DecoratRHandler]`-Attribute; das `Handlers`-Array selbst wird zur Compile-Zeit nicht interpretiert. |
| [GreetEndpoint.cs:15](../playground/DecoratR.Sample.Presentation/Endpoints/GreetEndpoint.cs) | Beispiel-Aufrufer löst `IRequestHandler<GreetCommand, string>` auf – bei Commands ohne Ergebnis wäre das heute `IRequestHandler<GreetCommand, Unit>`. |

Konsequenz: Alles, was der Generator über einen Handler wissen muss, ist der Zwei-Parameter-Service-Typ. Solange
ein Void-Handler *auch* `IRequestHandler<TRequest, Unit>` implementiert, ändert sich für Detection, Planner,
Constraint-Matching und Metadaten nichts.

## 3. Verifikation mit Probe-Projekt

Wegwerf-Konsolenprojekt (Scratchpad, nicht im Repo) gegen `src/DecoratR.Abstractions` und den aktuellen Generator
als Analyzer. Im Probe-Projekt lagen `Unit`, `IRequestHandler<TRequest>` und die Fassade `VoidRequestHandler<T>`
im Namespace `Probe`; der Generator wurde **nicht** angefasst.

Kernstück:

```csharp
public readonly record struct Unit { public static readonly Unit Value = default; }

public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    new ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default);

    ValueTask<Unit> IRequestHandler<TRequest, Unit>.HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var task = HandleAsync(request, cancellationToken);
        return task.IsCompletedSuccessfully ? new ValueTask<Unit>(Unit.Value) : Awaited(task);

        static async ValueTask<Unit> Awaited(ValueTask task)
        {
            await task.ConfigureAwait(false);
            return Unit.Value;
        }
    }
}

public sealed class VoidRequestHandler<TRequest>(IRequestHandler<TRequest, Unit> inner) : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    public ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var task = inner.HandleAsync(request, cancellationToken);
        return task.IsCompletedSuccessfully ? default : new ValueTask(task.AsTask());
    }
}
```

Dazu ein Handler `CreateTodoCommandHandler : IRequestHandler<CreateTodoCommand>` mit `ValueTask HandleAsync`,
ein `LoggingDecorator<TRequest, TResponse>` (Order 1) und ein `ClassResponseDecorator<TRequest, TResponse>`
mit `where TResponse : class` (Order 2), der für `Unit` **nicht** greifen darf.

Ergebnis:

- Der unveränderte Generator hat den Handler als `IRequestHandler<CreateTodoCommand, Unit>` registriert und die
  Pipeline `Wrap<IRequestHandler<CreateTodoCommand, Unit>, LoggingDecorator<CreateTodoCommand, Unit>>` emittiert.
- `ClassResponseDecorator` wurde korrekt übersprungen (`Unit` ist ein Struct, `!class`-Fakt fehlt).
- Nach manueller Registrierung der Fassade (`IRequestHandler<CreateTodoCommand>` → `VoidRequestHandler<CreateTodoCommand>`)
  lieferte `await handler.HandleAsync(cmd)` den Trace `logging-in > handler(buy milk) > logging-out(())`.
  Der Aufrufer sieht ein reines `ValueTask`.
- Build mit `IsAotCompatible=true`: keine IL-/Trim-Warnungen.

Die Fassade ist nötig, weil der äußerste Decorator (`LoggingDecorator<Cmd, Unit>`) nur
`IRequestHandler<Cmd, Unit>` implementiert, nicht `IRequestHandler<Cmd>`. Die Fassade holt sich per
Konstruktor-Injektion die dekorierte Kette und ist damit automatisch immer außen.

## 4. Zieldesign

### 4.1 `DecoratR.Abstractions`

1. **`Unit`** – `public readonly record struct Unit` mit `Unit.Value` und `ToString() => "()"`.
   Struct, damit `where TResponse : class`-Decorators Void-Handler automatisch ausschließen.
2. **`IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>`** – wie in Abschnitt 3, mit `new`-Member
   `ValueTask HandleAsync(...)` und explizit implementierter DIM-Brücke. Abstractions targetet net8/9/10
   ([DecoratR.Abstractions.csproj:6](../src/DecoratR.Abstractions/DecoratR.Abstractions.csproj)), DIM ist also verfügbar.
3. **`VoidRequestHandler<TRequest>`** (Name offen, siehe 7) – öffentliche Infrastrukturklasse mit
   `[EditorBrowsable(EditorBrowsableState.Never)]`, analog zu den `DecoratR.Metadata`-Attributen. Sie muss public
   sein, weil der generierte Library-Registry-Code aus fremden Assemblies `typeof(...)` darauf bildet.

Kein `IRequest<TResponse>` und kein `ICommand`-Marker: DecoratR koppelt den Response-Typ bewusst nur an den
Handler; für Void-Handler braucht der Request keine Sonderform.

### 4.2 Generator

| Schritt | Änderung | Betroffene Datei |
|---------|----------|------------------|
| Detection | `IsVoidHandlerInterface` für `DecoratR.IRequestHandler\`1`; in `HandlerDetector.Detect` merken, ob `AllInterfaces` das Ein-Parameter-Interface für denselben `TRequest` enthält. | [SymbolExtensions.cs:38](../src/DecoratR.Generator/Detection/SymbolExtensions.cs), [HandlerDetector.cs:49-71](../src/DecoratR.Generator/Detection/HandlerDetector.cs) |
| Modell | `HandlerMetadata` (nicht `ServiceTypeInfo`) bekommt `bool RegistersVoidFacade`. Service-Typ, Vergleich und `SameService` bleiben unverändert, damit DCTR007 und Planner weiter über `<TRequest, Unit>` arbeiten. | [HandlerMetadata.cs:7](../src/DecoratR.Generator/Model/HandlerMetadata.cs) |
| Emit (Host) | Nach der Handler-Registrierung zusätzlich `services.Add(new ServiceDescriptor(typeof(IRequestHandler<Cmd>), typeof(VoidRequestHandler<Cmd>), options.Lifetime))`. Keine Factory nötig, der Container injiziert `IRequestHandler<Cmd, Unit>`. | [RegistrationsEmitter.cs:96-110](../src/DecoratR.Generator/Emit/RegistrationsEmitter.cs) |
| Emit (Library) | Gleiches Paar als zusätzlicher `HandlerRegistration`-Eintrag im `Handlers`-Array. Die Composition Root iteriert das Array unverändert; `ImplementationType` trägt bereits `DynamicallyAccessedMembers(PublicConstructors)`. | [HandlerRegistryEmitter.cs:48-53](../src/DecoratR.Generator/Emit/HandlerRegistryEmitter.cs) |
| Metadaten | **Keine** Änderung an `[DecoratRHandler]` oder `[DecoratRDecorator]`. Die Fassade ist kein Dekorationsziel, und der Scanner braucht sie nicht zu kennen. Das v2-Format bleibt kompatibel. | [ReferencedAssemblyScanner.cs:73-94](../src/DecoratR.Generator/Detection/ReferencedAssemblyScanner.cs) |
| Planner | Unverändert. Die Fassade taucht in keiner `ServiceTypeInfo` auf und wird nie dekoriert. | [DecorationPlanner.cs](../src/DecoratR.Generator/Emit/DecorationPlanner.cs) |
| Diagnostics | DCTR005 bekommt einen Hinweis, wenn ein `[Decorator]` `IRequestHandler<TRequest>` (ein Parameter) implementiert: Decorators sind immer zweiparametrig, `TResponse` ist für Void-Handler `Unit`. Optional als eigene ID (DCTR015) mit klarerer Meldung. | [DecoratorDetector.cs:43-49](../src/DecoratR.Generator/Detection/DecoratorDetector.cs), [Diagnostics.cs](../src/DecoratR.Generator/Diagnostics.cs) |
| WellKnownTypes | Konstanten für `IRequestHandler\`1`, `global::DecoratR.Unit`, `global::DecoratR.VoidRequestHandler`. | [WellKnownTypes.cs](../src/DecoratR.Generator/WellKnownTypes.cs) |

Randfälle, die das Design bereits abdeckt:

- Ein Handler implementiert `IRequestHandler<Cmd>` **und** explizit `IRequestHandler<Cmd, Unit>`: eine Klasse,
  ein Service-Typ, eine Fassade. Der explizite Member überschreibt die DIM-Brücke.
- Zwei Klassen, eine mit `IRequestHandler<Cmd>`, eine mit `IRequestHandler<Cmd, Unit>`: DCTR007 wie bisher, weil
  `SameService` über `<Cmd, Unit>` vergleicht.
- Handler, der direkt `IRequestHandler<Cmd, Unit>` implementiert, ohne `IRequestHandler<Cmd>`: bleibt erlaubt,
  bekommt aber keine Fassade. So kann niemand versehentlich `IRequestHandler<Cmd>` auflösen, das nicht existiert.
- Interner Request-Typ in einer Library: die Fassade landet im `Handlers`-Array der Library selbst, also im
  gleichen Assembly wie der Request. DCTR011/DCTR013 bleiben allein für die Dekoration relevant.
- `DecoratROptions.Lifetime`: Fassade und Handler bekommen dieselbe Lifetime; bei Singleton wird die dekorierte
  Kette einmal aufgelöst.

### 4.3 Laufzeitkosten

Pro Auflösung ein zusätzliches Fassaden-Objekt und ein Interface-Aufruf. Für die beiden Brücken
(`ValueTask` → `ValueTask<Unit>` in der DIM, `ValueTask<Unit>` → `ValueTask` in der Fassade) wurden drei
Implementierungen mit BenchmarkDotNet verglichen (Scratchpad, .NET 10, Apple Silicon, ShortRun, ein
`async ValueTask<T>`-Decorator zwischen Fassade und Handler; Baseline ist derselbe Aufbau mit einem echten
`IRequestHandler<Cmd, Unit>` ohne Brücken):

| Variante | sync (ns) | sync alloc | async via `Task.Yield` (ns) | async alloc |
|----------|-----------|------------|-----------------------------|-------------|
| Baseline ohne Brücken | 17,6 | 0 B | 1 888 | 229 B |
| A: beide Brücken als `async`-Methoden | 22,2 | 0 B | 1 985 | 467 B |
| **B: Fast-Path + `AsTask()` (Fassade), Fast-Path + Await-Helper (DIM)** | **18,0** | **0 B** | **1 942** | **347 B** |
| C: wie A mit `PoolingAsyncValueTaskMethodBuilder` | 24,2 | 0 B | 1 929 | 346 B |

Gewählt ist B: im synchronen Pfad praktisch kostenlos (+0,4 ns, keine State-Machine), im asynchronen Pfad nur die
eine Box des Await-Helpers in der DIM. Die Fassade ist dort allokationsfrei, weil `AsTask()` den Task zurückgibt,
der die `ValueTask<Unit>` eines `async ValueTask<T>`-Decorators ohnehin trägt. Die verbleibenden ~118 B sind
konzeptionell nicht vermeidbar: eine `ValueTask` lässt sich ohne Allokation nicht in eine `ValueTask<Unit>`
verwandeln, sobald sie noch nicht abgeschlossen ist. Pooling (C) verkürzt nichts und bringt die bekannten
Fallstricke gepoolter `ValueTask`s mit.

## 5. Verworfene Alternativen

| Alternative | Warum nicht |
|-------------|-------------|
| **Eigene Void-Decorator-Familie** (`IRequestHandler<TRequest>` mit eigenen `[Decorator]`-Klassen, ein Typparameter) | Jeder Cross-Cutting-Decorator müsste doppelt geschrieben werden. Genau das vermeiden MediatR und Mediator mit `Unit`. |
| **Status quo dokumentieren** (Nutzer definiert `Unit` selbst) | Jede App hat ihren eigenen `Unit`; Library-übergreifend zwei verschiedene `Unit`-Typen ergeben verschiedene Service-Typen. Zumindest ein DecoratR-eigenes `Unit` ist Pflicht. |
| **`Unit` nur bereitstellen, Handler schreiben `ValueTask<Unit>`** (Mediator-Stil) | Löst das Doppel-`Unit`-Problem, aber nicht die eigentliche Frage; Handler-Autor und Aufrufer sehen `Unit` weiter. |
| **Generator emittiert pro Void-Handler eine Adapterklasse** statt DIM-Brücke | Mehr generierter Code, Adapter müsste den konkreten Handler auflösen (zusätzliche Registrierung der Implementierungsklasse). Die DIM-Brücke liegt einmal in Abstractions und ist einmal getestet. |
| **Offene Generic-Registrierung** `typeof(IRequestHandler<>) → typeof(VoidRequestHandler<>)` | Nur eine Zeile, aber `MakeGenericType` zur Laufzeit widerspricht dem AOT-first-Prinzip und der bisherigen Praxis geschlossener Registrierungen ([architecture.md:109-111](../architecture.md)). |
| **`IRequest<TResponse>` / `ICommand` auf Request-Ebene** (MediatR-Stil) | Orthogonale Designänderung; DecoratR bindet den Response-Typ bewusst an den Handler. Nicht nötig für dieses Feature. |
| **Fassade als private Klasse in jeden generierten Registry/Host emittieren** | Möglich und ohne öffentliche API, aber drei Kopien derselben Klasse und kein Ort für einen Unit-Test. Nur sinnvoll, falls die public Infrastrukturklasse in Abstractions unerwünscht ist (siehe 7). |

## 6. Umsetzungsplan

### Phase 1 – Abstractions

1. `Unit`, `IRequestHandler<TRequest>` und `VoidRequestHandler<TRequest>` in `src/DecoratR.Abstractions` anlegen,
   XML-Doku inklusive Hinweis, dass Decorators `Unit` als `TResponse` sehen.
2. Unit-Tests (neues kleines Testprojekt oder in `DecoratR.IntegrationTests`): DIM-Brücke synchron/asynchron,
   Exception-Propagation, Cancellation, Fassade.

### Phase 2 – Generator

3. `WellKnownTypes`, `SymbolExtensions.IsVoidHandlerInterface`, `HandlerMetadata.RegistersVoidFacade`.
4. `HandlerDetector.Detect`: Flag setzen, wenn `AllInterfaces` `IRequestHandler<TRequest>` mit dem gleichen
   Request-Typ wie das gefundene `IRequestHandler<TRequest, Unit>` enthält.
5. `RegistrationsEmitter` und `HandlerRegistryEmitter`: Fassaden-Registrierung emittieren.
   `IncrementalCachingTests` prüfen, dass das neue Flag in den Records strukturell vergleichbar ist.
6. DCTR005-Meldung erweitern (oder DCTR015), `AnalyzerReleases.Unshipped.md` pflegen.

### Phase 3 – Tests

7. `HandlerDiscoveryTests`: Void-Handler → Service-Typ `<Cmd, Unit>` + Fassaden-Registrierung im Host.
8. `CrossAssemblyTests` / `HandlerRegistryTests`: Fassade im Library-`Handlers`-Array, Host registriert sie mit.
9. `ConstraintFilteringTests`: `where TResponse : class` überspringt Void-Handler; `where TRequest : ICommand` greift.
10. `DiagnosticsTests`: `[Decorator]` mit `IRequestHandler<TRequest>` → DCTR005/DCTR015; zwei Void-Handler → DCTR007.
11. Snapshots (`Host_SingleAssembly`, `Library_HandlerRegistry`) um einen Void-Handler ergänzen.
12. `DecoratR.IntegrationTests`: Void-Handler lokal und aus der Library auflösen, Decorator-Reihenfolge, Lifetime.
    Projekt hat `IsAotCompatible=true`, also zugleich der AOT-Check.

### Phase 4 – Doku und Beispiele

13. `docs/guide.md` „Requests and handlers“ und „Resolve handlers from DI“ um Void-Handler ergänzen; Abschnitt
    „Constrained Decorators“ um den `Unit`/`!struct`-Hinweis.
14. README-Featureliste, `architecture.md` Detection Rules.
15. `examples/simple-api`: `CompleteTodoCommand` als Void-Beispiel; Playground-Endpoint mit
    `IRequestHandler<GreetCommand>` falls sinnvoll.
16. Changelog: additive Änderung, kein Breaking Change am Metadatenformat.

## 7. Offene Entscheidungen

1. **Name des Ergebnistyps:** `Unit` (Konvention aus MediatR/Mediator/LanguageExt, wiedererkennbar) oder ein
   DecoratR-eigener Name wie `Void`/`None`. Empfehlung: `Unit`, da er ohnehin fast unsichtbar ist.
2. **Fassade in Abstractions (public, `EditorBrowsable.Never`) oder je Assembly generiert (private)?**
   Empfehlung: Abstractions, siehe Abschnitt 5, letzte Zeile.
3. **Eigene Diagnostic-ID** für „Decorator implementiert das Ein-Parameter-Interface“ oder erweiterte DCTR005-Meldung.
   Empfehlung: erweiterte DCTR005-Meldung, solange keine eigene Fix-Logik nötig ist.
4. **Soll `IRequestHandler<Cmd, Unit>` weiterhin öffentlich auflösbar bleiben?** Technisch unvermeidbar (der
   Container braucht den Service-Typ für die Dekoration); dokumentieren, nicht verstecken.
