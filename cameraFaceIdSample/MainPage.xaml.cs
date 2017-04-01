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
//using cameraFaceIdSample.Classes;
using Microsoft.WindowsAzure.MobileServices;
using System.Collections.Generic;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0xc0a

namespace cameraFaceIdSample
{
    /// <summary>
    /// Página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>

    public sealed partial class MainPage : Page
    {

        public Recognition rec = new Recognition();
        public static string x;
        public static Guid guid;

        public MainPage()
        {
            this.InitializeComponent();

        }

        IMobileServiceTable<UsersUPT> userTableObj = App.MobileService.GetTable<UsersUPT>();


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
            await Dispatcher.RunAsync(
              Windows.UI.Core.CoreDispatcherPriority.Normal,
              () =>
              {
                  VisualStateManager.GoToState(this, this.currentVisualState, false);
              }
            );
        }
        async void OnStart(object sender, RoutedEventArgs args)
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
                // This event will fire on the task thread that the face
                // detection processor is running on. 
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
                // We move to RGBA16 because that is a format that we will then be able
                // to use a BitmapEncoder on to move it to PNG and we *cannot* do async
                // work here because it'll break our processing loop.
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
            // Because I constantly change visual states in the processing loop, I'm just doing
            // this with some code rather than with visual state changes because those would
            // get quickly overwritten while this work is ongoing.
            this.progressIndicator.Visibility = Visibility.Visible;

            // We create this task completion source which flags our main loop
            // to create a copy of the next frame that comes through and then
            // we pick that up here when it's done...
            this.copiedVideoFrameComplete = new TaskCompletionSource<SoftwareBitmap>();

            var bgra16CopiedFrame = await this.copiedVideoFrameComplete.Task;

            this.copiedVideoFrameComplete = null;

            InMemoryRandomAccessStream destStream = new InMemoryRandomAccessStream();

            // Now going to JPEG because Project Oxford can accept those.
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId,
              destStream);

            encoder.SetSoftwareBitmap(bgra16CopiedFrame);

            await encoder.FlushAsync();

            FaceServiceClient faceService = new FaceServiceClient(OxfordApiKey);
            FaceServiceClient faceService1 = new FaceServiceClient(OxfordApiKey);

            //Face[] faces = await faceService.DetectAsync(destStream.AsStream(), true, true, true, true);
            //Face[] faces = await faceService.DetectAsync(destStream.AsStream(), true, true, true, true);
            var requiredFaceAttributes = new FaceAttributeType[] {
            FaceAttributeType.Age, FaceAttributeType.Gender};
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
                if (conf >= .67)
                {
                    Debug.WriteLine("Usuario encontrado");
                    // busqueda en la base de datos
                    Debug.WriteLine("Entrando a metodo bd.visualizar info de sujeto \n");
                    Query(facescomp[0].PersistedFaceId.ToString());

                }

                Query(facescomp[0].PersistedFaceId.ToString());

            }
            else
            {
                //txtAge.Text = "no age";
                //txtGender.Text = "no gender";
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
    }
}