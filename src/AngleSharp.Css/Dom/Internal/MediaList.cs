#nullable disable
namespace AngleSharp.Css.Dom
{
    using AngleSharp.Css.Parser;
    using AngleSharp.Dom;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Represents a list of media elements.
    /// </summary>
    sealed class MediaList : IMediaList
    {
        #region Fields

        private readonly IBrowsingContext _context;
        private readonly ICssMutationTracker _owner;
        private readonly List<ICssMedium> _media;

        private static readonly CssMedium replacementMedium = new(CssKeywords.All, inverse: true, exclusive: false);

        #endregion

        #region ctor

        internal MediaList(IBrowsingContext context, ICssMutationTracker owner = null)
        {
            _context = context;
            _owner = owner;
            _media = new List<ICssMedium>();
        }

        #endregion

        #region Index

        public String this[Int32 index] => _media[index].ToCss();

        #endregion

        #region Properties

        public Int32 Length => _media.Count;

        public ICssParser Parser => _context.GetService<ICssParser>();

        public IFeatureValidatorFactory ValidatorFactory => _context.GetService<IFeatureValidatorFactory>();

        public String MediaText
        {
            get => this.ToCss();
            set
            {
                // Core initializes an attached sheet's empty media through this public setter.
                if (_media.Count == 0 && String.IsNullOrEmpty(value))
                {
                    return;
                }

                _media.Clear();
                _owner?.MarkChanged();
                FillMediaText(value, throwOnError: true);
            }
        }

        #endregion

        #region Methods

        public void SetMediaText(String value, Boolean throwOnError)
        {
            _media.Clear();
            FillMediaText(value, throwOnError);
        }

        private void FillMediaText(String value, Boolean throwOnError)
        {
            var v = String.IsNullOrEmpty(value) ? String.Empty : value;
            var media = MediaParser.Parse(v, ValidatorFactory) ?? Enumerable.Repeat<CssMedium>(null, 1);

            if (throwOnError && media.Contains(null))
            {
                throw new DomException(DomError.Syntax);
            }

            _media.AddRange(media.Select(m => m ?? replacementMedium));
        }

        public void Add(String newMedium)
        {
            var medium = MediumParser.Parse(newMedium, ValidatorFactory) ?? throw new DomException(DomError.Syntax);
            _media.Add(medium);
            _owner?.MarkChanged();
        }

        public void Remove(String oldMedium)
        {
            var medium = MediumParser.Parse(oldMedium, ValidatorFactory) ?? throw new DomException(DomError.Syntax);

            for (var i = 0; i < _media.Count; i++)
            {
                if (_media[i].Equals(medium))
                {
                    _media.RemoveAt(i);
                    _owner?.MarkChanged();
                    return;
                }
            }

            throw new DomException(DomError.NotFound);
        }

        public void Replace(IEnumerable<ICssMedium> media)
        {
            _media.Clear();
            _owner?.MarkChanged();
            _media.AddRange(media);
        }

        public void ToCss(TextWriter writer, IStyleFormatter formatter)
        {
            if (_media.Count > 0)
            {
                _media[0].ToCss(writer, formatter);

                for (var i = 1; i < _media.Count; i++)
                {
                    writer.Write(", ");
                    _media[i].ToCss(writer, formatter);
                }
            }
        }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<ICssMedium> GetEnumerator() => _media.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
