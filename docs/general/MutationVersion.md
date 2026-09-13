# CSSOM mutation versions

A host that caches stylesheet-derived work can read `sheet.GetMutationVersion()` before and after a query. The nullable `Int64` is an opaque equality token. It advances synchronously for native CSSOM writes, including declaration/property edits, selector and condition changes, rule-list edits, sheet media and disabled state. Reading it allocates nothing. A no-op or failed operation that already changed state may advance it; do not use its magnitude or ordering. Read and mutate on the thread that owns the CSSOM.

Parser construction does not advance the version. Normal synchronous and asynchronous parses finish at
zero; no counter reset is used, so user mutations made in parser callbacks remain visible. Parsing a
rule with an existing sheet as its owner also leaves that sheet's version unchanged. User insertion or
`CssText` replacement publishes the change after installing the parsed result. Parser construction uses
raw rule-list and declaration operations, and the parser's selector/condition initialization does not
call the notifying setters. Hosts must invalidate across parsing/resumption and resource/import loading
boundaries independently; a stylesheet version is not a signal that loading completed.

This is a separate signal from the document's DOM mutations. For example:

```csharp
var sheet = (ICssStyleSheet)document.StyleSheets[0];
var rule = (ICssStyleRule)sheet.Rules[0];
var version = sheet.GetMutationVersion();
rule.Style.SetProperty("display", "none");
// The style element's text and DOM attributes have not changed.
// A DOM MutationObserver receives no record, but the stylesheet version changes.
```

The motivating consumer is Jint.Browser's flat layout: an unchanged geometry read should reuse prior work, while a read immediately after a native host CSSOM write must recompute it. An external wrapper only sees writes made through that wrapper. CSSOM implementations are internal and sealed, and existing interfaces offer no synchronous change hook to intercept these native writes. DOM observers cannot report these changes, even if their record queue is drained synchronously. Walking or serializing the CSSOM can detect changes, but repeats work proportional to stylesheet size on every otherwise-unchanged read. The notification therefore belongs at the native mutation points; the cache and layout policy remain outside AngleSharp.Css.

Track each imported sheet separately. Also track document changes, stylesheet membership/loading, render-device/media environment and host-owned selector state. The version does not claim to version these other inputs. Unsupported stylesheet implementations return `null`; custom rules, selectors, properties or mutable value objects need an explicit invalidation policy or an uncached fallback. The new extension does not add a member to `ICssStyleSheet` or require third-party implementations to change.

The native-write reproducer was also run against the unmodified `devel` source at `650fb47`, using AngleSharp `1.8.2-beta.715` (which already has the DOM version). A cache keyed by both the DOM version and observer record count still returned the old display value:

```text
DOM version unchanged: True
DOM markup unchanged: True
Mutation records: 0
Cached display: block; actual display: none
```

`NativeCssomMutationIsInvisibleToADomObserver` keeps this case in the test suite and verifies that the new stylesheet revision allows the cache to refresh. It also checks that computing style does not change the source sheet's version.
