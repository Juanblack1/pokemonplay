internal readonly record struct GuestFrame(int Mask,uint Counter,Rectangle Viewport);
internal static class GuestDecoder
{
    internal static bool Advances(uint before,uint after){uint delta=unchecked(after-before);return delta>0&&delta<0x80000000;}
    internal static uint Distance(uint a,uint b)=>Math.Min(unchecked(a-b),unchecked(b-a));
    static bool Near(Color a,Color b)=>Math.Abs(a.R-b.R)<=16&&Math.Abs(a.G-b.G)<=16&&Math.Abs(a.B-b.B)<=16;
    internal static GuestFrame Decode(Bitmap image,bool desktop)
    {
        using var pixels=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(var drawing=Graphics.FromImage(pixels))drawing.DrawImageUnscaled(image,0,0);
        var locked=pixels.LockBits(new Rectangle(0,0,pixels.Width,pixels.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        byte[] rgb=new byte[Math.Abs(locked.Stride)*pixels.Height];
        try{System.Runtime.InteropServices.Marshal.Copy(locked.Scan0,rgb,0,rgb.Length);}finally{pixels.UnlockBits(locked);}
        Color Pixel(int x,int y){int offset=y*Math.Abs(locked.Stride)+x*4;return Color.FromArgb(rgb[offset+2],rgb[offset+1],rgb[offset]);}
        Rectangle viewport=new(0,0,image.Width,image.Height);
        if(desktop){
            int left=image.Width,top=image.Height,right=-1,bottom=-1;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(Near(Pixel(x,y),Color.Yellow)){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}
            if(right<left||bottom<top)throw new InvalidDataException("guest_decode: calibration border absent");
            viewport=Rectangle.FromLTRB(left,top,right+1,bottom+1);
        }else if(image.Width!=240||image.Height!=160)throw new InvalidDataException("guest_decode: internal image is not 240x160");
        if(viewport.Width<240||viewport.Height<160||Math.Abs((double)viewport.Width/viewport.Height-1.5)>0.03)throw new InvalidDataException("guest_decode: invalid viewport geometry");
        Color Sample(int x,int y)=>Pixel(Math.Clamp(viewport.Left+(int)((x+0.5)*viewport.Width/240),0,image.Width-1),Math.Clamp(viewport.Top+(int)((y+0.5)*viewport.Height/160),0,image.Height-1));
        // Match S1's complete canonical interiors, not a center-only sample.
        // Scaling maps each canonical pixel center to the composed viewport.
        int Region(Rectangle rect,Func<Color,int> classify,int inset=2){
            int? state=null;
            for(int y=rect.Top+inset;y<rect.Bottom-inset;y++)for(int x=rect.Left+inset;x<rect.Right-inset;x++){
                int value=classify(Sample(x,y));
                if(value<0||(state.HasValue&&state.Value!=value))throw new InvalidDataException("guest_decode: invalid/mixed region pixels");
                state=value;
            }
            return state??throw new InvalidDataException("guest_decode: empty region interior");
        }
        void Fixed(Rectangle rect,Color expected,int inset=2)=>Region(rect,color=>Near(color,expected)?1:-1,inset);
        foreach(var border in new[]{new Rectangle(0,0,240,4),new Rectangle(0,156,240,4),new Rectangle(0,4,4,152),new Rectangle(236,4,4,152)})Fixed(border,Color.Yellow,0);
        Color[] magic={Color.Yellow,Color.White,Color.Blue,Color.Red};for(int i=0;i<4;i++)Fixed(new Rectangle(92+14*i,100,10,8),magic[i]);
        int Button(Color color)=>color.R>=240&&color.G<=16&&color.B<=16?1:color.B>=240&&color.R<=16&&color.G<=16?0:-1;
        int Counter(Color color)=>Math.Min(color.R,Math.Min(color.G,color.B))>=240?1:Math.Max(color.R,Math.Max(color.G,color.B))<=16?0:-1;
        int mask=0;uint counter=0;
        for(int i=0;i<10;i++)mask|=Region(new Rectangle(12+44*(i%5),12+54*(i/5),32,32),Button)<<i;
        for(int i=0;i<32;i++)counter|=(uint)Region(new Rectangle(8+14*(i%16),112+14*(i/16),10,8),Counter)<<i;
        return new GuestFrame(mask,counter,viewport);
    }
}
