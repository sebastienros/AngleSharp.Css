namespace AngleSharp.Css.Dom
{
    using System;

    /// <summary>
    /// Declaration construction without CSSOM mutation notifications.
    /// </summary>
    interface ICssDeclarationBuilder
    {
        void SetProperty(String name, String value, String priority);
    }
}
