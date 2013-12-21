#region license
// /*
//     This file is part of Vocaluxe.
// 
//     Vocaluxe is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     Vocaluxe is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with Vocaluxe. If not, see <http://www.gnu.org/licenses/>.
//  */
#endregion

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using VocaluxeLib;
using VocaluxeLib.Draw;

namespace Vocaluxe.Base.Fonts
{
    class CGlyph
    {
        private CTexture _Texture;
        private readonly SizeF _BoundingBox;
        private readonly RectangleF _DrawBounding;
        public readonly float MaxHeight;

        public float Width
        {
            get { return _BoundingBox.Width * _GetFactor(); }
        }

        public float Height
        {
            get { return _BoundingBox.Height * _GetFactor(); }
        }

        public CGlyph(char chr, float maxHeight)
        {
            MaxHeight = maxHeight;
            float oldHeight = CFonts.Height;
            float outline = CFonts.Outline;
            float outlineSize = outline * maxHeight;
            string chrString = chr.ToString();

            CFonts.Height = maxHeight;
            Font fo = CFonts.GetFont();
            SizeF fullSize;
            Size bmpSize;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                fullSize = g.MeasureString(chrString, fo);
                if (chr != ' ')
                {
                    /* Fetch the character's black box (the exact bitmap size required), the black box relative to the write position, and the write position increments */
                    GlyphMetrics metrics = GlyphMetrics.GetGlyphMetrics(g, fo, chr);

                    /* Create a fake "bounding box" based on the write position increments. */
                    _BoundingBox = new SizeF((float)metrics.CellIncX, (float)metrics.CellIncY);

                    //fullSize = new SizeF((float)metrics.BlackBoxX + Math.Abs(metrics.GlyphOrigin.X), (float)metrics.BlackBoxY + Math.Abs(metrics.GlyphOrigin.Y));

                    //Gets exact height and width for drawing more than 1 char. But width is to small to draw char on bitmap as e.g. italic chars will get cropped
                    //_BoundingBox = g.MeasureString(chrString, fo, -1, new StringFormat(StringFormat.GenericTypographic));
                    // ReSharper disable CompareOfFloatsByEqualityOperator
                    if (_BoundingBox.Height == 0)
                        // ReSharper restore CompareOfFloatsByEqualityOperator
                        _BoundingBox.Height = fullSize.Height;
                    _BoundingBox.Width += outlineSize / 2;
                    _BoundingBox.Height += outlineSize;
                    fullSize.Width += outlineSize;
                    bmpSize = new Size((int)fullSize.Width, (int)Math.Round(_BoundingBox.Height));
                }
                else
                {
                    _BoundingBox = fullSize;
                    _BoundingBox.Height += outlineSize;
                    bmpSize = new Size(1, 1);
                }
            }
            using (Bitmap bmp = new Bitmap(bmpSize.Width, bmpSize.Height, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);

                if (chr == ' ')
                {
                    _Texture = CDraw.AddTexture(bmp);
                    _DrawBounding = new RectangleF(0, 0, 0, 0);
                }
                else
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    PointF point = new PointF(outlineSize / 2, outlineSize / 4);

                    using (GraphicsPath path = new GraphicsPath())
                    {
                        //Have to use size in em not pixels!
                        float emSize = fo.Size * fo.FontFamily.GetCellAscent(fo.Style) / fo.FontFamily.GetEmHeight(fo.Style);
                        path.AddString(chrString, fo.FontFamily, (int)fo.Style, emSize, point, new StringFormat());

                        using (Pen pen = new Pen(CFonts.OutlineColor.AsColor(), outlineSize))
                        {
                            pen.LineJoin = LineJoin.Round;
                            g.DrawPath(pen, path);
                            g.FillPath(Brushes.White, path);
                        }
                    }
                    _DrawBounding = _GetRealBounds(bmp);
                    using (Bitmap bmpCropped = bmp.Clone(_DrawBounding, PixelFormat.Format32bppArgb))
                    {
                        float dx = (fullSize.Width - _BoundingBox.Width - 1) / 2;
                        _DrawBounding.X -= dx;
                        _Texture = CDraw.AddTexture(bmpCropped);
                        /*_DrawBounding.X *= _Texture.Width / _DrawBounding.Width;
                        _DrawBounding.Y *= _Texture.Width / _DrawBounding.Width;
                        _DrawBounding.Width = _Texture.Width;
                        _DrawBounding.Height = _Texture.Height;*/
#pragma warning disable 162
                        // ReSharper disable HeuristicUnreachableCode
                        if (false && Char.IsLetterOrDigit(chr))
                        {
                            if (outline > 0)
                                bmpCropped.Save("font_" + chr + "o" + CFonts.Style + "2.png", ImageFormat.Png);
                            else
                                bmpCropped.Save("font_" + chr + CFonts.Style + "2.png", ImageFormat.Png);
                        }
                        // ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162
                    }
                }
            }
            CFonts.Height = oldHeight;
        }

        public void UnloadTexture()
        {
            CDraw.RemoveTexture(ref _Texture);
        }

        private float _GetFactor()
        {
            return CFonts.Height / _BoundingBox.Height;
        }

        public void GetTextureAndRect(float x, float y, float z, out CTexture texture, out SRectF rect)
        {
            texture = _Texture;
            float factor = _GetFactor();
            x += _DrawBounding.X * factor;
            y += _DrawBounding.Y * factor;
            float h = _DrawBounding.Height * factor;
            float w = _DrawBounding.Width * factor;
            rect = new SRectF(x, y, w, h, z);
        }

        private static Rectangle _GetRealBounds(Bitmap bmp)
        {
            int minX = 0, maxX = bmp.Width - 1, minY = 0;
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int values = bmpData.Width * bmp.Height;
            Int32[] rgbValues = new Int32[values];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, values);
            int index = 0;
            bool found = false;
            //find from top
            for (int y = 0; y < bmp.Height && !found; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (rgbValues[index] != 0)
                    {
                        minX = x;
                        maxX = x;
                        minY = y;
                        found = true;
                        break;
                    }
                    index++;
                }
            }
            found = false;
            //find left
            for (int x = 0; x < minX && !found; x++)
            {
                index = x + minY * bmp.Width;
                for (int y = minY; y < bmp.Height; y++)
                {
                    if (rgbValues[index] != 0)
                    {
                        found = true;
                        minX = x;
                        break;
                    }
                    index += bmp.Width;
                }
            }
            found = false;
            //find right
            for (int x = bmp.Width - 1; x > maxX && !found; x--)
            {
                index = x + minY * bmp.Width;
                for (int y = minY; y < bmp.Height; y++)
                {
                    if (rgbValues[index] != 0)
                    {
                        found = true;
                        maxX = x;
                        break;
                    }
                    index += bmp.Width;
                }
            }

            //Add some additional space. Textures need some extra pixel for resizing.
            const int d = 4;
            minX = minX - d;
            if (minX < 0)
                minX = 0;
            minY = minY - d;
            if (minY < 0)
                minY = 0;
            maxX = maxX + d;
            if (maxX > bmp.Width)
                maxX = bmp.Width;

            return new Rectangle(minX, minY, maxX - minX, bmp.Height - minY);
        }

        private class GlyphMetrics
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct FIXED
            {
                public short fract;
                public short value;

                public static FIXED One { get { FIXED f = new FIXED(); f.value = 1; f.fract = 0; return f; } }
                public static FIXED Zero { get { FIXED f = new FIXED(); f.value = 0; f.fract = 0; return f; } }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MAT2
            {
                [MarshalAs(UnmanagedType.Struct)]
                public FIXED eM11;
                [MarshalAs(UnmanagedType.Struct)]
                public FIXED eM12;
                [MarshalAs(UnmanagedType.Struct)]
                public FIXED eM21;
                [MarshalAs(UnmanagedType.Struct)]
                public FIXED eM22;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct GLYPHMETRICS
            {
                public int gmBlackBoxX;
                public int gmBlackBoxY;
                [MarshalAs(UnmanagedType.Struct)]
                public POINT gmptGlyphOrigin;
                public short gmCellIncX;
                public short gmCellIncY;
            }

            const int GGO_METRICS = 0;
            const int GGO_BITMAP = 1;
            const int GGO_NATIVE = 2;
            const int GGO_BEZIER = 3;
            const int GGO_GRAY2_BITMAP = 4;
            const int GGO_GRAY4_BITMAP = 5;
            const int GGO_GRAY8_BITMAP = 6;
            const int GGO_GLYPH_INDEX = 0x0080;
            const int GGO_UNHINTED = 0x0100;

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern bool DeleteObject(IntPtr hdc);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern uint GetGlyphOutline(IntPtr hdc, uint uChar, uint uFormat, out GLYPHMETRICS lpgm, uint cbBuffer, IntPtr lpvBuffer, ref MAT2 lpmat2);

            public static GlyphMetrics GetGlyphMetrics(
                Graphics graphics,
                Font font,
                char character)
            {
                return new GlyphMetrics(graphics, font, character);
            }

            public int BlackBoxX { get { return _Metrics.gmBlackBoxX; } }
            public int BlackBoxY { get { return _Metrics.gmBlackBoxX; } }
            public Point GlyphOrigin { get { return new Point(_Metrics.gmptGlyphOrigin.x, _Metrics.gmptGlyphOrigin.y); } }
            public short CellIncX { get { return _Metrics.gmCellIncX; } }
            public short CellIncY { get { return _Metrics.gmCellIncY; } }

            private GLYPHMETRICS _Metrics;

            private GlyphMetrics(Graphics graphics, Font font, char character)
            {
                IntPtr hDC = IntPtr.Zero;
                IntPtr hFont = IntPtr.Zero;
                try
                {
                    hDC = graphics.GetHdc();
                    hFont = font.ToHfont();
                    IntPtr hFontDefault = SelectObject(hDC, hFont);
                    MAT2 mat2 = new MAT2();
                    mat2.eM11 = FIXED.One;
                    mat2.eM12 = FIXED.Zero;
                    mat2.eM21 = FIXED.Zero;
                    mat2.eM22 = FIXED.One;
                    GetGlyphOutline(hDC, (uint)character, GGO_METRICS, out _Metrics, 0, IntPtr.Zero, ref mat2);
                    SelectObject(hDC, hFontDefault);
                }
                finally
                {
                    if (hFont != IntPtr.Zero) DeleteObject(hFont);
                    if (hDC != IntPtr.Zero) graphics.ReleaseHdc(hDC);
                }
            }
        }

        private class FontMetrics
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct TEXTMETRIC
            {
                public int tmHeight;
                public int tmAscent;
                public int tmDescent;
                public int tmInternalLeading;
                public int tmExternalLeading;
                public int tmAveCharWidth;
                public int tmMaxCharWidth;
                public int tmWeight;
                public int tmOverhang;
                public int tmDigitizedAspectX;
                public int tmDigitizedAspectY;
                public char tmFirstChar;
                public char tmLastChar;
                public char tmDefaultChar;
                public char tmBreakChar;
                public byte tmItalic;
                public byte tmUnderlined;
                public byte tmStruckOut;
                public byte tmPitchAndFamily;
                public byte tmCharSet;
            }
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern bool DeleteObject(IntPtr hdc);
            private TEXTMETRIC GenerateTextMetrics(
                Graphics graphics,
                Font font)
            {
                IntPtr hDC = IntPtr.Zero;
                TEXTMETRIC textMetric;
                IntPtr hFont = IntPtr.Zero;
                try
                {
                    hDC = graphics.GetHdc();
                    hFont = font.ToHfont();
                    IntPtr hFontDefault = SelectObject(hDC, hFont);
                    bool result = GetTextMetrics(hDC, out textMetric);
                    SelectObject(hDC, hFontDefault);
                }
                finally
                {
                    if (hFont != IntPtr.Zero) DeleteObject(hFont);
                    if (hDC != IntPtr.Zero) graphics.ReleaseHdc(hDC);
                }
                return textMetric;
            }

            private TEXTMETRIC metrics;
            public int Height { get { return this.metrics.tmHeight; } }
            public int Ascent { get { return this.metrics.tmAscent; } }
            public int Descent { get { return this.metrics.tmDescent; } }
            public int InternalLeading { get { return this.metrics.tmInternalLeading; } }
            public int ExternalLeading { get { return this.metrics.tmExternalLeading; } }
            public int AverageCharacterWidth { get { return this.metrics.tmAveCharWidth; } }
            public int MaximumCharacterWidth { get { return this.metrics.tmMaxCharWidth; } }
            public int Weight { get { return this.metrics.tmWeight; } }
            public int Overhang { get { return this.metrics.tmOverhang; } }
            public int DigitizedAspectX { get { return this.metrics.tmDigitizedAspectX; } }
            public int DigitizedAspectY { get { return this.metrics.tmDigitizedAspectY; } }

            private FontMetrics(Graphics graphics, Font font)
            {
                this.metrics = this.GenerateTextMetrics(graphics, font);
            }

            public static FontMetrics GetFontMetrics(
                Graphics graphics,
                Font font)
            {
                return new FontMetrics(graphics, font);
            }
        }

    }
}