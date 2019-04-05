using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Resources;
using System.Windows.Shapes;
using Brushes = System.Windows.Media.Brushes;
using Image = System.Drawing.Image;
using Path = System.IO.Path;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Rectangle = System.Drawing.Rectangle;

namespace NinePatch
{
    public class NinePatchCanvas : ContentControl
    {
        static NinePatchCanvas()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NinePatchCanvas), new FrameworkPropertyMetadata(typeof(NinePatchCanvas)));
        }

        public static readonly DependencyProperty PngUriProperty = DependencyProperty.Register(
            nameof(PngUri),
            typeof(Uri),
            typeof(NinePatchCanvas),
            new PropertyMetadata(null, PngUriChanged));

        private static void PngUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NinePatchCanvas npCanvas)
            {
                if (e.OldValue != null)
                    npCanvas.CleanUp();

                if (!(e.NewValue is Uri uri))
                    return;

                string baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                    ?? throw new InvalidOperationException();

                string filePath = Path.Combine(baseDir, uri.OriginalString);
                if (File.Exists(filePath))
                {
                    npCanvas.LoadFromFile(filePath);
                    return;
                }

                StreamResourceInfo sri = Application.GetResourceStream(uri)
                    ?? throw new IOException();
                npCanvas.LoadFromStream(sri.Stream);
            }
        }

        public Uri PngUri
        {
            get => (Uri)GetValue(PngUriProperty);
            set => SetValue(PngUriProperty, value);
        }

        private static readonly DependencyPropertyKey ContentMarginPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(ContentMargin),
            typeof(Thickness),
            typeof(NinePatchCanvas),
            new PropertyMetadata(new Thickness(0)));

        public static readonly DependencyProperty ContentMarginProperty = ContentMarginPropertyKey.DependencyProperty;

        public Thickness ContentMargin
        {
            get => (Thickness)GetValue(ContentMarginProperty);
            private set => SetValue(ContentMarginPropertyKey, value);
        }

        private NinePatchData ninePatchData;
        private Grid bgGrid;
        public override void OnApplyTemplate()
        {
            bgGrid = Template.FindName("bgGrid", this) as Grid
                ?? throw new InvalidOperationException();

            if (ninePatchData != null)
            {
                ContentMargin = new Thickness(
                    ninePatchData.PaddingLeft,
                    ninePatchData.PaddingTop,
                    ninePatchData.PaddingRight,
                    ninePatchData.PaddingBottom);
            }

            MakeColumsAndRows();
        }

        private void MakeColumsAndRows()
        {
            bool stretch = false;
            uint last = 0;
            foreach (uint xDiv in ninePatchData.XDivs)
            {
                if (xDiv != 0)
                {
                    bgGrid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = new GridLength(xDiv - last, stretch ? GridUnitType.Star : GridUnitType.Pixel)
                    });
                    last = xDiv;
                }

                stretch = !stretch;
            }
            if (last < ninePatchData.Rc.Width)
                bgGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(ninePatchData.Rc.Width - last, stretch ? GridUnitType.Star : GridUnitType.Pixel)
                });

            stretch = false;
            last = 0;
            foreach (uint yDiv in ninePatchData.YDivs)
            {
                if (yDiv != 0)
                {
                    bgGrid.RowDefinitions.Add(new RowDefinition
                    {
                        Height = new GridLength(yDiv - last, stretch ? GridUnitType.Star : GridUnitType.Pixel)
                    });
                    last = yDiv;
                }

                stretch = !stretch;
            }
            if (last < ninePatchData.Rc.Height)
                bgGrid.RowDefinitions.Add(new RowDefinition
                {
                    Height = new GridLength(ninePatchData.Rc.Height - last, stretch ? GridUnitType.Star : GridUnitType.Pixel)
                });

            for (int r = 0; r < bgGrid.RowDefinitions.Count; r++)
            {
                for (int c = 0; c < bgGrid.ColumnDefinitions.Count; c++)
                {
                    Ellipse ell = new Ellipse
                    {
                        Fill = Brushes.Crimson,
                    };
                    ell.SetValue(Grid.ColumnProperty, c);
                    ell.SetValue(Grid.RowProperty, r);
                    bgGrid.Children.Add(ell);
                }
            }
        }

        private void LoadFromStream(Stream stream)
        {
            if (!(Image.FromStream(stream) is Bitmap bmp))
                throw new InvalidDataException();

            Rectangle rc = new Rectangle(0, 0, bmp.Width, bmp.Height);

            stream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (reader.ReadUInt64() != 0x0a1a0a0d474e5089ul)
                    throw new InvalidDataException();

                string ctype = null;
                while (ctype != "IEND")
                {
                    uint length = reader.ReadBigEndianUInt32();
                    ctype = Encoding.ASCII.GetString(reader.ReadBytes(4));

                    switch (ctype)
                    {
                        case "npOl":
                            if (ninePatchData == null)
                                ninePatchData = new NinePatchData();
                            ninePatchData.Read_npOl(reader);
                            break;
                        case "npLb":
                            if (ninePatchData == null)
                                ninePatchData = new NinePatchData();
                            ninePatchData.Read_npLb(reader);
                            break;
                        case "npTc":
                            if (ninePatchData == null)
                                ninePatchData = new NinePatchData();
                            ninePatchData.Read_npTc(reader);
                            break;
                        default:
                            reader.Skip(length);
                            break;
                    }

                    reader.ReadBigEndianUInt32(); // crc
                }
            }

            if (ninePatchData == null)
            {
                ninePatchData = new NinePatchData { Rc = rc };

                BitmapData bmpData = bmp.LockBits(rc, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                List<uint> XDivs = new List<uint>();
                List<uint> YDivs = new List<uint>();

                // Top Edge
                uint current = 0x00000000;
                for (uint n = 1; n < rc.Width - 2; ++n)
                {
                    uint pixel = bmpData.GetPixel(n, 0);
                    switch (pixel)
                    {
                        case 0x00000000u when current == 0xFF000000u:
                            XDivs.Add(n - 1);
                            current = 0x00000000u;
                            break;
                        case 0xFF000000u when current == 0x00000000u:
                            XDivs.Add(n - 1);
                            current = 0xFF000000u;
                            break;
                    }
                }
                if (current == 0xFF000000u)
                    XDivs.Add((uint)rc.Width - 2u);
                ninePatchData.NumXDivs = (byte)XDivs.Count;
                ninePatchData.XDivs = XDivs.ToArray();

                // Left Edge
                current = 0x00000000;
                for (uint n = 1; n < rc.Height - 2; ++n)
                {
                    uint pixel = bmpData.GetPixel(0, n);
                    switch (pixel)
                    {
                        case 0x00000000u when current == 0xFF000000u:
                            YDivs.Add(n - 1);
                            current = 0x00000000u;
                            break;
                        case 0xFF000000u when current == 0x00000000u:
                            YDivs.Add(n - 1);
                            current = 0xFF000000u;
                            break;
                    }
                }
                if (current == 0xFF000000u)
                    YDivs.Add((uint)rc.Height - 2u);
                ninePatchData.NumYDivs = (byte)YDivs.Count;
                ninePatchData.YDivs = YDivs.ToArray();

                // Bottom Edge
                uint x1 = 1;
                while (bmpData.GetPixel(x1, (uint)rc.Height - 1) == 0xFFFF0000)
                {
                    ++ninePatchData.LayoutBoundsLeft;
                    ++x1;
                }

                uint x2 = (uint)rc.Width - 2;
                while (bmpData.GetPixel(x2, (uint)rc.Height - 1) == 0xFFFF0000)
                {
                    ++ninePatchData.LayoutBoundsRight;
                    --x2;
                }
                current = 0x00000000;
                for (uint n = 1; n < rc.Width - 2; ++n)
                {
                    uint pixel = bmpData.GetPixel(n, (uint)rc.Height - 1);
                    switch (pixel)
                    {
                        case 0xFFFF0000u when current == 0x00000000u:
                            break;
                        case 0xFF000000u when current == 0x00000000u:
                            ninePatchData.PaddingLeft = (int)n - 1;
                            current = 0xFF000000u;
                            break;
                        case 0x00000000u when current == 0xFF000000u:
                        case 0xFFFF0000u when current == 0xFF000000u:
                            ninePatchData.PaddingRight = rc.Width - (int)n - 1;
                            current = 0x00000000u;
                            break;
                    }
                }

                // Right Edge
                uint y1 = 1;
                while (bmpData.GetPixel((uint)rc.Width - 1, y1) == 0xFFFF0000)
                {
                    ++ninePatchData.LayoutBoundsTop;
                    ++y1;
                }

                uint y2 = (uint)rc.Height - 2;
                while (bmpData.GetPixel((uint)rc.Width - 1, y2) == 0xFFFF0000)
                {
                    ++ninePatchData.LayoutBoundsBottom;
                    --y2;
                }
                current = 0x00000000;
                for (uint n = 1; n < rc.Height - 2; ++n)
                {
                    uint pixel = bmpData.GetPixel((uint)rc.Width - 1, n);
                    switch (pixel)
                    {
                        case 0xFFFF0000u when current == 0x00000000u:
                            break;
                        case 0xFF000000u when current == 0x00000000u:
                            ninePatchData.PaddingTop = (int)n - 1;
                            current = 0xFF000000u;
                            break;
                        case 0x00000000u when current == 0xFF000000u:
                        case 0xFFFF0000u when current == 0xFF000000u:
                            ninePatchData.PaddingBottom = rc.Height - (int)n - 1;
                            current = 0x00000000u;
                            break;
                    }
                }

                bmp.UnlockBits(bmpData);

                Rectangle rcDst = rc;
                rcDst.Inflate(-2, -2);
                Rectangle rcSrc = rcDst;
                rcSrc.Offset(1, 1);

                Bitmap bm = new Bitmap(bmp, rc.Width - 2, rc.Height - 2);
                using (Graphics G = Graphics.FromImage(bm))
                    G.DrawImage(bmp, rcDst, rcSrc, GraphicsUnit.Pixel);
                bmp.Dispose();
                bmp = bm;

                ninePatchData.Rc = rcDst;
            }
        }

        private void LoadFromFile(string filePath)
        {
            using (FileStream fileStream = File.OpenRead(filePath))
                LoadFromStream(fileStream);
        }

        private void CleanUp()
        {
            ninePatchData = null;
            ContentMargin = new Thickness(0);

            if (bgGrid == null)
                return;

            bgGrid.ColumnDefinitions.Clear();
            bgGrid.RowDefinitions.Clear();
            bgGrid.Children.Clear();
        }
    }
}
