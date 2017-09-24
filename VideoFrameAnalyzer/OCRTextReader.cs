using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;

namespace VideoFrameAnalyzer
{
    class OCRTextReader
    {
        //private EmotionServiceClient _emotionClient = null;
        //private FaceServiceClient _faceClient = null;
        //private VisionServiceClient _visionClient = null;
        //private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        //private static readonly ImageEncodingParam[] s_jpegParams = { new ImageEncodingParam(ImwriteFlags.JpegQuality, 60) };
        //private readonly CascadeClassifier _localFaceDetector = new CascadeClassifier();
        //private bool _fuseClientRemoteResults;
        //private LiveCameraResult _latestResultsToDisplay = null;
        //private AppMode _mode;
        //private DateTime _startTime;

        //private async Task<LiveCameraResult> TaggingAnalysisFunction(VideoFrame frame)
        //{
        //    // Encode image. 
        //    var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
        //    // Submit image to API. 
        //    var analysis = await _visionClient.RecognizeTextAsync(jpg);
        //    // Count the API call. 
        //    Properties.Settings.Default.VisionAPICallCount++;
        //    // Output.
        //    return new LiveCameraResult { Tags = analysis.Tags };
        //}
    }
}
