﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System.Threading;
using System.Net;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;

using Windows.UI.Xaml.Media.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace CustomVisionDemo
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // Add your Azure Custom Vision endpoint to your environment variables.
        private string Key = "your key";
        private string Endpoint = "your endpiont";
        private string ProjectID = "your project id";
        private string publishedModelName = "your iteration name";
        private double minProbability = 0.75;
        private CustomVisionPredictionClient predictionApi;
        string imageFilePath = "test_image.jpg";

        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private StorageFile recordStorageFile;
        private StorageFile audioFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private readonly string VIDEO_FILE_NAME = "video.mp4";
        private readonly string AUDIO_FILE_NAME = "audio.mp3";
        private bool isPreviewing;
        private bool isRecording;

        public MainPage()
        {
            this.InitializeComponent();
            //init parameters
            predictionApi = AuthenticatePrediction(Endpoint, Key);
            //predict pics
            var objects = DetectObjects(imageFilePath);
            Console.WriteLine("Object - Probability - Position(X,Y)");
            foreach (var obj in objects)
            {
                Console.WriteLine("{0}: {1} - ({2},{3})",
                    obj.TagName,
                    obj.Probability,
                    obj.BoundingBox.Left,
                    obj.BoundingBox.Top);
            }

            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            isRecording = false;
            isPreviewing = false;
        }

        private CustomVisionPredictionClient AuthenticatePrediction(string endpoint, string predictionKey)
        {
            return new CustomVisionPredictionClient(new Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.ApiKeyServiceClientCredentials(predictionKey))
            {
                Endpoint = endpoint
            };
        }

        public List<PredictionModel> DetectObjects(string imageFile)
        {
            using (var stream = File.Open(imageFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var result = predictionApi.DetectImage(new Guid(ProjectID), publishedModelName, stream);
                return result.Predictions.Where(x => x.Probability > minProbability).ToList();
            }
        }

        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
        }
        /// <summary>
        /// Helper function to enable or disable Initialization buttons
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
                audio_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
                audio_init.IsEnabled = false;
            }
        }

        /// <summary>
        /// Helper function to enable or disable video related buttons (TakePhoto, Start Video Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;

                recordVideo.IsEnabled = true;
                recordVideo.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;

                recordVideo.IsEnabled = false;
                recordVideo.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Helper function to enable or disable audio related buttons (Start Audio Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetAudioButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                recordAudio.IsEnabled = true;
                recordAudio.Visibility = Visibility.Visible;
            }
            else
            {
                recordAudio.IsEnabled = false;
                recordAudio.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Source = null;
                    playbackElement.Source = null;
                    isPreviewing = false;
                }
                if (isRecording)
                {
                    await mediaCapture.StopRecordAsync();
                    isRecording = false;
                    recordVideo.Content = "Start Video Record";
                    recordAudio.Content = "Start Audio Record";
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            SetInitButtonVisibility(Action.ENABLE);
        }

        /// <summary>
        /// 'Initialize Audio and Video' button action function
        /// Dispose existing MediaCapture object and set it up for audio and video
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio and Video' 
        /// - DISABLE 'Start Audio Record'
        /// - ENABLE 'Initialize Audio Only'
        /// - ENABLE 'Start Video Record'
        /// - ENABLE 'Take Photo'
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes

            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        playbackElement.Source = null;
                        isPreviewing = false;
                    }
                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        isRecording = false;
                        recordVideo.Content = "Start Video Record";
                        recordAudio.Content = "Start Audio Record";
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio and video...";
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                status.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                status.Text = "Camera preview succeeded";

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);

                // Enable Audio Only Init button, leave the video init button disabled
                audio_init.IsEnabled = true;
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);
            Cleanup();
        }


        /// <summary>
        /// 'Initialize Audio Only' button action function
        /// Dispose existing MediaCapture object and set it up for audio only
        /// Enable or disable appropriate buttons
        /// - DISABLE 'Initialize Audio Only' 
        /// - DISABLE 'Start Video Record'
        /// - DISABLE 'Take Photo'
        /// - ENABLE 'Initialize Audio and Video'
        /// - ENABLE 'Start Audio Record'        
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void initAudioOnly_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            SetAudioButtonVisibility(Action.DISABLE);

            try
            {
                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        playbackElement.Source = null;
                        isPreviewing = false;
                    }
                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        isRecording = false;
                        recordVideo.Content = "Start Video Record";
                        recordAudio.Content = "Start Audio Record";
                    }
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                status.Text = "Initializing camera to capture audio only...";
                mediaCapture = new MediaCapture();
                var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Audio;
                settings.MediaCategory = Windows.Media.Capture.MediaCategory.Other;
                settings.AudioProcessing = Windows.Media.AudioProcessing.Default;
                await mediaCapture.InitializeAsync(settings);

                // Set callbacks for failure and recording limit exceeded
                status.Text = "Device successfully initialized for audio recording!" + "\nPress \'Start Audio Record\' to record";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Enable buttons for audio
                SetAudioButtonVisibility(Action.ENABLE);

                // Enable Audio and video Only Init button
                video_init.IsEnabled = true;
            }
            catch (Exception ex)
            {
                status.Text = "Unable to initialize camera for audio mode: " + ex.Message;
            }
        }

        /// <summary>
        /// 'Take Photo' button click action function
        /// Capture image to a file in the default account photos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void takePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                recordVideo.IsEnabled = false;
                captureImage.Source = null;

                //photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                //    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                photoFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);

                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                takePhoto.IsEnabled = true;
                status.Text = "Take Photo succeeded: " + photoFile.Path;

                //custom vision with photo file
                customVisionProcess(photoFile);

                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                captureImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
                recordVideo.IsEnabled = true;
            }
        }

        /// <summary>
        /// 'Start Video Record' button click action function
        /// Button name is changed to 'Stop Video Record' once recording is started
        /// Records video to a file in the default account videos folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void recordVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                takePhoto.IsEnabled = false;
                recordVideo.IsEnabled = false;
                playbackElement.Source = null;

                if (recordVideo.Content.ToString() == "Start Video Record")
                {
                    takePhoto.IsEnabled = false;
                    status.Text = "Initialize video recording";
                    String fileName;
                    fileName = VIDEO_FILE_NAME;

                    recordStorageFile = await Windows.Storage.KnownFolders.VideosLibrary.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                    status.Text = "Video storage file preparation successful";

                    MediaEncodingProfile recordProfile = null;
                    recordProfile = MediaEncodingProfile.CreateMp4(Windows.Media.MediaProperties.VideoEncodingQuality.Auto);

                    await mediaCapture.StartRecordToStorageFileAsync(recordProfile, recordStorageFile);
                    recordVideo.IsEnabled = true;
                    recordVideo.Content = "Stop Video Record";
                    isRecording = true;
                    status.Text = "Video recording in progress... press \'Stop Video Record\' to stop";
                }
                else
                {
                    takePhoto.IsEnabled = true;
                    status.Text = "Stopping video recording...";
                    await mediaCapture.StopRecordAsync();
                    isRecording = false;

                    var stream = await recordStorageFile.OpenReadAsync();
                    playbackElement.AutoPlay = true;
                    playbackElement.SetSource(stream, recordStorageFile.FileType);
                    playbackElement.Play();
                    status.Text = "Playing recorded video" + recordStorageFile.Path;
                    recordVideo.Content = "Start Video Record";
                }
            }
            catch (Exception ex)
            {
                if (ex is System.UnauthorizedAccessException)
                {
                    status.Text = "Unable to play recorded video; video recorded successfully to: " + recordStorageFile.Path;
                    recordVideo.Content = "Start Video Record";
                }
                else
                {
                    status.Text = ex.Message;
                    Cleanup();
                }
            }
            finally
            {
                recordVideo.IsEnabled = true;
            }
        }

        /// <summary>
        /// 'Start Audio Record' button click action function
        /// Button name is changes to 'Stop Audio Record' once recording is started
        /// Records audio to a file in the default account video folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void recordAudio_Click(object sender, RoutedEventArgs e)
        {
            recordAudio.IsEnabled = false;
            playbackElement3.Source = null;

            try
            {
                if (recordAudio.Content.ToString() == "Start Audio Record")
                {
                    audioFile = await Windows.Storage.KnownFolders.VideosLibrary.CreateFileAsync(AUDIO_FILE_NAME, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

                    status.Text = "Audio storage file preparation successful";

                    MediaEncodingProfile recordProfile = null;
                    recordProfile = MediaEncodingProfile.CreateM4a(Windows.Media.MediaProperties.AudioEncodingQuality.Auto);

                    await mediaCapture.StartRecordToStorageFileAsync(recordProfile, audioFile);

                    isRecording = true;
                    recordAudio.IsEnabled = true;
                    recordAudio.Content = "Stop Audio Record";
                    status.Text = "Audio recording in progress... press \'Stop Audio Record\' to stop";
                }
                else
                {
                    status.Text = "Stopping audio recording...";

                    await mediaCapture.StopRecordAsync();

                    isRecording = false;
                    recordAudio.IsEnabled = true;
                    recordAudio.Content = "Start Audio Record";

                    var stream = await audioFile.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    status.Text = "Playback recorded audio: " + audioFile.Path;
                    playbackElement3.AutoPlay = true;
                    playbackElement3.SetSource(stream, audioFile.FileType);
                    playbackElement3.Play();
                }
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                recordAudio.IsEnabled = true;
            }
        }

        /// <summary>
        /// Callback function for any failures in MediaCapture operations
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        /// <param name="currentFailure"></param>
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    status.Text = "MediaCaptureFailed: " + currentFailure.Message;

                    if (isRecording)
                    {
                        await mediaCapture.StopRecordAsync();
                        status.Text += "\n Recording Stopped";
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    SetVideoButtonVisibility(Action.DISABLE);
                    SetAudioButtonVisibility(Action.DISABLE);
                    status.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }

        /// <summary>
        /// Callback function if Recording Limit Exceeded
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        public async void mediaCapture_RecordLimitExceeded(Windows.Media.Capture.MediaCapture currentCaptureObject)
        {
            try
            {
                if (isRecording)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            status.Text = "Stopping Record on exceeding max record duration";
                            await mediaCapture.StopRecordAsync();
                            isRecording = false;
                            recordAudio.Content = "Start Audio Record";
                            recordVideo.Content = "Start Video Record";
                            if (mediaCapture.MediaCaptureSettings.StreamingCaptureMode == StreamingCaptureMode.Audio)
                            {
                                status.Text = "Stopped record on exceeding max record duration: " + audioFile.Path;
                            }
                            else
                            {
                                status.Text = "Stopped record on exceeding max record duration: " + recordStorageFile.Path;
                            }
                        }
                        catch (Exception e)
                        {
                            status.Text = e.Message;
                        }
                    });
                }
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }

        private async void customVisionProcess(StorageFile imgFile)
        {
            if (imgFile != null)
            {

                using (var stream = await imgFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var result = predictionApi.DetectImage(new Guid(ProjectID), publishedModelName, stream.AsStream());
                    status.Text = result.Predictions[0].TagName + ": " + result.Predictions[0].Probability + Environment.NewLine + "Bounding Box: " + result.Predictions[0].BoundingBox.Left + "," +
                        result.Predictions[0].BoundingBox.Top + "," + result.Predictions[0].BoundingBox.Width + "," + result.Predictions[0].BoundingBox.Height;
                    //result.Predictions.Where(x => x.Probability > minProbability).ToList();
                }
            }
        }
        private async void customvision_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".jpg");
            openPicker.FileTypeFilter.Add(".jpeg");
            openPicker.FileTypeFilter.Add(".png");

            StorageFile storageFile = await openPicker.PickSingleFileAsync();

            if (storageFile != null)
            {

                using (var stream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var result = predictionApi.DetectImage(new Guid(ProjectID), publishedModelName, stream.AsStream());
                    status.Text = result.Predictions[0].TagName + result.Predictions[0].Probability;
                    //result.Predictions.Where(x => x.Probability > minProbability).ToList();
                }
            }
        }
    }
}
