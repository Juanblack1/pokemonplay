using System.Windows.Forms;

// Opaque, buffered surfaces keep native scrolling from copying a transparent
// parent's old pixels into the newly exposed portion of a card.
internal sealed class LibraryScrollPanel : BufferedPanel
{
    public LibraryScrollPanel()
    {
        BackColor = AppTheme.Background;
        AutoScroll = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnScroll(ScrollEventArgs e)
    {
        base.OnScroll(e);
        Invalidate(true);
    }
}

internal sealed class LibraryFlowPanel : FlowLayoutPanel
{
    public LibraryFlowPanel()
    {
        BackColor = AppTheme.Background;
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }
}
