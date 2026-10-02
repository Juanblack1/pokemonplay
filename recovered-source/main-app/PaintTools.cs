using System;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class PaintTools
{
	public static GraphicsPath Rounded(Rectangle rectangle, int radius)
	{
        int corner = Math.Min(4, Math.Max(1, Math.Min(rectangle.Width, rectangle.Height) / 4));
        int left=rectangle.Left, top=rectangle.Top, right=rectangle.Right, bottom=rectangle.Bottom;
        var path=new GraphicsPath();
        path.AddPolygon(new[]{new Point(left+corner,top),new Point(right-corner,top),new Point(right-corner,top+corner),new Point(right,top+corner),new Point(right,bottom-corner),new Point(right-corner,bottom-corner),new Point(right-corner,bottom),new Point(left+corner,bottom),new Point(left+corner,bottom-corner),new Point(left,bottom-corner),new Point(left,top+corner),new Point(left+corner,top+corner)});
        return path;
    }

	public static void FillRounded(Graphics graphics, Brush brush, Rectangle rectangle, int radius)
	{
		using GraphicsPath path = Rounded(rectangle, radius);
		graphics.FillPath(brush, path);
	}

	public static void DrawRounded(Graphics graphics, Pen pen, Rectangle rectangle, int radius)
	{
		using GraphicsPath path = Rounded(rectangle, radius);
		graphics.DrawPath(pen, path);
	}

	public static Rectangle FitImage(Image image, Rectangle box)
	{
		if (image == null || image.Width <= 0 || image.Height <= 0)
		{
			return box;
		}
		double num = Math.Min((double)box.Width / (double)image.Width, (double)box.Height / (double)image.Height);
		int num2 = Math.Max(1, (int)Math.Round((double)image.Width * num));
		int num3 = Math.Max(1, (int)Math.Round((double)image.Height * num));
		return new Rectangle(box.X + (box.Width - num2) / 2, box.Y + (box.Height - num3) / 2, num2, num3);
	}

	public static void DrawPokeball(Graphics graphics, Rectangle box, Color red, Color navy)
	{
		graphics.SmoothingMode = SmoothingMode.None;
		using (SolidBrush brush = new SolidBrush(red))
		{
			graphics.FillEllipse(brush, box);
		}
		Rectangle rect = new Rectangle(box.X, box.Y + box.Height / 2, box.Width, box.Height / 2);
		using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(244, 247, 253)))
		{
			graphics.FillPie(brush2, rect, 0f, 180f);
		}
		using (Pen pen = new Pen(Color.FromArgb(205, 222, 246), Math.Max(1, box.Width / 24)))
		{
			graphics.DrawEllipse(pen, box);
		}
		using (Pen pen2 = new Pen(navy, Math.Max(2, box.Width / 12)))
		{
			graphics.DrawLine(pen2, box.Left, box.Top + box.Height / 2, box.Right, box.Top + box.Height / 2);
		}
		int num = Math.Max(6, box.Width / 4);
		Rectangle rect2 = new Rectangle(box.X + (box.Width - num) / 2, box.Y + (box.Height - num) / 2, num, num);
		using (SolidBrush brush3 = new SolidBrush(navy))
		{
			graphics.FillEllipse(brush3, rect2);
		}
		Rectangle rect3 = new Rectangle(rect2.X + num / 4, rect2.Y + num / 4, num / 2, num / 2);
		using SolidBrush whiteBrush = new SolidBrush(Color.White);
		graphics.FillEllipse(whiteBrush, rect3);
	}

	public static void DrawIcon(Graphics graphics, NavIcon icon, Rectangle box, Color color)
	{
		graphics.SmoothingMode = SmoothingMode.None;
		using Pen pen = new Pen(color, 1.8f);
		pen.StartCap = LineCap.Round;
		pen.EndCap = LineCap.Round;
		pen.LineJoin = LineJoin.Round;
		int x = box.X;
		int y = box.Y;
		int width = box.Width;
		int height = box.Height;
		switch (icon)
		{
		case NavIcon.Library:
		{
			Point[] points = new Point[3]
			{
				new Point(x + 2, y + 9),
				new Point(x + width / 2, y + 2),
				new Point(x + width - 2, y + 9)
			};
			graphics.DrawLines(pen, points);
			graphics.DrawLine(pen, x + 5, y + 8, x + 5, y + height - 2);
			graphics.DrawLine(pen, x + width - 5, y + 8, x + width - 5, y + height - 2);
			graphics.DrawLine(pen, x + 5, y + height - 2, x + width - 5, y + height - 2);
			graphics.DrawLine(pen, x + width / 2, y + height - 2, x + width / 2, y + height - 8);
			break;
		}
		case NavIcon.Saves:
			graphics.DrawLine(pen, x + 2, y + 5, x + 9, y + 5);
			graphics.DrawLine(pen, x + 9, y + 5, x + 12, y + 8);
			graphics.DrawRectangle(pen, x + 2, y + 5, width - 4, height - 7);
			graphics.DrawLine(pen, x + 2, y + 11, x + width - 2, y + 11);
			graphics.DrawLine(pen, x + 5, y + 2, x + 5, y + 7);
			break;
		case NavIcon.Bank:
			graphics.DrawRectangle(pen, x + 1, y + 4, width - 2, height - 5);
			graphics.DrawLine(pen, x + 1, y + 9, x + width - 1, y + 9);
			graphics.DrawLine(pen, x + width / 3, y + 10, x + width / 3, y + height - 1);
			graphics.DrawLine(pen, x + 2 * width / 3, y + 10, x + 2 * width / 3, y + height - 1);
			break;
		case NavIcon.Settings:
		{
			graphics.DrawEllipse(pen, x + 5, y + 5, width - 10, height - 10);
			graphics.DrawEllipse(pen, x + width / 2 - 2, y + height / 2 - 2, 4, 4);
			for (int i = 0; i < 8; i++)
			{
				double num = (double)i * Math.PI / 4.0;
				int x2 = x + width / 2 + (int)(Math.Cos(num) * (double)(width / 2 - 3));
				int y2 = y + height / 2 + (int)(Math.Sin(num) * (double)(height / 2 - 3));
				int x3 = x + width / 2 + (int)(Math.Cos(num) * (double)(width / 2 - 6));
				int y3 = y + height / 2 + (int)(Math.Sin(num) * (double)(height / 2 - 6));
				graphics.DrawLine(pen, x3, y3, x2, y2);
			}
			break;
		}
		default:
			graphics.DrawRectangle(pen, x + 1, y + 5, width - 2, height - 6);
			graphics.DrawLine(pen, x + 2, y + 5, x + 8, y + 5);
			graphics.DrawLine(pen, x + 8, y + 5, x + 10, y + 2);
			graphics.DrawLine(pen, x + 10, y + 2, x + width - 2, y + 2);
			graphics.DrawLine(pen, x + width - 2, y + 2, x + width - 2, y + 5);
			break;
		}
	}

	public static void DrawFileIcon(Graphics graphics, Rectangle box, Color color)
	{
		using Pen pen = new Pen(color, 1.4f);
		pen.LineJoin = LineJoin.Round;
		Point[] points = new Point[6]
		{
			new Point(box.X + 3, box.Y + 1),
			new Point(box.Right - 5, box.Y + 1),
			new Point(box.Right - 1, box.Y + 5),
			new Point(box.Right - 1, box.Bottom - 1),
			new Point(box.X + 3, box.Bottom - 1),
			new Point(box.X + 3, box.Y + 1)
		};
		graphics.DrawLines(pen, points);
		graphics.DrawLine(pen, box.Right - 5, box.Y + 1, box.Right - 5, box.Y + 5);
		graphics.DrawLine(pen, box.Right - 5, box.Y + 5, box.Right - 1, box.Y + 5);
	}
}
