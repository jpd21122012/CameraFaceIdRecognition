using cameraFaceIdSample.Classes;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.WindowsAzure.MobileServices;
using System.Collections.Generic;
using Windows.UI.Xaml.Media;
using Windows.Devices.Geolocation;
using Windows.UI.Core;

namespace cameraFaceIdSample
{
    public sealed partial class MainPage : Page
    {
        private Geolocator _geolocator = null;
        IMobileServiceTable<UsersUPT> userTableObj = App.MobileService.GetTable<UsersUPT>();
        public MainPage()
        {
            this.InitializeComponent();
            OnStart();
            //btnStop.Visibility = Visibility.Collapsed;
        }

        private async void Query(string idBuscar)
        {
            try
            {
                List<UsersUPT> lista = new List<UsersUPT>();
                UsersUPT u = new UsersUPT();
                lista = await userTableObj.Where(userTableObj => userTableObj.PID == idBuscar).ToListAsync();
                li_nom.ItemsSource = lista;
                li_nom.DisplayMemberPath = "nombre";
                lista = await userTableObj.Where(userTableObj => userTableObj.PID == idBuscar).ToListAsync();
                li_age.ItemsSource = lista;
                li_age.DisplayMemberPath = "edad";
                lista = await userTableObj.Where(userTableObj => userTableObj.PID == idBuscar).ToListAsync();
                li_desc.ItemsSource = lista;
                li_desc.DisplayMemberPath = "descripcion";
            }
            catch (Exception)
            {
            }
        }

        string CurrentVisualState
        {
            get
            {
                return (this.currentVisualState);
            }
            set
            {
                if (this.currentVisualState != value)
                {
                    this.currentVisualState = value;
                    this.ChangeStateAsync();
                }
            }
        }
        async Task ChangeStateAsync()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,() =>
              {
                  VisualStateManager.GoToState(this, this.currentVisualState, false);
              });
        }
        async void OnStart()
        {
            this.CurrentVisualState = "Playing";
            this.requestStopCancellationToken = new CancellationTokenSource();
            this.cameraPreviewManager = new CameraPreviewManager(this.captureElement);
            var videoProperties =
              await this.cameraPreviewManager.StartPreviewToCaptureElementAsync(
                vcd => vcd.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
              this.faceDetectionProcessor = new FaceDetectionFrameProcessor(
              this.cameraPreviewManager.MediaCapture,
              this.cameraPreviewManager.VideoProperties);
              this.drawingHandler = new FacialDrawingHandler(
              this.drawCanvas, videoProperties, Colors.White);
            this.faceDetectionProcessor.FrameProcessed += (s, e) =>
            {
                this.drawingHandler.SetLatestFrameReceived(e.Results);
                this.CurrentVisualState =e.Results.Count > 0 ? "PlayingWithFace" : "Playing";
                this.CopyBitmapForOxfordIfRequestPending(e.Frame.SoftwareBitmap);
            };

            try
            {
                await this.faceDetectionProcessor.RunFrameProcessingLoopAsync(
                  this.requestStopCancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
            }
            await this.cameraPreviewManager.StopPreviewAsync();
            this.requestStopCancellationToken.Dispose();
            this.CurrentVisualState = "Stopped";
        }
        void CopyBitmapForOxfordIfRequestPending(SoftwareBitmap bitmap)
        {
            if ((this.copiedVideoFrameComplete != null) &&
             (!this.copiedVideoFrameComplete.Task.IsCompleted))
            {
                var convertedRgba16Bitmap = SoftwareBitmap.Convert(bitmap,BitmapPixelFormat.Rgba16);
                this.copiedVideoFrameComplete.SetResult(convertedRgba16Bitmap);
            }
        }
        void OnStop(object sender, RoutedEventArgs e)
        {
            this.requestStopCancellationToken.Cancel();
        }

        async void OnSubmitToOxfordAsync(object sender, RoutedEventArgs e)
        {
            this.progressIndicator.Visibility = Visibility.Visible;
            this.copiedVideoFrameComplete = new TaskCompletionSource<SoftwareBitmap>();
            var bgra16CopiedFrame = await this.copiedVideoFrameComplete.Task;
            this.copiedVideoFrameComplete = null;
            InMemoryRandomAccessStream destStream = new InMemoryRandomAccessStream();
            // Now going to JPEG because Project Oxford can accept those.
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId,destStream);
            encoder.SetSoftwareBitmap(bgra16CopiedFrame);
            await encoder.FlushAsync();
            FaceServiceClient faceService = new FaceServiceClient(OxfordApiKey);
            FaceServiceClient faceService1 = new FaceServiceClient(OxfordApiKey);
            var requiredFaceAttributes = new FaceAttributeType[] {
            FaceAttributeType.Age, FaceAttributeType.Gender};
            Face[] faces = await faceService.DetectAsync(destStream.AsStream(), returnFaceLandmarks: true, returnFaceAttributes: requiredFaceAttributes);

            if (faces != null)
            {
                Debug.WriteLine("ID de rostro: " + faces[0].FaceId);
                Guid idGuid = Guid.Parse(faces[0].FaceId.ToString());
                SimilarPersistedFace[] facescomp = await faceService1.FindSimilarAsync(idGuid, "21122012", 1);
                double conf = Double.Parse(facescomp[0].Confidence.ToString());
                string pid = facescomp[0].PersistedFaceId.ToString();
                Debug.WriteLine("PID: " + facescomp[0].PersistedFaceId);
                Debug.WriteLine("conf: " + facescomp[0].Confidence);
                if (conf >= .67)
                {
                    Debug.WriteLine("Usuario encontrado");
                    // busqueda en la base de datos
                    Debug.WriteLine("Entrando a metodo bd.visualizar info de sujeto \n");
                    stackpanelAlert.Opacity = 0.5;
                    stackpanelAlert.Background = new SolidColorBrush(Colors.Red);
                    txtAlert.Text = "!Alerta¡ Sujeto buscado";
                    Query(facescomp[0].PersistedFaceId.ToString());
                    imgOff.Visibility = Visibility.Visible;
                    imgOn.Visibility = Visibility.Collapsed;
                    li_nom.Visibility = Visibility.Visible;
                    li_age.Visibility = Visibility.Visible;
                    li_desc.Visibility = Visibility.Visible;
                    tbnom.Visibility=Visibility.Visible;
                    tbage.Visibility = Visibility.Visible;
                    tbdesc.Visibility = Visibility.Visible;
                    StartTracking();
                    tbLatitude.Visibility = Visibility.Visible;
                    tbLatitude.Visibility = Visibility.Visible;
                }
                else
                {
                    stackpanelAlert.Opacity = 0.5;
                    stackpanelAlert.Background = new SolidColorBrush(Colors.Green);
                    txtAlert.Text = "Sujeto No Buscado";
                    imgOn.Visibility = Visibility.Visible;
                    imgOff.Visibility = Visibility.Collapsed;
                    li_nom.Visibility = Visibility.Collapsed;
                    li_age.Visibility = Visibility.Collapsed;
                    li_desc.Visibility = Visibility.Collapsed;
                    tbnom.Visibility = Visibility.Collapsed;
                    tbage.Visibility = Visibility.Collapsed;
                    tbdesc.Visibility = Visibility.Collapsed;
                    tbLatitude.Text = "";
                    tbLongitude.Text = "";
                    tbLatitude.Visibility = Visibility.Collapsed;
                    tbLatitude.Visibility = Visibility.Collapsed;
                }
            }
            this.progressIndicator.Visibility = Visibility.Collapsed;
        }
        string currentVisualState;
        FaceDetectionFrameProcessor faceDetectionProcessor;
        CancellationTokenSource requestStopCancellationToken;
        CameraPreviewManager cameraPreviewManager;
        FacialDrawingHandler drawingHandler;
        static readonly string OxfordApiKey = "12476023b4c349939778c49e5db321d6";
        volatile TaskCompletionSource<SoftwareBitmap> copiedVideoFrameComplete;
        private async void StartTracking()
        {
            // Request permission to access location
            var accessStatus = await Geolocator.RequestAccessAsync();

            switch (accessStatus)
            {
                case GeolocationAccessStatus.Allowed:
                    _geolocator = new Geolocator { ReportInterval = 2000 };
                    // Subscribe to PositionChanged event to get updated tracking positions
                    _geolocator.PositionChanged += OnPositionChanged;
                    break;
            }
        }

        async private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdateLocationData(e.Position);
            });
        }
        private void UpdateLocationData(Geoposition position)
        {
            if (position != null)
            {
                tbLatitude.Text = "Latitude: "+position.Coordinate.Point.Position.Latitude.ToString();
                tbLongitude.Text = "Longitude: "+position.Coordinate.Point.Position.Longitude.ToString();
            }
        }
    }
}