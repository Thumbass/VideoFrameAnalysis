// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Vision;
//using Microsoft.ProjectOxford.Vision.Contract;
using Microsoft.ProjectOxford.Common;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using VideoFrameAnalyzer;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace LiveCameraSample
{
    // Updated September 23 2017 8:27 PM East
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged

    {
        public event PropertyChangedEventHandler PropertyChanged;
        private EmotionServiceClient _emotionClient = null;
        private FaceServiceClient _faceClient = null;
        private VisionServiceClient _visionClient = null;
        private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };
        private readonly CascadeClassifier _localFaceDetector = new CascadeClassifier();
        private bool _fuseClientRemoteResults;
        private LiveCameraResult _latestResultsToDisplay = null;
        private AppMode _mode;
        private DateTime _startTime;
        string camoip = Properties.Settings.Default.SingleCamIP.Trim();
        string dvrip = Properties.Settings.Default.DVRIP.Trim();
        int camoch = Properties.Settings.Default.Channel;
        int subch = Properties.Settings.Default.SubChannel;
        string camoun = Properties.Settings.Default.CamUsername.Trim();
        string camopw = Properties.Settings.Default.CamPassword.Trim();
        int chs = Properties.Settings.Default.Channels;
        string dvrun = Properties.Settings.Default.DVRUsername.Trim();
        string dvrpw = Properties.Settings.Default.DVRPassword.Trim();
        string web = Properties.Settings.Default.WebCam.Trim();
        string camo = null;
        Log analysisLog = new Log();
        public enum AppMode
        {
            Faces,
            Emotions,
            EmotionsWithClientFaceDetect,
            Tags,
            Description,
            Celebrities,
            Translation
        }
        private LiveCameraResult apiResult;

        public LiveCameraResult ApiResult
        {
            get { return apiResult; }
            set { apiResult = value; }
        }

        private ObservableCollection<LiveCameraResult> _totalAPIResults = new ObservableCollection<LiveCameraResult>();

        public ObservableCollection<LiveCameraResult> TotalAPIResults
        {
            get { return _totalAPIResults; }
            set
            {
                _totalAPIResults = value;
                RaisePropertyChanged(_totalAPIResults);
            }
        }

        private void RaisePropertyChanged(ObservableCollection<LiveCameraResult> propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TotalAPIResults"));
        }
    
    // Once you enter this method the code will jump to Settings (after InitializeComponent();)
    #region MainWindow
    public MainWindow()
        {
            InitializeComponent();

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += async (s, e) =>
            {
                if (_mode == AppMode.EmotionsWithClientFaceDetect)
                {
                    // Local face detection. 
                    var rects = _localFaceDetector.DetectMultiScale(e.Frame.Image);
                    // Attach faces to frame. 
                    e.Frame.UserData = rects;
                }

                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                await this.Dispatcher.BeginInvoke((Action)(() =>
                 {
                    // Display the image in the left pane.
                    LeftImage.Source = e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    if (_fuseClientRemoteResults)
                     {
                         RightImage.Source = VisualizeResult(e.Frame);
                     }
                 }));

                // See if auto-stop should be triggered. 
                if (Properties.Settings.Default.AutoStopEnabled && (DateTime.Now - _startTime) > Properties.Settings.Default.AutoStopTime)
                {
                    await _grabber.StopProcessingAsync();
                }
            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        MessageArea.Text = "API call timed out.";
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPIException;
                        var emotionEx = e.Exception as Microsoft.ProjectOxford.Common.ClientException;
                        var visionEx = e.Exception as Microsoft.ProjectOxford.Vision.ClientException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.ErrorMessage;
                        }
                        else if (emotionEx != null)
                        {
                            apiName = "Emotion";
                            message = emotionEx.Error.Message;
                        }
                        else if (visionEx != null)
                        {
                            apiName = "Computer Vision";
                            message = visionEx.Error.Message;
                        }
                        MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                    }
                    else
                    {
                        _latestResultsToDisplay = e.Analysis;

                        // Display the image and visualization in the right pane. 
                        if (!_fuseClientRemoteResults)
                        {
                            RightImage.Source = VisualizeResult(e.Frame);
                        }
                    }
                }));
            };

            // Create local face detector. 
            _localFaceDetector.Load("Data/haarcascade_frontalface_alt2.xml");
        }
        #endregion
        /// <summary> Function which submits a frame to the Face API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
        private async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var attrs = new List<FaceAttributeType> { FaceAttributeType.Age,
                FaceAttributeType.Gender, FaceAttributeType.HeadPose };
            var faces = await _faceClient.DetectAsync(jpg, returnFaceAttributes: attrs);
            // Count the API call. 
            Properties.Settings.Default.FaceAPICallCount++;
            // Output. 
            LiveCameraResult _result = new LiveCameraResult
            {
                Faces = faces,
                TimeStamp = DateTime.Now,
                SelectedCamera = camo
            };
            TotalAPIResults.Add(_result);
            ApiResult = _result;
            analysisLog.SaveData(_result);
            return _result;
        }

        /// <summary> Function which submits a frame to the Emotion API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the emotions returned by the API. </returns>
        private async Task<LiveCameraResult> EmotionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            Emotion[] emotions = null;

            // See if we have local face detections for this image.
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces == null)
            {
                // If localFaces is null, we're not performing local face detection.
                // Use Cognigitve Services to do the face detection.
                Properties.Settings.Default.EmotionAPICallCount++;
                emotions = await _emotionClient.RecognizeAsync(jpg);
            }
            else if (localFaces.Count() > 0)
            {
                // If we have local face detections, we can call the API with them. 
                // First, convert the OpenCvSharp rectangles. 
                var rects = localFaces.Select(
                    f => new Microsoft.ProjectOxford.Common.Rectangle
                    {
                        Left = f.Left,
                        Top = f.Top,
                        Width = f.Width,
                        Height = f.Height
                    });
                Properties.Settings.Default.EmotionAPICallCount++;
                emotions = await _emotionClient.RecognizeAsync(jpg, rects.ToArray());
            }
            else
            {
                // Local face detection found no faces; don't call Cognitive Services.
                emotions = new Emotion[0];
            }

            // Output. 
            LiveCameraResult _result = new LiveCameraResult
            {
                TimeStamp = DateTime.Now,
                Faces = emotions.Select(e => CreateFace(e.FaceRectangle)).ToArray(),
                // Extract emotion scores from results. 
                EmotionScores = emotions.Select(e => e.Scores).ToArray()
            };

            TotalAPIResults.Add(_result); ApiResult = _result;
            analysisLog.SaveData(_result);
            return _result;
            
        }

        /// <summary> Function which submits a frame to the Computer Vision API for tagging. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the tags returned by the API. </returns>
        private async Task<LiveCameraResult> TaggingAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var analysis = await _visionClient.GetTagsAsync(jpg);
            // Count the API call. 
            Properties.Settings.Default.VisionAPICallCount++;
            // Output.
            LiveCameraResult _result = new LiveCameraResult { Tags = analysis.Tags, TimeStamp = DateTime.Now };
            TotalAPIResults.Add(_result);
            ApiResult = _result;
            analysisLog.SaveData(_result);
            return _result;
             
        }

        /// <summary> Function which submits a frame to the Computer Vision API for celebrity
        ///     detection. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the celebrities returned by the API. </returns>
        private async Task<LiveCameraResult> CelebrityAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var result = await _visionClient.AnalyzeImageInDomainAsync(jpg, "celebrities");
            // Count the API call. 
            Properties.Settings.Default.VisionAPICallCount++;
            // Output. 
            var celebs = JsonConvert.DeserializeObject<CelebritiesResult>(result.Result.ToString()).Celebrities;
            LiveCameraResult _result = new LiveCameraResult 
            {
                TimeStamp = DateTime.Now,
                // Extract face rectangles from results. 
                Faces = celebs.Select(c => CreateFace(c.FaceRectangle)).ToArray(),
                // Extract celebrity names from results. 
                CelebrityNames = celebs.Select(c => c.Name).ToArray()
            };
            TotalAPIResults.Add(_result);
            ApiResult = _result;
            analysisLog.SaveData(_result);
            return _result;
        }

        private async Task<LiveCameraResult> DescriptionAnalysisFunction(VideoFrame frame) // Not Currently Working
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var analysis = await _visionClient.GetTagsAsync(jpg);
            // Count the API call. 
            Properties.Settings.Default.VisionAPICallCount++;
            // Output.
            //return new LiveCameraResult { Tags = analysis.Tags };

            return new LiveCameraResult { Tags = analysis.Tags};
            
        }

        private async Task<LiveCameraResult> TranslationAnalysisFunction(VideoFrame frame) // Not Currently Working
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API.
            var result = await _visionClient.RecognizeTextAsync(jpg);
            return new LiveCameraResult
            {
                
            };
        }

        private BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;

            if (result != null)
            {
                // See if we have local face detections for this image.
                var clientFaces = (OpenCvSharp.Rect[])frame.UserData;
                if (clientFaces != null && result.Faces != null)
                {
                    // If so, then the analysis results might be from an older frame. We need to match
                    // the client-side face detections (computed on this frame) with the analysis
                    // results (computed on the older frame) that we want to display. 
                    MatchAndReplaceFaceRectangles(result.Faces, clientFaces);
                }

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.EmotionScores, result.CelebrityNames);
                visImage = Visualization.DrawTags(visImage, result.Tags);
            }

            return visImage;
        }

        /// <summary> Populate CameraList in the UI, once it is loaded. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Routed event information. </param>
        private void CameraList_Loaded(object sender, RoutedEventArgs e)
        {
            int numCameras = _grabber.GetNumCameras();

            if (numCameras == 0)
            {
                MessageArea.Text = "No cameras found!";
            }

            List<string> ListData = new List<string>();
            for (int i=0; i <= numCameras; i++)
            {
                if (i > 0)
                {
                    ListData.Add($"Camera {i}");
                }
            }
            if (!string.IsNullOrWhiteSpace(camoip))
            {
                ListData.Add("ipcam");
            }
            if (!string.IsNullOrWhiteSpace(dvrip))
            {
                for (int x = 1; x <= chs; x++)
                {
                    if (chs > 0)
                    {
                        ListData.Add($"DVR Network Camera {x}");
                    }
                }
                    
            }
            if (!string.IsNullOrWhiteSpace(web))
            {
                ListData.Add("web");
            }

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = ListData; //Enumerable.Range(0, numCameras).Select(i => string.Format("Camera {0}", i + 1));
            comboBox.SelectedIndex = 0;
            //selectedCamera = comboBox.SelectedItem.ToString();

        }
        //string camo = ComboBox.
        /// <summary> Populate ModeList in the UI, once it is loaded. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Routed event information. </param>
        private void ModeList_Loaded(object sender, RoutedEventArgs e)
        {
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = modes.Select(m => m.ToString());
            comboBox.SelectedIndex = 0;
        }

        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Disable "most-recent" results display. 
            _fuseClientRemoteResults = false;

            var comboBox = sender as ComboBox;
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));
            _mode = modes[comboBox.SelectedIndex];
            switch (_mode)
            {
                case AppMode.Faces:
                    _grabber.AnalysisFunction = FacesAnalysisFunction;
                    break;
                case AppMode.Emotions:
                    _grabber.AnalysisFunction = EmotionAnalysisFunction;
                    break;
                case AppMode.EmotionsWithClientFaceDetect:
                    // Same as Emotions, except we will display the most recent faces combined with
                    // the most recent API results. 
                    _grabber.AnalysisFunction = EmotionAnalysisFunction;
                    _fuseClientRemoteResults = true;
                    break;
                case AppMode.Tags:
                    _grabber.AnalysisFunction = TaggingAnalysisFunction;
                    break;
                case AppMode.Celebrities:
                    _grabber.AnalysisFunction = CelebrityAnalysisFunction;
                    break;
                case AppMode.Description:
                    _grabber.AnalysisFunction = DescriptionAnalysisFunction;
                    break;
                case AppMode.Translation:
                    _grabber.AnalysisFunction = TranslationAnalysisFunction;
                    break;
                default:
                    _grabber.AnalysisFunction = null;
                    break;
            }
        }
        #region StartButtonClick / StartProcessingCamera
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraList.HasItems)
            {
                MessageArea.Text = "No cameras found; cannot start processing";
                return;
            }
            // Clean leading/trailing spaces in API keys. 
            Properties.Settings.Default.FaceAPIKey = Properties.Settings.Default.FaceAPIKey.Trim();
            Properties.Settings.Default.EmotionAPIKey = Properties.Settings.Default.EmotionAPIKey.Trim();
            Properties.Settings.Default.VisionAPIKey = Properties.Settings.Default.VisionAPIKey.Trim();
            Properties.Settings.Default.TranslationTextAPIKey = Properties.Settings.Default.TranslationTextAPIKey.Trim();

            // Define Camera Information
            Properties.Settings.Default.SingleCamIP = Properties.Settings.Default.SingleCamIP.Trim();
            Properties.Settings.Default.DVRIP = Properties.Settings.Default.DVRIP.Trim();
            Properties.Settings.Default.Channel = Properties.Settings.Default.Channel;
            Properties.Settings.Default.SubChannel = Properties.Settings.Default.SubChannel;
            Properties.Settings.Default.Channels = Properties.Settings.Default.Channels;
            Properties.Settings.Default.CamUsername = Properties.Settings.Default.CamUsername.Trim();
            Properties.Settings.Default.CamPassword = Properties.Settings.Default.CamPassword.Trim();
            Properties.Settings.Default.DVRUsername = Properties.Settings.Default.DVRUsername.Trim();
            Properties.Settings.Default.DVRPassword = Properties.Settings.Default.DVRPassword.Trim();
            Properties.Settings.Default.WebCam = Properties.Settings.Default.WebCam.Trim();


            // Create API clients. 
            _faceClient = new FaceServiceClient(Properties.Settings.Default.FaceAPIKey, Properties.Settings.Default.FaceAPIHost);
            _emotionClient = new EmotionServiceClient(Properties.Settings.Default.EmotionAPIKey, Properties.Settings.Default.EmotionAPIHost);
            _visionClient = new VisionServiceClient(Properties.Settings.Default.VisionAPIKey, Properties.Settings.Default.VisionAPIHost);

            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(Properties.Settings.Default.AnalysisInterval);

            // Reset message. 
            MessageArea.Text = "";

            // Record start time, for auto-stop
            _startTime = DateTime.Now;
            string rtsp = SetRTSPStream();

            await _grabber.StartProcessingCameraAsync(rtsp, CameraList.SelectedIndex);

        }

        

        #endregion
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await _grabber.StopProcessingAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = 1 - SettingsPanel.Visibility;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Hidden;
            Properties.Settings.Default.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private Face CreateFace(FaceRectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private Face CreateFace(Microsoft.ProjectOxford.Vision.Contract.FaceRectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private Face CreateFace(Microsoft.ProjectOxford.Common.Rectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private void MatchAndReplaceFaceRectangles(Face[] faces, OpenCvSharp.Rect[] clientRects)
        {
            // Use a simple heuristic for matching the client-side faces to the faces in the
            // results. Just sort both lists left-to-right, and assume a 1:1 correspondence. 

            // Sort the faces left-to-right. 
            var sortedResultFaces = faces
                .OrderBy(f => f.FaceRectangle.Left + 0.5 * f.FaceRectangle.Width)
                .ToArray();

            // Sort the clientRects left-to-right.
            var sortedClientRects = clientRects
                .OrderBy(r => r.Left + 0.5 * r.Width)
                .ToArray();

            // Assume that the sorted lists now corrrespond directly. We can simply update the
            // FaceRectangles in sortedResultFaces, because they refer to the same underlying
            // objects as the input "faces" array. 
            for (int i = 0; i < Math.Min(faces.Length, clientRects.Length); i++)
            {
                // convert from OpenCvSharp rectangles
                OpenCvSharp.Rect r = sortedClientRects[i];
                sortedResultFaces[i].FaceRectangle = new FaceRectangle { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height };
            }
        }

        public string SetRTSPStream()
        {
            camo = CameraList.SelectedValue.ToString();
            string ip = camo.Equals("ipcam") ? camoip : camo.StartsWith("DVR") ? dvrip : string.Empty;
            int ch = 1;
            ch = GetChannel(camo, ch);
            int sch = camo.Equals("ipcam") ? subch : 0;
            string un = camo.Equals("ipcam") ? camoun : camo.StartsWith("DVR") ? dvrun : string.Empty;
            string pw = camo.Equals("ipcam") ? camopw : camo.StartsWith("DVR") ? dvrpw : string.Empty;
            string rtsp = string.Empty;

            if (camo.Equals("ipcam") || camo.StartsWith("DVR"))
            {
                rtsp = string.Format("rtsp://{0}:{1}@{2}/cam/realmonitor?channel={3}&subtype={4}", un, pw, ip, ch, sch);
            }
            else if (camo.Equals("WEB"))
            {
                rtsp = web;
            }

            return rtsp;
        }

        private int GetChannel(string camo, int ch)
        {
            if (camo.Equals("ipcam") || camo.StartsWith("DVR"))
            {
                if (camo.Equals("ipcam"))
                {
                    ch = camoch;
                }
                else
                {
                    string[] split = camo.Split(new Char[] { ' ' });
                    string chsel = split[3];

                    if (Int32.TryParse(chsel, out int i))
                    {
                        ch = i;
                    }
                }
            }

            return ch;
        }

        private void ItemsControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            
        }
    }
}
