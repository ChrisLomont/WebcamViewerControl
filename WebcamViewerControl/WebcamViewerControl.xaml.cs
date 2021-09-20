// Chris Lomont WPF webcam viewer using OpenCVSharp
// 2021
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lomont.WPF
{
    /* TODO
    * 1. Make into control
    * 2. make buttons IsEnabled act from the command interface
    3. test multiple at once
    * 4. add hooks for processing images in between frames
    * 5. Remove MVVM light needs from control
    * 6. Package into minimal files pair (Xaml and cs, maybe a reader)
    7. Add audio, and recording, ala BasicAudio and a FFMPeg wrapper https://cuteprogramming.wordpress.com/2020/12/12/look-at-me-webcam-recording-with-net-5-winforms/

    Add properties/events
        * 1. fps
        2. start, stop, refresh
        3. Camera index, 
        * 4. clear last frame or not
        * 5. show controls
        * 6. grab frames before they draw
        7. camera list
    */

    public class ImageReceivedArgs : EventArgs
    {
        public WriteableBitmap WriteableBitmap { get; set; }
    }

    /// <summary>
    /// Interaction logic for WebcamViewer.xaml
    /// </summary>
    public partial class WebcamViewerControl
    {
        // good article on user control databinding
        // https://blog.scottlogic.com/2012/02/06/a-simple-pattern-for-creating-re-useable-usercontrols-in-wpf-silverlight.html
        // main trick - set the datacontext of the control at the element below the highest element, so the highest element 
        // can be bound to outside stuff
        public WebcamViewerControl()
        {
            InitializeComponent();

            // set DataContext one below UserControl so parents can bind here
            TopControl.DataContext = this;

            // to listen to DP changes
            var dpd = DependencyPropertyDescriptor.FromProperty(FramesPerSecondProperty, typeof(WebcamViewerControl));
            dpd?.AddValueChanged(this, (_, _) => ChangeFps());
        }
        /// <summary>
        /// Listen to this to get images before viewed
        /// </summary>
        public event EventHandler<ImageReceivedArgs> ImageReceivedEvent;
        
        public delegate void ImageReceivedEventHandler(object sender, ImageReceivedArgs args);

        /// <summary>
        /// Load the camera list
        /// </summary>
        public void LoadList()
        {
            Cameras.Clear();
            foreach (var camera in WebcamReader.GetCameraList())
                Cameras.Add(camera);
            CameraBox.SelectedIndex = 0;
        }

        public void ChangeFps() => webcam?.SetFramesPerSecond(FramesPerSecond);
        public void Dispose() => webcam?.Dispose();
        public ObservableCollection<string> Cameras { get; } = new();

        public void StartWebcam(int cameraIndex)
        {
            SetButtonEnabledStates(false, false);

            ;
            if (webcam == null || lastCameraIndex != cameraIndex)
            {
                webcam?.Dispose();
                webcam = new WebcamReader();
                lastCameraIndex = cameraIndex;
            }

            try
            {
                webcam.Start(cameraIndex, ShowBitmap, FramesPerSecond);
                SetButtonEnabledStates(false, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetButtonEnabledStates(true, false);
            }
        }
        public async void StopWebcam()
        {
            try
            {

                await webcam.Stop();
                SetButtonEnabledStates(true, false);
                if (!LeaveLastFrame)
                    Image.Source = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Dependency properties

        /// <summary>
        /// Sets frames per second
        /// </summary>
        public int FramesPerSecond
        {
            get => (int)GetValue(FramesPerSecondProperty);
            set => SetValue(FramesPerSecondProperty, value);
        }

        public static readonly DependencyProperty FramesPerSecondProperty =
            DependencyProperty.Register("FramesPerSecond", typeof(int),
                typeof(WebcamViewerControl), new PropertyMetadata(30));


        /// <summary>
        /// Shows or hides the controls
        /// </summary>
        public bool ShowControls
        {
            get => (bool)GetValue(ShowControlsProperty);
            set => SetValue(ShowControlsProperty, value);
        }

        public static readonly DependencyProperty ShowControlsProperty =
            DependencyProperty.Register("ShowControls", typeof(bool),
                typeof(WebcamViewerControl), new PropertyMetadata(true));

        /// <summary>
        /// Shows last frame on stop
        /// </summary>
        public bool LeaveLastFrame
        {
            get => (bool)GetValue(LeaveLastFrameProperty);
            set => SetValue(LeaveLastFrameProperty, value);
        }

        public static readonly DependencyProperty LeaveLastFrameProperty =
            DependencyProperty.Register("LeaveLastFrame", typeof(bool),
                typeof(WebcamViewerControl), new PropertyMetadata(true));

        #endregion

        #region Window handlers
        void Window_Closing(object sender, CancelEventArgs e) => Dispose();
        void WebcamViewer_OnLoaded(object sender, RoutedEventArgs e)
        {
            // user controls don't have a closing message, so listen to parents
            var window = Window.GetWindow(this);
            if (window != null) window.Closing += Window_Closing;
            LoadList();
        }
        void StartButton_OnClick(object sender, RoutedEventArgs e) => StartWebcam(CameraBox.SelectedIndex);
        void StopButton_OnClick(object sender, RoutedEventArgs e) => StopWebcam();
        void RefreshButton_OnClick(object sender, RoutedEventArgs e) => LoadList();

        #endregion

        #region Implementation

        // Wrap event invocations inside a protected virtual method
        // to allow derived classes to override the event invocation behavior
        protected virtual void OnRaiseImageReceivedEvent(WriteableBitmap bitmap) => ImageReceivedEvent?.Invoke(this, new ImageReceivedArgs { WriteableBitmap = bitmap });

        void SetButtonEnabledStates(bool startEnabled, bool stopEnabled)
        {
            StartButton.IsEnabled = startEnabled;
            StopButton.IsEnabled = stopEnabled;
        }

        int lastCameraIndex = -1;

        WebcamReader webcam;

        BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            // todo - see how to make this section faster, less copies, etc...
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            var wBmp = new WriteableBitmap(bitmapSource);
            // let outsiders modify it
            OnRaiseImageReceivedEvent(wBmp);
            return wBmp;
        }

        void ShowBitmap(System.Drawing.Bitmap lastFrame)
        {
            // convert to WPF
            var lastFrameBitmapImage = ToBitmapSource(lastFrame);
            lastFrameBitmapImage.Freeze();
            // WPF data binding marshalls to dispatcher thread, but here we set the property directly
            Image.Dispatcher.Invoke(() => { Image.Source = lastFrameBitmapImage; });
        }

        #endregion
    }
}