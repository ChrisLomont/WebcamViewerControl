using System;
using System.Diagnostics;
using System.Windows;

namespace Lomont.WPF
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }
        void ImageReceived(object sender, ImageReceivedArgs args)
        {
            try
            {
                var bmp = args.WriteableBitmap;

                var (w, h) = (bmp.PixelWidth, bmp.PixelHeight);
                var str = bmp.BackBufferStride;
                var d = str / w; // byte depth
                var pix = new byte[w * h * d]; // bgr or bgra (hopefully)

                // get raw pixels
                bmp.CopyPixels(pix, w * d, 0);

                // kill a color channel
                for (var i = 0; i < pix.Length; i += d)
                    pix[i + colorRemovalIndex] = 0;

                // put raw pixels
                bmp.WritePixels(new Int32Rect(0, 0, w, h), pix, str, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        int colorRemovalIndex = 0;
        void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            colorRemovalIndex = (colorRemovalIndex + 1) % 3;
            viewer.ImageReceivedEvent += ImageReceived;
        }

        void ToggleButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            viewer.ImageReceivedEvent -= ImageReceived;
        }
        void ButtonBase_AddViewer(object sender, RoutedEventArgs e)
        {
            var cnt = backGrid.Children.Count;
            var columns = (int)Math.Ceiling(Math.Sqrt(cnt));
            backGrid.Columns = columns;
            backGrid.Children.Add(new WebcamViewerControl());
        }
    }
}