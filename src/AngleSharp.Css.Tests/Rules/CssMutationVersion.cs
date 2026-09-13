namespace AngleSharp.Css.Tests.Rules
{
    using AngleSharp.Css.Dom;
    using AngleSharp.Css.Dom.Events;
    using AngleSharp.Css.Parser;
    using AngleSharp.Css.Tests.Mocks;
    using AngleSharp.Io;
    using AngleSharp.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static CssConstructionFunctions;

    [TestFixture]
    public class CssMutationVersionTests
    {
        private static IEnumerable<TestCaseData> RuleChanges()
        {
            yield return Change("declaration", "a { display: block }", r => ((ICssStyleRule)r).Style.SetProperty("display", "none"));
            yield return Change("remove declaration", "a { display: block }", r => ((ICssStyleRule)r).Style.RemoveProperty("display"));
            yield return Change("declaration text", "a { display: block }", r => ((ICssStyleRule)r).Style.CssText = "");
            yield return Change("property value", "a { display: block }", r => ((ICssStyleRule)r).Style.GetProperty("display")!.Value = "none");
            yield return Change("property priority", "a { display: block }", r => ((ICssStyleRule)r).Style.GetProperty("display")!.IsImportant = true);
            yield return Change("selector", "a { display: block }", r => ((ICssStyleRule)r).SelectorText = "b");
            yield return Change("rule text", "a { display: block }", r => r.CssText = "b { display: none }");
            yield return Change("nested declaration", "@media screen { a { display: block } }", r => ((ICssStyleRule)((ICssMediaRule)r).Rules[0]).Style.SetProperty("display", "none"));
            yield return Change("group insertion", "@media screen {}", r => ((ICssMediaRule)r).Insert("a {}", 0));
            yield return Change("group removal", "@media screen { a {} }", r => ((ICssMediaRule)r).RemoveAt(0));
            yield return Change("group replacement", "@media screen { a {} }", r => r.CssText = "@media print {}");
            yield return Change("media text", "@media screen {}", r => ((ICssMediaRule)r).Media.MediaText = "print");
            yield return Change("media append", "@media screen {}", r => ((ICssMediaRule)r).Media.Add("print"));
            yield return Change("media removal", "@media screen {}", r => ((ICssMediaRule)r).Media.Remove("screen"));
            yield return Change("supports condition", "@supports (display: block) {}", r => ((ICssSupportsRule)r).ConditionText = "(display: none)");
            yield return Change("container condition", "@container (width > 1px) {}", r => ((ICssContainerRule)r).ConditionText = "(width > 2px)");
            yield return Change("scope", "@scope (.a) {}", r => ((ICssScopeRule)r).ScopeText = "(.b)");
            yield return Change("page selector", "@page :left { margin: 1px }", r => ((ICssPageRule)r).SelectorText = ":right");
            yield return Change("keyframes name", "@keyframes a { from { opacity: 0 } }", r => ((ICssKeyframesRule)r).Name = "b");
            yield return Change("keyframe selector", "@keyframes a { from { opacity: 0 } }", r => ((ICssKeyframeRule)((ICssKeyframesRule)r).Rules[0]).KeyText = "to");
            yield return Change("font declaration", "@font-face { font-family: a }", r => ((ICssFontFaceRule)r).Family = "b");
            yield return Change("descriptor", "@property --x { syntax: '*'; inherits: false }", r => ((ICssPropertyRule)r).SetProperty("inherits", "true"));
            yield return Change("descriptor property", "@property --x { syntax: '*'; inherits: false }", r => ((ICssPropertyRule)r).GetProperty("inherits")!.Value = "true");
        }

        private static TestCaseData Change(String name, String css, Action<ICssRule> change) =>
            new TestCaseData(css, change).SetName("MutationVersion: " + name);

        [TestCaseSource(nameof(RuleChanges))]
        public void RuleMutationAdvancesVersion(String css, Action<ICssRule> change)
        {
            var sheet = ParseStyleSheet(css);
            var before = sheet.GetMutationVersion();
            Assert.AreEqual(0, before, "Constructing a stylesheet must not advance its version.");

            change(sheet.Rules[0]);

            Assert.AreNotEqual(before, sheet.GetMutationVersion());
            var after = sheet.GetMutationVersion();
            Assert.IsNotNull(sheet.ToCss());
            Assert.AreEqual(after, sheet.GetMutationVersion(), "Reading CSS must not invalidate it.");
        }

        [TestCase("@charset 'utf-8'; @import 'a.css' screen; @namespace x 'urn:x'; x|a { color: red !important }")]
        [TestCase("@document url('https://example.org') { a { color: red } }")]
        [TestCase("@supports (display: block) { @container (width > 1px) { a { & b { display: none } } } }")]
        [TestCase("@property --x { syntax: '*'; inherits: false } @font-face { font-family: a; src: url(a.woff) }")]
        [TestCase("@scope (.a) { a {} } @keyframes a { from { opacity: 0 } } @page :left { margin: 1px }")]
        [TestCase("@media screen { a { color: red } } @supports INVALID {} b { color: red; invalid }")]
        public async Task SyncAndAsyncConstructionLeaveVersionAtZero(String css)
        {
            var parser = new CssParser();
            var synchronous = parser.ParseStyleSheet(css);
            var asynchronous = await parser.ParseStyleSheetAsync(css).ConfigureAwait(false);

            Assert.Greater(synchronous.Rules.Length, 0);
            Assert.AreEqual(0, synchronous.GetMutationVersion());
            Assert.AreEqual(0, asynchronous.GetMutationVersion());
        }

        [TestCase("@media screen { a { color: red } }")]
        [TestCase("@font-face { font-family: a }")]
        [TestCase("@property --x { syntax: '*'; inherits: false }")]
        [TestCase("@supports INVALID {}")]
        public void ParsingARuleDoesNotMutateItsOwner(String css)
        {
            var parser = new CssParser();
            var sheet = parser.ParseStyleSheet("a {}");
            sheet.IsDisabled = true;
            var before = sheet.GetMutationVersion();

            parser.ParseRule(sheet, css);

            Assert.AreEqual(before, sheet.GetMutationVersion());
            Assert.AreEqual(1, sheet.Rules.Length);
        }

        [Test]
        public void ConstructionDoesNotResetUserMutationsInParseCallbacks()
        {
            var parser = new CssParser();
            Int64? afterUserMutation = null;
            parser.Parsing += (_, ev) =>
            {
                var sheet = ((CssParseEvent)ev).StyleSheet;
                Assert.AreEqual(0, sheet.GetMutationVersion());
                sheet.IsDisabled = true;
                afterUserMutation = sheet.GetMutationVersion();
            };

            var result = parser.ParseStyleSheet("@media screen { a { display: block } }");

            Assert.Greater(afterUserMutation, 0);
            Assert.AreEqual(afterUserMutation, result.GetMutationVersion());
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task LoadingAStylesheetAndItsImportsLeavesVersionsAtZero(Boolean disabled)
        {
            var files = new Dictionary<String, String> { { "child.css", "a { color: red }" } };
            var config = Configuration.Default.With(new TestServerRequester(files))
                .WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }).WithCss();
            using var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Address("http://localhost/index.html")
                .Content("<style></style>")).ConfigureAwait(false);
            using var response = new DefaultResponse
            {
                Address = new Url("http://localhost/parent.css"),
                Content = new MemoryStream(Encoding.UTF8.GetBytes("@import 'child.css';"))
            };
            var options = new StyleOptions(document) { Element = document.QuerySelector("style"), IsDisabled = disabled };

            var sheet = (ICssStyleSheet)await new CssStylingService()
                .ParseStylesheetAsync(response, options, CancellationToken.None).ConfigureAwait(false);
            var imported = ((ICssImportRule)sheet.Rules[0]).Sheet;

            Assert.AreEqual(disabled, sheet.IsDisabled);
            Assert.IsNotNull(imported);
            Assert.AreEqual(1, imported!.Rules.Length);
            Assert.AreEqual(0, sheet.GetMutationVersion());
            Assert.AreEqual(0, imported.GetMutationVersion());
        }

        [Test]
        public void SheetMutationsAdvanceOnlyTheirOwnVersion()
        {
            var first = ParseStyleSheet("a {}");
            var second = ParseStyleSheet("b {}");
            var firstVersion = first.GetMutationVersion();
            var before = second.GetMutationVersion();
            second.Insert("c {}", 1);
            Assert.AreNotEqual(before, second.GetMutationVersion());
            before = second.GetMutationVersion();
            second.RemoveAt(1);
            Assert.AreNotEqual(before, second.GetMutationVersion());
            before = second.GetMutationVersion();
            second.IsDisabled = true;
            Assert.AreNotEqual(before, second.GetMutationVersion());
            before = second.GetMutationVersion();
            second.Media.MediaText = "print";
            Assert.AreNotEqual(before, second.GetMutationVersion());
            Assert.AreEqual(firstVersion, first.GetMutationVersion());
        }

        [Test]
        public void RevisionAdvancesBeforeDeclarationCallbacks()
        {
            var sheet = ParseStyleSheet("a { display: block }");
            var style = (CssStyleDeclaration)((ICssStyleRule)sheet.Rules[0]).Style;
            var before = sheet.GetMutationVersion();
            var observed = before;
            style.Changed += _ => observed = sheet.GetMutationVersion();

            style.SetProperty("display", "none");

            Assert.AreNotEqual(before, observed);
        }

        [Test]
        public void ReentrantDeclarationMutationAdvancesVersionWithoutRepeatingCallbacks()
        {
            var sheet = ParseStyleSheet("a { display: block }");
            var style = (CssStyleDeclaration)((ICssStyleRule)sheet.Rules[0]).Style;
            var callbacks = 0;
            style.Changed += _ =>
            {
                callbacks++;
                var before = sheet.GetMutationVersion();
                style.SetProperty("color", "red");
                Assert.AreNotEqual(before, sheet.GetMutationVersion());
            };

            style.SetProperty("display", "none");

            Assert.AreEqual(1, callbacks);
        }

        [Test]
        public void InvalidMediaThatClearsStateStillAdvancesVersion()
        {
            var sheet = ParseStyleSheet("a {}");
            sheet.Media.MediaText = "screen";
            var before = sheet.GetMutationVersion();

            Assert.Throws<DomException>(() => sheet.Media.MediaText = "@");

            Assert.AreNotEqual(before, sheet.GetMutationVersion());
        }

        [Test]
        public void DetachedGroupUsesItsNewSheet()
        {
            var first = ParseStyleSheet("@media screen { a { display: block } }");
            var group = first.Rules[0];
            var second = ParseStyleSheet("");
            first.RemoveAt(0);
            ((CssStyleSheet)second).Add(group);
            var firstVersion = first.GetMutationVersion();
            var secondVersion = second.GetMutationVersion();

            ((ICssStyleRule)((ICssMediaRule)group).Rules[0]).Style.SetProperty("display", "none");

            Assert.AreEqual(firstVersion, first.GetMutationVersion());
            Assert.AreNotEqual(secondVersion, second.GetMutationVersion());
        }

        [Test]
        public async Task NativeCssomMutationIsInvisibleToADomObserver()
        {
            using var context = BrowsingContext.New(Configuration.Default.WithCss());
            var document = await context.OpenAsync(req => req.Content("<style>div { display: block }</style><div></div>")).ConfigureAwait(false);
            var sheet = (ICssStyleSheet)document.StyleSheets[0]!;
            Assert.AreEqual(0, sheet.GetMutationVersion());
            var rule = (ICssStyleRule)sheet.Rules[0];
            var markup = document.DocumentElement.OuterHtml;
            var records = 0;
            var observer = new MutationObserver((changes, _) => records += changes.Length);
            observer.Connect(document, childList: true, subtree: true, attributes: true, characterData: true);
            var version = sheet.GetMutationVersion();
            var cachedDisplay = rule.Style.GetPropertyValue("display");

            // Native CSSOM writes do not rewrite the style element's text or pass through a host wrapper.
            rule.Style.SetProperty("display", "none");
            if (version != sheet.GetMutationVersion())
            {
                cachedDisplay = rule.Style.GetPropertyValue("display");
            }

            Assert.AreEqual(markup, document.DocumentElement.OuterHtml);
            Assert.AreEqual(0, records);
            Assert.AreEqual("none", cachedDisplay);
            var after = sheet.GetMutationVersion();
            Assert.AreEqual("none", document.DefaultView!.GetComputedStyle(document.QuerySelector("div")!).GetPropertyValue("display"));
            Assert.AreEqual(after, sheet.GetMutationVersion(), "Computing style must not invalidate the source sheet.");
        }
    }
}
