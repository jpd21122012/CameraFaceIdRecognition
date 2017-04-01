using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using System.IO;
using System.Diagnostics;

namespace cameraFaceIdSample.Classes
{
    public class PersistentIdList
    {
        public static string idReturned = "";
        public static double idconfReturned;
        //public ConexionBD co = new ConexionBD();
        public async void AddListId(string fileLocation, string subscriptionKey, string name, int edad, string descrip, Stream fileStream)
        {
            try
            {
                var client = new FaceServiceClient(subscriptionKey);
                //var faces1 = await client.DetectAsync(fileStream, true);
                var faces = await client.AddFaceToFaceListAsync("21122012", fileStream, name);
                Debug.WriteLine(" >> PId: " + faces.PersistedFaceId.ToString());
                //descomForm1.PID = faces.PersistedFaceId.ToString();
                //descomConexionBD.GuardarInfo(name, edad, descrip, Form1.PID);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }

        }
        public static string idlist = "";
        public static string httpidlist = "";
        //metodo 2
        public async void SimilarFinder(string fileLocation, string subscriptionKey, string name, int edad, Stream fileStream)
        {
            try
            {
                var client = new FaceServiceClient(subscriptionKey);
                var faces = await client.DetectAsync(fileStream, true);
                Debug.WriteLine(" > " + faces.Length + " detected.");
                Debug.WriteLine(" >> IdActual: " + faces[0].FaceId.ToString());
                idlist = faces[0].FaceId.ToString();
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }

            Guid guidId = Guid.Parse(idlist);
            Debug.WriteLine("Entrando a Find Similar\n" + guidId);
            //findsimilar

            try
            {
                var client = new FaceServiceClient(subscriptionKey);
                var faces = await client.FindSimilarAsync(guidId, "21122012", 2);
                Debug.WriteLine(" >> PId: " + faces[0].ToString() + faces[1].ToString());
                //Comparer.idActual = faces.PersistedFaceId.ToString();
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }

        }

    }
}
