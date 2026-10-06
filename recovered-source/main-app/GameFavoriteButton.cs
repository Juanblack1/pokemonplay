using System;
using System.Drawing;
using System.Windows.Forms;

internal sealed class GameFavoriteButton : ThemeButton
{
    public GameFavoriteButton(bool favorite) : base(favorite ? "★" : "☆", ButtonKind.Secondary)
    {
        AutoSize = false;
        Padding = new Padding(4);
    }

    protected override void DrawButtonText(Graphics graphics, Rectangle bounds)
    {
        // Draw the icon independently of glyph availability in the pixel font.
        float radius = Math.Min(Width, Height) * 0.28f;
        var points = new PointF[10];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float length = i % 2 == 0 ? radius : radius * 0.45f;
            points[i] = new PointF(Width / 2f + (float)Math.Cos(angle) * length,
                Height / 2f + (float)Math.Sin(angle) * length);
        }
        Color color = Enabled ? (Text == "★" ? AppTheme.Gold : AppTheme.TextSecondary) : AppTheme.TextMuted;
        if (Text == "★")
        {
            using var brush = new SolidBrush(color);
            graphics.FillPolygon(brush, points);
        }
        using var pen = new Pen(color, 1.5f);
        graphics.DrawPolygon(pen, points);
    }
}
