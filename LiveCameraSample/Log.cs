//using LibraryLog;
using ClassLibraryLogCreator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoFrameAnalyzer;

namespace LiveCameraSample
{
    class Log
    {
        string filePath;
        public Log()
        {
            filePath = @"C:\temp\CogSvcLog.json";            
        }
        public void SaveData(LiveCameraResult AnalysisFunction)
        {

            LogHelper.Log(LogTarget.Binary, AnalysisFunction, filePath);
        }

        public void FaceLogFormat<T>(T target)
        {
            Type t = typeof(T);

            DateTime timeStamp = DateTime.Now;

        }
        //public async Task SaveDataAsync(Func<VideoFrame, Task<LiveCameraResult>> analysisFunction)
        //{
        //    await LogHelper.Log(LogTarget.Binary, analysisFunction, filePath);
        //}
    }
}
