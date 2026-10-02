using System.Drawing;
internal static class AppTheme
{
 public static readonly Color Background=Color.FromArgb(10,16,38),Sidebar=Color.FromArgb(13,20,46),TopBar=Sidebar;
 public static readonly Color Surface=Color.FromArgb(20,29,61),SurfaceRaised=Color.FromArgb(29,42,83),SurfaceHover=Color.FromArgb(38,56,105),SurfaceSelected=Color.FromArgb(25,57,92);
 public static readonly Color Border=Color.FromArgb(68,100,195),BorderSoft=Color.FromArgb(45,65,123),Focus=Color.FromArgb(105,217,255);
 public static readonly Color Blue=Color.FromArgb(47,96,205),BlueSoft=Focus,Gold=Color.FromArgb(255,210,105),Red=Color.FromArgb(247,106,125),Green=Color.FromArgb(99,214,167);
 public static readonly Color Text=Color.FromArgb(241,246,255),TextSecondary=Color.FromArgb(199,216,250),TextMuted=Color.FromArgb(157,179,220),TextSubtle=Color.FromArgb(120,144,193);
 public static readonly Font Display=PixelFonts.At(26),PageTitle=PixelFonts.At(24),Section=PixelFonts.At(15);
 public static readonly Font Body=new Font("Segoe UI",10f),BodyBold=PixelFonts.At(11),Caption=new Font("Segoe UI",9f),CaptionBold=PixelFonts.At(10);
 public const int ControlHeight=40,Radius=4;
}
