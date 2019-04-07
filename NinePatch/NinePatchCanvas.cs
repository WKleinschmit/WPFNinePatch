using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

            foreach (ColumnDefinition columnDefinition in ColumnDefinitions)
                bgGrid.ColumnDefinitions.Add(columnDefinition);

            foreach (RowDefinition rowDefinition in RowDefinitions)
                bgGrid.RowDefinitions.Add(rowDefinition);
        }

        private readonly List<ColumnDefinition> ColumnDefinitions = new List<ColumnDefinition>();
        private readonly List<RowDefinition> RowDefinitions = new List<RowDefinition>();

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
                {
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
                }
            }

            if (!ninePatchData.HasPatches)
                ReadFromImage(bmp);
        }

        private void ReadFromImage(Bitmap bmp)
        {

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
