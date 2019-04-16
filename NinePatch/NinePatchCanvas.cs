
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Resources;
using Brush = System.Drawing.Brush;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;
using Path = System.IO.Path;
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

        private static readonly DependencyPropertyKey LayoutBoundsPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(LayoutBounds),
            typeof(Thickness),
            typeof(NinePatchCanvas),
            new PropertyMetadata(new Thickness(0)));

        public static readonly DependencyProperty LayoutBoundsProperty =
            LayoutBoundsPropertyKey.DependencyProperty;

        public Thickness LayoutBounds
        {
            get => (Thickness)GetValue(LayoutBoundsProperty);
            private set => SetValue(LayoutBoundsPropertyKey, value);
        }

        private Thickness SavedLayoutBounds = new Thickness(0);

        public static DependencyProperty UseLayoutBoundsProperty = DependencyProperty.Register(
            nameof(UseLayoutBounds),
            typeof(bool),
            typeof(NinePatchCanvas),
            new PropertyMetadata(false, UseLayoutBoundsChanged));

        private static void UseLayoutBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NinePatchCanvas ninePatchCanvas)
            {
                if (e.NewValue is bool newValue)
                {
                    if (newValue)
                    {
                        ninePatchCanvas.LayoutBounds = ninePatchCanvas.SavedLayoutBounds;
                        Thickness t = ninePatchCanvas.ContentMargin;
                        t.Left += ninePatchCanvas.SavedLayoutBounds.Left;
                        t.Top += ninePatchCanvas.SavedLayoutBounds.Top;
                        t.Right += ninePatchCanvas.SavedLayoutBounds.Right;
                        t.Bottom += ninePatchCanvas.SavedLayoutBounds.Bottom;
                        ninePatchCanvas.ContentMargin = t;
                    }
                    else
                    {
                        ninePatchCanvas.LayoutBounds = new Thickness(0);
                        Thickness t = ninePatchCanvas.ContentMargin;
                        t.Left -= ninePatchCanvas.SavedLayoutBounds.Left;
                        t.Top -= ninePatchCanvas.SavedLayoutBounds.Top;
                        t.Right -= ninePatchCanvas.SavedLayoutBounds.Right;
                        t.Bottom -= ninePatchCanvas.SavedLayoutBounds.Bottom;
                        ninePatchCanvas.ContentMargin = t;
                    }
                }
            }
        }

        public bool UseLayoutBounds
        {
            get => (bool)GetValue(UseLayoutBoundsProperty);
            set => SetValue(UseLayoutBoundsProperty, value);
        }

        private static readonly DependencyPropertyKey HasLayoutBoundsPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(HasLayoutBounds),
            typeof(bool),
            typeof(NinePatchCanvas),
            new PropertyMetadata(false));

        public static readonly DependencyProperty HasLayoutBoundsProperty =
            HasLayoutBoundsPropertyKey.DependencyProperty;

        public bool HasLayoutBounds
        {
            get => (bool)GetValue(HasLayoutBoundsProperty);
            private set => SetValue(HasLayoutBoundsPropertyKey, value);
        }

        private static readonly DependencyPropertyKey ContentMarginPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(ContentMargin),
            typeof(Thickness),
            typeof(NinePatchCanvas),
            new PropertyMetadata(new Thickness(0)));

        public static readonly DependencyProperty ContentMarginProperty =
            ContentMarginPropertyKey.DependencyProperty;

        public Thickness ContentMargin
        {
            get => (Thickness)GetValue(ContentMarginProperty);
            private set => SetValue(ContentMarginPropertyKey, value);
        }

        private NinePatchData ninePatchData;
        private Grid bgGrid;

        private readonly List<ColumnDefinition> ColumnDefinitions = new List<ColumnDefinition>();
        private readonly List<RowDefinition> RowDefinitions = new List<RowDefinition>();
        private readonly List<Patch> Patches = new List<Patch>();


        public override void OnApplyTemplate()
        {
            bgGrid = Template.FindName("bgGrid", this) as Grid
                ?? throw new InvalidOperationException();

            bgGrid.Background = Background;

            foreach (ColumnDefinition columnDefinition in ColumnDefinitions)
                bgGrid.ColumnDefinitions.Add(columnDefinition);

            foreach (RowDefinition rowDefinition in RowDefinitions)
                bgGrid.RowDefinitions.Add(rowDefinition);

            foreach (Patch patch in Patches)
            {
                UIElement dp = patch.Element;
                dp.SetValue(Grid.RowProperty, patch.Row);
                dp.SetValue(Grid.ColumnProperty, patch.Column);
                bgGrid.Children.Add(dp);
            }
        }

        private void LoadFromStream(Stream stream)
        {
            if (!(Image.FromStream(stream) is Bitmap bmp))
                throw new InvalidDataException();

            stream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (reader.ReadUInt64() != 0x0a1a0a0d474e5089ul)
                    throw new InvalidDataException();

                ninePatchData = new NinePatchData();

                string ctype = null;
                while (ctype != "IEND")
                {
                    uint length = reader.ReadBigEndianUInt32();
                    ctype = Encoding.ASCII.GetString(reader.ReadBytes(4));

                    switch (ctype)
                    {
                        case "npOl":
                            ninePatchData.Read_npOl(reader);
                            break;
                        case "npLb":
                            ninePatchData.Read_npLb(reader);
                            break;
                        case "npTc":
                            ninePatchData.Read_npTc(reader);
                            break;
                        default:
                            reader.Skip(length);
                            break;
                    }

                    reader.ReadBigEndianUInt32(); // crc
                }

                if (ninePatchData.HasPatches)
                    ReadNinePatchData(bmp);
                else
                    ReadFromImage(bmp);

                if (ninePatchData.HasPatches)
                    ReadNinePatchData(bmp);
                //else
                //    throw new InvalidDataException();

                if (!ninePatchData.HasLayoutBounds)
                    return;

                SavedLayoutBounds = new Thickness(
                    -ninePatchData.LayoutBoundsLeft,
                    -ninePatchData.LayoutBoundsTop,
                    -ninePatchData.LayoutBoundsRight,
                    -ninePatchData.LayoutBoundsBottom);

                HasLayoutBounds = true;

                if (UseLayoutBounds)
                    LayoutBounds = SavedLayoutBounds;
            }
        }

        private void ReadFromImage(Bitmap bmp)
        {

        }

        private void ReadNinePatchData(Image bmp)
        {
            Rectangle rc = new Rectangle(0, 0, bmp.Width, bmp.Height);

            ContentMargin = new Thickness(
                ninePatchData.PaddingLeft,
                ninePatchData.PaddingTop,
                ninePatchData.PaddingRight,
                ninePatchData.PaddingBottom);

            bool stretch = false;
            uint n = 0;
            ColumnDefinitions.Clear();
            foreach (uint xDiv in ninePatchData.XDivs)
            {
                if (xDiv == 0)
                {
                    stretch = !stretch;
                    continue;
                }

                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(xDiv - n, stretch ? GridUnitType.Star : GridUnitType.Pixel) });
                n = xDiv;
                stretch = !stretch;

                if (xDiv == ninePatchData.XDivs.Last() && xDiv != rc.Width)
                    ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rc.Width - n, stretch ? GridUnitType.Star : GridUnitType.Pixel) });
            }

            stretch = false;
            n = 0;
            RowDefinitions.Clear();
            foreach (uint yDiv in ninePatchData.YDivs)
            {
                if (yDiv == 0)
                {
                    stretch = !stretch;
                    continue;
                }

                RowDefinitions.Add(new RowDefinition { Height = new GridLength(yDiv - n, stretch ? GridUnitType.Star : GridUnitType.Pixel) });
                n = yDiv;
                stretch = !stretch;

                if (yDiv == ninePatchData.YDivs.Last() && yDiv != rc.Height)
                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(rc.Height - n, stretch ? GridUnitType.Star : GridUnitType.Pixel) });
            }

            if (ninePatchData.Colors.Length == ColumnDefinitions.Count * RowDefinitions.Count)
            {
                n = 0;
                int y = 0;
                Brush brTransparent = new SolidBrush(Color.Transparent);
                for (int row = 0; row < RowDefinitions.Count; ++row)
                {
                    GridLength rowHeight = RowDefinitions[row].Height;
                    int h = (int)rowHeight.Value;
                    int x = 0;
                    for (int col = 0; col < ColumnDefinitions.Count; ++col)
                    {
                        GridLength colWidth = ColumnDefinitions[col].Width;
                        int w = (int)colWidth.Value;
                        switch (ninePatchData.Colors[n++])
                        {
                            case NinePatchData.Transparent:
                                break;
                            case NinePatchData.NoSingleColor:
                                {
                                    Rectangle rcOrig = new Rectangle(x, y, w, h);
                                    Rectangle rcPatch = new Rectangle(0, 0, w, h);
                                    Bitmap bmpPatch = new Bitmap(w, h, bmp.PixelFormat);
                                    using (Graphics G = Graphics.FromImage(bmpPatch))
                                        G.DrawImage(bmp, rcPatch, rcOrig, GraphicsUnit.Pixel);
                                    System.Windows.Controls.Image I = new System.Windows.Controls.Image
                                    {
                                        SnapsToDevicePixels = true,
                                        Source = bmpPatch.ToBitmapSource(),
                                        Stretch = Stretch.Fill,
                                    };
                                    Patches.Add(new Patch
                                    {
                                        Row = row,
                                        Column = col,
                                        Element = I,
                                    });
                                    bmpPatch.Dispose();
                                }
                                break;
                        }

                        x += w;
                    }

                    y += h;
                }
                brTransparent.Dispose();
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
