# Runtime integration fork

This EvotecIT fork builds on AngleSharp v1.8.3. The upstream source and license remain
in place. OfficeIMO uses the integration branch while the patches are qualified and
prepared for discussion with upstream maintainers.

The maintained changes cover:

- DOM ownership, detached-node comparisons, dataset names, event dispatch, and mutation observation.
- ID and class selectors fold ASCII case in quirks-mode documents and retain case
  sensitivity in standards mode.
- `:lang()` resolves element and ancestor declarations, giving XML `lang` precedence
  over HTML or SVG `lang`, ignoring unrelated attribute namespaces, and matching
  complete language subtags rather than arbitrary string prefixes.
- HTML 4.01 Transitional and Frameset doctypes select quirks mode when the system
  identifier is missing or empty, and limited quirks when it is nonempty. Document
  parsing and public doctype tokens use the same classification.
- String and stream fragment parsing and DOM markup setters preserve the context
  document's quirks or limited-quirks mode before constructing and adopting nodes.
- Native `Unloading` handlers subscribe to the standard `beforeunload` event name.
- Native listener removal notifies script hosts with the removed callback and capture
  flag, allowing them to invalidate cached handler properties after bulk removal.
- The first connected HTML base element owns the frozen document base URL. DOM
  insertion, removal, attribute changes, adoption and cloning update that state;
  invalid, data and JavaScript bases freeze the fallback URL. Detached, later,
  template-content and foreign elements cannot replace the active base.
- Optional parser synchronization, mutation microtask scheduling, and synchronous mutation notification services.
- Reentrant `document.write`, script preparation and ordering, stylesheet blockers, and document readiness.
- Document replacement preserves the document, window, origin and queued tasks;
  clears connected listeners; reports the DOM replacement; and applies the entry
  document's URL only when the target is fully active. Host unload scopes and
  parser-executed scripts suppress destructive input-stream operations.
  This does not implement a script-created incremental input parser.
- Initial auxiliary documents have an HTML/head/body tree, inherited base and
  origin, and a creator referrer. Host code retains ownership of opening policy,
  quotas, navigation and realm lifetime.
- Frame sandbox propagation and integrity metadata captured at resource preparation.
- Local iframe documents keep their `about:blank` / `about:srcdoc` identity and
  snapshot the creator's base URL and origin. Empty frames initialize after
  connected element setup; authored sources do not execute while detached.
  Opaque sandbox origins remain distinct from inherited resource bases.
- Standalone observer notification batches continue after a callback failure, then
  report collected exceptions to the owning event loop. Public CORS requests retain
  source-element integrity requirements unless explicitly overridden; empty metadata
  disables the check, and cross-origin no-CORS responses cannot satisfy integrity.

Host-specific resource budgets, import maps, module resolution, navigation policy,
and rendering stay in OfficeIMO. The optional services must preserve ordinary
AngleSharp use when no host implements them.

The native beforeunload wiring was accepted in
[AngleSharp #1354](https://github.com/AngleSharp/AngleSharp/pull/1354), preserving
the existing `Unloading` constant and using `BeforeUnload` for subscriptions.
Frozen base URLs were accepted in
[AngleSharp #1355](https://github.com/AngleSharp/AngleSharp/pull/1355).
The native listener-removal notification was accepted in
[AngleSharp #1356](https://github.com/AngleSharp/AngleSharp/pull/1356).
Queued timer callbacks recheck cancellation when executed, including window
disposal after the delay has elapsed. This fix was accepted in
[AngleSharp #1357](https://github.com/AngleSharp/AngleSharp/pull/1357).

Document form policy now inherits the browsing-context sandbox. Rejected
submissions stop before target lookup or navigation in both asynchronous
overloads. This fix was accepted in
[AngleSharp #1358](https://github.com/AngleSharp/AngleSharp/pull/1358).

The released fixes and their regression cases remain in the integration fork.
Target dispatch invokes capturing listeners before non-capturing listeners, with
a fresh listener snapshot for each invocation. Capture-time listener changes and
propagation flags retain their standard target semantics.
Its document base URL state also retains the host's frame, template-content and
document-replacement lifecycle behavior. An explicit document base override wins
over an authored base without becoming the fallback used to freeze that base.

Build and test from this repository:

```sh
dotnet build src/AngleSharp/AngleSharp.Core.csproj -f net10.0
dotnet test src/AngleSharp.Core.Tests/AngleSharp.Core.Tests.csproj -f net10.0
prefetched=true dotnet test src/AngleSharp.Core.Tests/AngleSharp.Core.Tests.csproj -f net10.0
```

Keep changes against upstream reviewable and preserve new upstream fixes during
integration. Qualify both the standalone library and the consuming runtime before
updating an OfficeIMO revision pin. Fork builds are not official AngleSharp releases.
Do not publish packages under upstream package identities.
