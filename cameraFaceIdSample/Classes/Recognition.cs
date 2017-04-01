using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using System.IO;
using Windows.Storage.Streams;

namespace cameraFaceIdSample.Classes
{
    public class Recognition
    {
        public static string idlist = "";
        public static string httpidlist = "";
        public static string idReturned = "";
        public static double idconfReturned;
        public string responsejson = "";
        public double responseconf = 0;
        public static string IdSearch = "";


        //ConexionBD bd = new ConexionBD();
        public async Task HttpFindSimilarAsync()
        {
            Debug.WriteLine("Entered in HTTPFIND...");
            
               
                //fileStream.Dispose();
            
            try
            {
                Guid idGuid = Guid.Parse(IdSearch);
                MainPage.guid = idGuid;
                var client1 = new FaceServiceClient("12476023b4c349939778c49e5db321d6");
                var faces = await client1.FindSimilarAsync(idGuid, "21122012", 1);
                Debug.WriteLine(" >> IdActual: " + faces[0].PersistedFaceId.ToString());
                Debug.WriteLine(" >> Conf: " + faces[0].Confidence.ToString());
                idReturned = faces[0].PersistedFaceId.ToString();
                string dou = faces[0].Confidence.ToString();
                idconfReturned = Double.Parse(dou);
                //Evaluar si es mayor a .7 entonces regresa los datos de la persona
                if (idconfReturned >= .67)
                {
                    Debug.WriteLine("Usuario encontrado");
                    // busqueda en la base de datos
                    Debug.WriteLine("Entrando a metodo bd.visualizar info de sujeto \n");
                    //Descomentarbd.VisualizarInfo(idReturned);
                    MainPage.x = idReturned;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }


        }
    }
}
