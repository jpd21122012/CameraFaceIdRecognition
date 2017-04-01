using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Media;
using System.Threading.Tasks;

namespace cameraFaceIdSample.Classes
{
    class PreviewFrameProcessedEventArgs<T> : EventArgs
    {
        public PreviewFrameProcessedEventArgs()
        {

        }
        public PreviewFrameProcessedEventArgs(
          T processingResults,
          VideoFrame frame)
        {
            this.Results = processingResults;
            this.Frame = frame;
        }
        public T Results { get; set; }
        public VideoFrame Frame { get; set; }
    }
}
