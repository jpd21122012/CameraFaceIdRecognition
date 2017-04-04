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
using Windows.Services.Maps;
using Windows.UI.Core;

namespace cameraFaceIdSample
{
    public sealed partial class MainPage : Page
    {
        IMobileServiceTable<UsersUPT> userTableObj = App.MobileService.GetTable<UsersUPT>();
        bool verifyInternet = true;
        string currentVisualState;
        FaceDetectionFrameProcessor faceDetectionProcessor;
        CancellationTokenSource requestStopCancellationToken;
        CameraPreviewManager cameraPreviewManager;
        FacialDrawingHandler drawingHandler;
        private Geolocator _geolocator = null;
        static readonly string OxfordApiKey = "12476023b4c349939778c49e5db321d6";
        volatile TaskCompletionSource<SoftwareBitmap> copiedVideoFrameComplete;

        public MainPage()
        {
            InitializeComponent();
            stackpanelNames.Visibility = Visibility.Collapsed;
            OnStart();
            imgInternet.Visibility = (Network.IsConnected ? Visibility.Collapsed : Visibility.Visible);
            verifyInternet = (Network.IsConnected ? true : false);
            Debug.WriteLine("Actuamente tu internet esta: " + verifyInternet);
            Network.InternetConnectionChanged += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    verifyInternet = e.IsConnected;
                    Debug.WriteLine("Internet cambió a: " + e.IsConnected);
                    imgInternet.Visibility =
                        (e.IsConnected ? Visibility.Collapsed : Visibility.Visible);
                    stackpanelInternet.Visibility = (e.IsConnected ? Visibility.Collapsed : Visibility.Visible);
                });
            };
        }

        private async void Query(string idBuscar)
        {
            List<UsersUPT> lista = new List<UsersUPT>();
            UsersUPT u = new UsersUPT();

            try
            {
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
            catch (Exception ex)
            {
                Debug.Write("Error: " + ex);
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
                }
            }
        }
        async Task ChangeStateAsync()
        {
            await Dispatcher.RunAsync(
              CoreDispatcherPriority.Normal,
              () =>
              {
                  VisualStateManager.GoToState(this, this.currentVisualState, false);
              }
            );
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
            this.drawCanvas, videoProperties, Colors.Blue);
            this.faceDetectionProcessor.FrameProcessed += (s, e) =>
            {
                this.drawingHandler.SetLatestFrameReceived(e.Results);
                this.CurrentVisualState =
                  e.Results.Count > 0 ? "PlayingWithFace" : "Playing";
                this.CopyBitmapForOxfordIfRequestPending(e.Frame.SoftwareBitmap);
            };
            try
            {
                await this.faceDetectionProcessor.RunFrameProcessingLoopAsync(
                  this.requestStopCancellationToken.Token);
            }
            catch (OperationCanceledException ex)
            {
                Debug.Write("Error: " + ex);
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
                var convertedRgba16Bitmap = SoftwareBitmap.Convert(bitmap,
                  BitmapPixelFormat.Rgba16);
                this.copiedVideoFrameComplete.SetResult(convertedRgba16Bitmap);
            }
        }
        void OnStop(object sender, RoutedEventArgs e)
        {
            this.requestStopCancellationToken.Cancel();
        }

        async void OnSubmitToOxfordAsync(object sender, RoutedEventArgs e)
        {
            stackpanelAlert.Visibility = Visibility.Collapsed;
            stackpanel.Visibility = Visibility.Collapsed;
            imgCautiono.Visibility = Visibility.Collapsed;
            imgGlasses.Visibility = Visibility.Collapsed;
            imgOk.Visibility = Visibility.Collapsed;
            imgNoFaces.Visibility = Visibility.Collapsed;
            stackpanelNames.Visibility = Visibility.Collapsed;
            stackpanelInternet.Visibility = Visibility.Collapsed;
            if (verifyInternet == true)
            {
                try
                {
                    this.copiedVideoFrameComplete = new TaskCompletionSource<SoftwareBitmap>();
                    var bgra16CopiedFrame = await this.copiedVideoFrameComplete.Task;
                    this.copiedVideoFrameComplete = null;
                    InMemoryRandomAccessStream destStream = new InMemoryRandomAccessStream();
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId,destStream);
                    encoder.SetSoftwareBitmap(bgra16CopiedFrame);
                    await encoder.FlushAsync();
                    FaceServiceClient faceService = new FaceServiceClient(OxfordApiKey);
                    FaceServiceClient faceService1 = new FaceServiceClient(OxfordApiKey);
                    var requiredFaceAttributes = new FaceAttributeType[]
                    {FaceAttributeType.Age, FaceAttributeType.Gender, FaceAttributeType.Glasses};
                    Face[] faces = await faceService.DetectAsync(destStream.AsStream(), returnFaceLandmarks: true, returnFaceAttributes: requiredFaceAttributes);
                    if (faces != null)
                    {
                        var edad = faces[0].FaceAttributes.Age;
                        var genero = faces[0].FaceAttributes.Gender;
                        int roundedAge = (int)Math.Round(edad);
                        Debug.WriteLine("ID de rostro: " + faces[0].FaceId);
                        Guid idGuid = Guid.Parse(faces[0].FaceId.ToString());
                        SimilarPersistedFace[] facescomp = await faceService1.FindSimilarAsync(idGuid, "21122012", 1);
                        double conf = Double.Parse(facescomp[0].Confidence.ToString());
                        string pid = facescomp[0].PersistedFaceId.ToString();
                        Debug.WriteLine("PID: " + facescomp[0].PersistedFaceId);
                        Debug.WriteLine("conf: " + facescomp[0].Confidence);
                        string lentes = faces[0].FaceAttributes.Glasses.ToString();
                        Debug.WriteLine("Resultado de lentes: " + lentes);
                        if (lentes == "NoGlasses")
                        {
                            if (conf >= .67)
                            {
                                stackpanel.Visibility = Visibility.Visible;
                                stackpanelNames.Visibility = Visibility.Visible;
                                stackpanelAlert.Width = 320;
                                Debug.WriteLine("Usuario encontrado");
                                stackpanelAlert.Visibility = Visibility.Visible;
                                stackpanelAlert.Background = new SolidColorBrush(Colors.Red);
                                imgCautiono.Visibility = Visibility.Visible;
                                imgGlasses.Visibility = Visibility.Collapsed;
                                imgOk.Visibility = Visibility.Collapsed;
                                imgNoFaces.Visibility = Visibility.Collapsed;
                                Debug.WriteLine("Entrando a metodo bd.visualizar info de sujeto \n");
                                Query(facescomp[0].PersistedFaceId.ToString());
                                await StartTracking();
                            }
                            else
                            {
                                stackpanelNames.Visibility = Visibility.Collapsed;
                                stackpanelAlert.Width = 320;
                                stackpanelAlert.Visibility = Visibility.Visible;
                                stackpanelAlert.Background = new SolidColorBrush(Colors.Green);
                                imgCautiono.Visibility = Visibility.Collapsed;
                                imgOk.Visibility = Visibility.Visible;

                                imgGlasses.Visibility = Visibility.Collapsed;
                                Debug.WriteLine("Usuario no identificado");
                                stackpanel.Visibility = Visibility.Collapsed;
                                imgNoFaces.Visibility = Visibility.Collapsed;
                                Query(null);
                            }
                        }
                        else
                        {
                            stackpanelNames.Visibility = Visibility.Collapsed;
                            stackpanelAlert.Width = 500;
                            Query(null);
                            stackpanelAlert.Visibility = Visibility.Visible;
                            imgCautiono.Visibility = Visibility.Collapsed;
                            imgOk.Visibility = Visibility.Collapsed;
                            imgGlasses.Visibility = Visibility.Visible;

                            stackpanelAlert.Background = new SolidColorBrush(Colors.Blue);
                            stackpanel.Visibility = Visibility.Collapsed;
                            imgNoFaces.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                catch (Exception ex)
                {

                    stackpanelAlert.Visibility = Visibility.Visible;
                    stackpanelAlert.Background = new SolidColorBrush(Colors.Blue);
                    imgNoFaces.Visibility = Visibility.Visible;
                    stackpanelAlert.Width = 360;
                    Debug.Write("No faces detected: : " + ex);
                }
            }
            else
            {
                stackpanel.Visibility = Visibility.Collapsed;
                imgCautiono.Visibility = Visibility.Collapsed;
                imgGlasses.Visibility = Visibility.Collapsed;
                imgOk.Visibility = Visibility.Collapsed;
                imgNoFaces.Visibility = Visibility.Collapsed;
                stackpanelNames.Visibility = Visibility.Collapsed;
                stackpanelInternet.Visibility = Visibility.Visible;
                imgInternet.Visibility = Visibility.Visible;
                Debug.WriteLine("No hay internet");
            }
        }

        private async Task StartTracking()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            switch (accessStatus)
            {
                case GeolocationAccessStatus.Allowed:
                    _geolocator = new Geolocator { ReportInterval = 2000 };
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
        private async Task UpdateLocationData(Geoposition position)
        {
            if (position != null)
            {
                txtLatitude.Text = position.Coordinate.Point.Position.Latitude.ToString();
                txtLongitude.Text = position.Coordinate.Point.Position.Longitude.ToString();
                await getTown(position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
            }
        }

        private async Task getTown(double lat, double lon)
        {
            BasicGeoposition location = new BasicGeoposition();
            location.Latitude = lat;
            location.Longitude = lon;
            Geopoint pointToReverseGeocode = new Geopoint(location);
            MapLocationFinderResult result =
                  await MapLocationFinder.FindLocationsAtAsync(pointToReverseGeocode);
            if (result.Status == MapLocationFinderStatus.Success)
            {
                txtStreet.Text = result.Locations[0].Address.Street;
                txtDistrict.Text = result.Locations[0].Address.District;
                txtTown.Text = result.Locations[0].Address.Town;
                txtCountry.Text = result.Locations[0].Address.Country;
                Debug.WriteLine("Town: " + result.Locations[0].Address.Town);
                Debug.WriteLine("district: " + result.Locations[0].Address.District);
                Debug.WriteLine("Country: " + result.Locations[0].Address.Country);
                Debug.WriteLine("Street: " + result.Locations[0].Address.Street);
            }
        }
    }
}