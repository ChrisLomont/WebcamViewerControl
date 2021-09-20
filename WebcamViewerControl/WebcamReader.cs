using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DirectShowLib;
using OpenCvSharp;
using OpenCvSharp.Extensions;


namespace Lomont.WPF
{
    /// <summary>
    /// A simple web cam reader that returns images.
    /// </summary>
    public class WebcamReader : IDisposable
    {
        /// <summary>
        /// Start the webcam 
        /// </summary>
        /// <param name="cameraIndex">Camera index from the list</param>
        /// <param name="imageAction">An action to call on each image</param>
        /// <param name="framesPerSecond">How many frames per second to poll images</param>
        public void Start(int cameraIndex, Action<Bitmap> imageAction, int framesPerSecond = 30)
        {
            // disallow starting another while this is running.
            if (webcamTask is {IsCompleted: false})
                return;

            fps = framesPerSecond;
            cts = new CancellationTokenSource();

            webcamTask = Task.Run(async () =>
            {
                using var videoCapture = new VideoCapture();

                if (!videoCapture.Open(cameraIndex))
                    throw new ApplicationException($"Cannot connect to camera {cameraIndex}");

                using var frame = new Mat();
                while (!cts.IsCancellationRequested)
                {
                    videoCapture.Read(frame);
                    if (!frame.Empty())
                    {
                        using var bmp = frame.ToBitmap();
                        imageAction(bmp);
                    }
                    await Task.Delay(1000 / fps);
                }
            }, cts.Token);
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        public async Task Stop()
        {
            if (cts.IsCancellationRequested)
                return;

            if (!webcamTask.IsCompleted)
            {
                cts.Cancel();

                // Wait for it, to avoid conflicts from outside timing
                await webcamTask;
            }
        }
        
        /// <summary>
        /// Set number of frames per second to poll camera
        /// </summary>
        /// <param name="value"></param>
        public void SetFramesPerSecond(int value)
        {
            // some limits :)
            if (value is > 0 and <= 1000)
                fps = value;
        }

        // see https://stackoverflow.com/questions/6960520/when-to-dispose-cancellationtokensource
        public void Dispose() => cts?.Cancel();

        /// <summary>
        /// Get a list of camera names
        /// </summary>
        /// <returns></returns>
        public static List<string> GetCameraList() => DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).Select(v => v.Name).ToList();

        #region Implementation 
        CancellationTokenSource cts;
        Task webcamTask;
        int fps = 30;
        #endregion
    }
}
