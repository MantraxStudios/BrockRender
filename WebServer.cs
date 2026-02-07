using System.Net;
using System.Text;
using System.IO;
using System.Linq;

namespace SpaceShooter
{
    class WebServer
    {
        private HttpListener? _listener;
        private Thread? _serverThread;
        private bool _running = true;
        private readonly string _htmlContent;

        public WebServer()
        {
            string htmlPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
            if (File.Exists(htmlPath))
                _htmlContent = File.ReadAllText(htmlPath);
            else
                _htmlContent = "<html><body><h1>index.html not found</h1></body></html>";
        }

        public void Start()
        {
            _serverThread = new Thread(() =>
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://+:8080/");
                _listener.Start();
                Console.WriteLine("Servidor: http://localhost:8080/");

                while (_running)
                {
                    try
                    {
                        HttpListenerContext context = _listener.GetContext();
                        ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
                    }
                    catch { }
                }
            });
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";

            if (path == "/frame1")
                ServeFrame(context, 1);
            else if (path == "/frame2")
                ServeFrame(context, 2);
            else if (path == "/input1")
                HandleInput(context, 1);
            else if (path == "/input2")
                HandleInput(context, 2);
            else
                ServeHtml(context);
        }

        private void HandleInput(HttpListenerContext context, int player)
        {
            try
            {
                using (StreamReader reader = new StreamReader(context.Request.InputStream))
                {
                    string json = reader.ReadToEnd();
                    var inputs = player == 1 ? Game.WebInputs1 : Game.WebInputs2;

                    lock (Game.InputLock)
                    {
                        foreach (var key in inputs.Keys.ToList())
                        {
                            if (json.Contains($"\"{key}\":true"))
                                inputs[key] = true;
                            else if (json.Contains($"\"{key}\":false"))
                                inputs[key] = false;
                        }
                    }
                }

                context.Response.StatusCode = 200;
                context.Response.Close();
            }
            catch { }
        }

        private void ServeFrame(HttpListenerContext context, int player)
        {
            try
            {
                byte[]? frameData;
                int score1, score2;

                lock (Game.FrameLock)
                {
                    frameData = player == 1 ? Game.LatestFrameJPEG1 : Game.LatestFrameJPEG2;
                    score1 = Game.Player1Score;
                    score2 = Game.Player2Score;
                }

                if (frameData != null)
                {
                    context.Response.ContentType = "image/jpeg";
                    context.Response.ContentLength64 = frameData.Length;
                    context.Response.AddHeader("Cache-Control", "no-cache");
                    context.Response.AddHeader("X-Score1", score1.ToString());
                    context.Response.AddHeader("X-Score2", score2.ToString());
                    context.Response.OutputStream.Write(frameData, 0, frameData.Length);
                }
                else
                {
                    context.Response.StatusCode = 503;
                }
                context.Response.Close();
            }
            catch { }
        }

        private void ServeHtml(HttpListenerContext context)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(_htmlContent);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();
            _listener?.Close();
        }
    }
}
