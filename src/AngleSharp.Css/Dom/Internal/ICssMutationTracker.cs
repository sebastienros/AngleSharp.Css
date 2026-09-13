namespace AngleSharp.Css.Dom
{
    // Shared by sheets and rules so lists can notify their current owner without callbacks.
    internal interface ICssMutationTracker
    {
        void MarkChanged();
    }
}
