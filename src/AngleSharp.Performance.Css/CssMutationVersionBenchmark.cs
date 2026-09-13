namespace AngleSharp.Performance.Css
{
    using AngleSharp.Css.Dom;
    using AngleSharp.Css.Parser;
    using AngleSharp.Dom;
    using BenchmarkDotNet.Attributes;
    using System;

    /// <summary>
    /// Measures native CSSOM writes and the parse/read controls with an isolated sheet per process.
    /// Parsing a new sheet is measured only by ParseSheet; other cases reuse setup state.
    /// </summary>
    [MemoryDiagnoser]
    public class CssMutationVersionBenchmark
    {
        private const String Source = "@media screen { div { display: block; width: 10px; color: red } } a { margin: 0; padding: 2px }";
        private CssParser _parser = default!;
        private ICssStyleSheet _sheet = default!;
        private ICssStyleDeclaration _style = default!;
        private ICssMediaRule _media = default!;
        private Boolean _toggle;

        [GlobalSetup]
        public void Setup()
        {
            _parser = new CssParser();
            _sheet = _parser.ParseStyleSheet(Source);
            _media = (ICssMediaRule)_sheet.Rules[0];
            _style = ((ICssStyleRule)_media.Rules[0]).Style;
        }

        [Benchmark]
        public ICssStyleSheet ParseSheet() => _parser.ParseStyleSheet(Source);

        [Benchmark]
        public String ReadProperty() => _style.GetPropertyValue("display");

        [Benchmark]
        public void SetProperty()
        {
            _toggle = !_toggle;
            _style.SetProperty("display", _toggle ? "block" : "none");
        }

        [Benchmark]
        public void SetMedia()
        {
            _toggle = !_toggle;
            _media.Media.MediaText = _toggle ? "screen" : "print";
        }

        [Benchmark]
        public void InsertRemoveRule()
        {
            _sheet.Insert("b { display: none }", 2);
            _sheet.RemoveAt(2);
        }
    }
}
