
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Drawing.Imaging;
using System.Drawing;

namespace FaceDetectFinal
{
    public static class FaceDetect
    {
        [FunctionName("FaceDetect")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            var image = await req.Content.ReadAsStreamAsync();

            MemoryStream mem = new MemoryStream();
            image.CopyTo(mem); //make a copy since one gets destroy in the other API. Lame, I know.
            image.Position = 0;
            mem.Position = 0;

            string result = await CallVisionAPI(image);
            log.Info(result);

            if (string.IsNullOrEmpty(result))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            dynamic faceData = JsonConvert.DeserializeObject(result);


            MemoryStream outputStream = new MemoryStream();
            using (Image maybeFace = Image.FromStream(mem, true))
            {
                using (Graphics g = Graphics.FromImage(maybeFace))
                {
                    Pen yellowPen = new Pen(Color.Yellow, 4);

                    int numFaces = faceData.Count;
                    for (int i = 0; i < numFaces; i++)
                    {
                        dynamic faceRectangle = faceData[i].faceRectangle;
                        int top = faceRectangle.top;
                        int left = faceRectangle.left;
                        int width = faceRectangle.width;
                        int height = faceRectangle.height;

                        Rectangle rect = new Rectangle(left, top, width, height);

                        g.DrawRectangle(yellowPen, rect);
                    }
                }
                maybeFace.Save(outputStream, ImageFormat.Jpeg);
            }

            var response = new HttpResponseMessage()
            {
                Content = new ByteArrayContent(outputStream.ToArray()),
                StatusCode = HttpStatusCode.OK,
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return response;
        }

        static async Task<string> CallFaceAPI(Stream image)
        {
            using (var client = new HttpClient())
            {
                var content = new StreamContent(image);
                var url = "https://westcentralus.api.cognitive.microsoft.com/face/v1.0/detect";
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "4ec24a903183424bacf45a2072714299");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var httpResponse = await client.PostAsync(url, content);

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return await httpResponse.Content.ReadAsStringAsync();
                }
            }
            return null;
        }
    }
}
