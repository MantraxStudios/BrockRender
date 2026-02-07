using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SpaceShooter
{
    public class Bullet
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public int Owner;
        public float LifeTime;
        
        public Bullet(Vector3 pos, Vector3 vel, int owner)
        {
            Position = pos;
            Velocity = vel;
            Owner = owner;
            LifeTime = 3.0f;
        }
    }

    public class Ship
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float RotationY;
        public float RotationPitch;
        public int Score;
        public bool IsAlive;
        public float RespawnTimer;
        public bool ShootPressed;
        public float ShootCooldown;
        
        public Ship(Vector3 startPos)
        {
            Position = startPos;
            Velocity = Vector3.Zero;
            RotationY = 0;
            RotationPitch = 0;
            Score = 0;
            IsAlive = true;
            RespawnTimer = 0;
            ShootPressed = false;
            ShootCooldown = 0;
        }

        public Vector3 GetForward()
        {
            return new Vector3(
                MathF.Sin(RotationY),
                -MathF.Sin(RotationPitch),
                MathF.Cos(RotationY)
            );
        }
    }

    public class Game : GameWindow
    {
        private int _vertexBufferObject;
        private int _vertexArrayObject;
        private int _shaderProgram;
        private int _elementBufferObject;
        
        private int _sphereVBO;
        private int _sphereVAO;
        private int _sphereEBO;
        private int _sphereIndexCount;
        
        private Ship _player1;
        private Ship _player2;
        private List<Bullet> _bullets = new List<Bullet>();
        
        private float _acceleration = 0.3f;
        private float _maxSpeed = 0.5f;
        private float _turnSpeed = 0.08f;
        private float _bulletSpeed = 1.0f;
        private float _shootCooldownTime = 0.3f;
        
        public static byte[]? LatestFrameJPEG1 { get; private set; }
        public static byte[]? LatestFrameJPEG2 { get; private set; }
        public static int Player1Score { get; private set; }
        public static int Player2Score { get; private set; }
        public static object FrameLock = new object();
        
        public static Dictionary<string, bool> WebInputs1 = new Dictionary<string, bool>()
        {
            { "w", false }, { "s", false }, { "a", false }, { "d", false }, { "space", false }
        };
        public static Dictionary<string, bool> WebInputs2 = new Dictionary<string, bool>()
        {
            { "up", false }, { "down", false }, { "left", false }, { "right", false }, { "enter", false }
        };
        public static object InputLock = new object();

        private readonly float[] _cubeVertices =
        {
            -0.5f, -0.5f, -0.5f,   1.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,   0.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,   0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,   1.0f, 1.0f, 0.0f,
            -0.5f, -0.5f,  0.5f,   1.0f, 0.0f, 1.0f,
             0.5f, -0.5f,  0.5f,   0.0f, 1.0f, 1.0f,
             0.5f,  0.5f,  0.5f,   1.0f, 1.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,   0.5f, 0.5f, 0.5f
        };

        private readonly uint[] _cubeIndices =
        {
            0, 1, 2, 2, 3, 0,
            4, 5, 6, 6, 7, 4,
            0, 3, 7, 7, 4, 0,
            1, 2, 6, 6, 5, 1,
            3, 2, 6, 6, 7, 3,
            0, 1, 5, 5, 4, 0
        };

        public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            _player1 = new Ship(new Vector3(-10, 0, 0));
            _player2 = new Ship(new Vector3(10, 0, 0));
            _player2.RotationY = MathF.PI;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 0.05f, 1.0f);
            GL.Enable(EnableCap.DepthTest);

            string vertexShaderSource = @"
                #version 330 core
                layout (location = 0) in vec3 aPosition;
                layout (location = 1) in vec3 aColor;
                out vec3 ourColor;
                uniform mat4 model;
                uniform mat4 view;
                uniform mat4 projection;
                void main()
                {
                    gl_Position = projection * view * model * vec4(aPosition, 1.0);
                    ourColor = aColor;
                }";

            string fragmentShaderSource = @"
                #version 330 core
                in vec3 ourColor;
                out vec4 FragColor;
                uniform vec3 objectColor;
                void main()
                {
                    FragColor = vec4(objectColor * ourColor, 1.0);
                }";

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);

            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _cubeVertices.Length * sizeof(float), _cubeVertices, BufferUsageHint.StaticDraw);

            _elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _cubeIndices.Length * sizeof(uint), _cubeIndices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            
            CreateSphere();
        }

        private void CreateSphere()
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();
            
            int segments = 12;
            int rings = 12;
            
            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = MathF.PI * ring / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float theta = 2.0f * MathF.PI * segment / segments;
                    
                    float x = MathF.Sin(phi) * MathF.Cos(theta);
                    float y = MathF.Cos(phi);
                    float z = MathF.Sin(phi) * MathF.Sin(theta);
                    
                    vertices.Add(x);
                    vertices.Add(y);
                    vertices.Add(z);
                    vertices.Add(1.0f);
                    vertices.Add(1.0f);
                    vertices.Add(1.0f);
                }
            }
            
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    uint current = (uint)(ring * (segments + 1) + segment);
                    uint next = current + (uint)segments + 1;
                    
                    indices.Add(current);
                    indices.Add(next);
                    indices.Add(current + 1);
                    
                    indices.Add(current + 1);
                    indices.Add(next);
                    indices.Add(next + 1);
                }
            }
            
            _sphereIndexCount = indices.Count;
            
            _sphereVAO = GL.GenVertexArray();
            GL.BindVertexArray(_sphereVAO);
            
            _sphereVBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sphereVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);
            
            _sphereEBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _sphereEBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);
            
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            // Render Player 1 view
            RenderPlayerView(_player1, 1);
            
            // Render Player 2 view
            RenderPlayerView(_player2, 2);

            SwapBuffers();
        }

        private void RenderPlayerView(Ship player, int playerNum)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.UseProgram(_shaderProgram);

            Vector3 cameraPos = player.Position - player.GetForward() * 5.0f + Vector3.UnitY * 2.0f;
            Vector3 cameraTarget = player.Position + player.GetForward() * 3.0f;
            
            Matrix4 view = Matrix4.LookAt(cameraPos, cameraTarget, Vector3.UnitY);
            
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(60.0f), 
                640f / 480f, 
                0.1f, 200.0f
            );

            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "view"), false, ref view);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "projection"), false, ref projection);

            // Draw space grid
            DrawSpaceGrid();
            
            // Draw player 1
            if (_player1.IsAlive)
                DrawShip(_player1, new Vector3(0.0f, 1.0f, 0.0f), playerNum == 1);
            
            // Draw player 2
            if (_player2.IsAlive)
                DrawShip(_player2, new Vector3(1.0f, 0.0f, 0.0f), playerNum == 2);
            
            // Draw bullets
            foreach (var bullet in _bullets)
            {
                Vector3 color = bullet.Owner == 1 ? new Vector3(0.0f, 1.0f, 0.5f) : new Vector3(1.0f, 0.5f, 0.0f);
                DrawSphere(bullet.Position, 0.2f, color);
            }

            CaptureFrame(playerNum);
        }

        private void DrawShip(Ship ship, Vector3 color, bool isCurrentPlayer)
        {
            // Main body
            Matrix4 model = Matrix4.CreateScale(0.8f, 0.4f, 1.5f) * 
                           Matrix4.CreateRotationY(ship.RotationY) * 
                           Matrix4.CreateRotationX(ship.RotationPitch) *
                           Matrix4.CreateTranslation(ship.Position);
            DrawCubeWithModel(model, color);
            
            // Cockpit
            model = Matrix4.CreateScale(0.5f, 0.3f, 0.8f) * 
                   Matrix4.CreateRotationY(ship.RotationY) * 
                   Matrix4.CreateRotationX(ship.RotationPitch) *
                   Matrix4.CreateTranslation(ship.Position + ship.GetForward() * 0.3f + Vector3.UnitY * 0.3f);
            DrawCubeWithModel(model, new Vector3(0.3f, 0.7f, 1.0f));
            
            // Wings
            Vector3 right = Vector3.Cross(ship.GetForward(), Vector3.UnitY);
            model = Matrix4.CreateScale(2.0f, 0.1f, 0.8f) * 
                   Matrix4.CreateRotationY(ship.RotationY) * 
                   Matrix4.CreateRotationX(ship.RotationPitch) *
                   Matrix4.CreateTranslation(ship.Position - ship.GetForward() * 0.3f);
            DrawCubeWithModel(model, color * 0.7f);
        }

        private void DrawSpaceGrid()
        {
            for (int x = -50; x <= 50; x += 5)
            {
                DrawCube(new Vector3(x, -5, 0), new Vector3(0.1f, 0.1f, 100.0f), new Vector3(0.1f, 0.1f, 0.2f));
            }
            for (int z = -50; z <= 50; z += 5)
            {
                DrawCube(new Vector3(0, -5, z), new Vector3(100.0f, 0.1f, 0.1f), new Vector3(0.1f, 0.1f, 0.2f));
            }
        }

        private void DrawCube(Vector3 position, Vector3 scale, Vector3 color)
        {
            Matrix4 model = Matrix4.CreateScale(scale) * Matrix4.CreateTranslation(position);
            DrawCubeWithModel(model, color);
        }

        private void DrawCubeWithModel(Matrix4 model, Vector3 color)
        {
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref model);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "objectColor"), color);
            
            GL.BindVertexArray(_vertexArrayObject);
            GL.DrawElements(PrimitiveType.Triangles, _cubeIndices.Length, DrawElementsType.UnsignedInt, 0);
        }

        private void DrawSphere(Vector3 position, float radius, Vector3 color)
        {
            Matrix4 model = Matrix4.CreateScale(radius) * Matrix4.CreateTranslation(position);
            GL.UniformMatrix4(GL.GetUniformLocation(_shaderProgram, "model"), false, ref model);
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "objectColor"), color);
            
            GL.BindVertexArray(_sphereVAO);
            GL.DrawElements(PrimitiveType.Triangles, _sphereIndexCount, DrawElementsType.UnsignedInt, 0);
        }

        private void CaptureFrame(int playerNum)
        {
            try
            {
                int width = 640;
                int height = 480;
                
                byte[] pixels = new byte[width * height * 3];
                GL.ReadPixels(0, 0, width, height, OpenTK.Graphics.OpenGL4.PixelFormat.Rgb, PixelType.UnsignedByte, pixels);

                byte[] flipped = new byte[pixels.Length];
                for (int y = 0; y < height; y++)
                {
                    Array.Copy(pixels, (height - 1 - y) * width * 3, flipped, y * width * 3, width * 3);
                }

                byte[] jpeg = EncodeJPEG(flipped, width, height);
                
                lock (FrameLock)
                {
                    if (playerNum == 1)
                        LatestFrameJPEG1 = jpeg;
                    else
                        LatestFrameJPEG2 = jpeg;
                    
                    Player1Score = _player1.Score;
                    Player2Score = _player2.Score;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private byte[] EncodeJPEG(byte[] rgb, int width, int height)
        {
            using (var ms = new MemoryStream())
            {
                using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(rgb, width, height))
                {
                    var encoder = new JpegEncoder { Quality = 70 };
                    image.SaveAsJpeg(ms, encoder);
                }
                return ms.ToArray();
            }
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            float deltaTime = (float)args.Time;

            // Player 1 controls
            bool w, s, a, d, space;
            bool up, down, left, right, enter;
            
            lock (InputLock)
            {
                w = KeyboardState.IsKeyDown(Keys.W) || WebInputs1["w"];
                s = KeyboardState.IsKeyDown(Keys.S) || WebInputs1["s"];
                a = KeyboardState.IsKeyDown(Keys.A) || WebInputs1["a"];
                d = KeyboardState.IsKeyDown(Keys.D) || WebInputs1["d"];
                space = KeyboardState.IsKeyDown(Keys.Space) || WebInputs1["space"];
                
                up = KeyboardState.IsKeyDown(Keys.Up) || WebInputs2["up"];
                down = KeyboardState.IsKeyDown(Keys.Down) || WebInputs2["down"];
                left = KeyboardState.IsKeyDown(Keys.Left) || WebInputs2["left"];
                right = KeyboardState.IsKeyDown(Keys.Right) || WebInputs2["right"];
                enter = KeyboardState.IsKeyDown(Keys.Enter) || WebInputs2["enter"];
            }

            UpdateShip(_player1, w, s, a, d, space, deltaTime);
            UpdateShip(_player2, up, down, left, right, enter, deltaTime);

            // Update bullets
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                _bullets[i].Position += _bullets[i].Velocity * deltaTime * 60.0f;
                _bullets[i].LifeTime -= deltaTime;
                
                if (_bullets[i].LifeTime <= 0 || _bullets[i].Position.Length > 100)
                {
                    _bullets.RemoveAt(i);
                    continue;
                }

                // Check collision with player 1
                if (_player1.IsAlive && _bullets[i].Owner == 2 && Vector3.Distance(_bullets[i].Position, _player1.Position) < 1.5f)
                {
                    _player1.IsAlive = false;
                    _player1.RespawnTimer = 3.0f;
                    _player2.Score++;
                    _bullets.RemoveAt(i);
                    continue;
                }

                // Check collision with player 2
                if (_player2.IsAlive && _bullets[i].Owner == 1 && Vector3.Distance(_bullets[i].Position, _player2.Position) < 1.5f)
                {
                    _player2.IsAlive = false;
                    _player2.RespawnTimer = 3.0f;
                    _player1.Score++;
                    _bullets.RemoveAt(i);
                }
            }

            // Respawn logic
            if (!_player1.IsAlive)
            {
                _player1.RespawnTimer -= deltaTime;
                if (_player1.RespawnTimer <= 0)
                {
                    _player1.Position = new Vector3(-10, 0, 0);
                    _player1.Velocity = Vector3.Zero;
                    _player1.RotationY = 0;
                    _player1.IsAlive = true;
                }
            }

            if (!_player2.IsAlive)
            {
                _player2.RespawnTimer -= deltaTime;
                if (_player2.RespawnTimer <= 0)
                {
                    _player2.Position = new Vector3(10, 0, 0);
                    _player2.Velocity = Vector3.Zero;
                    _player2.RotationY = MathF.PI;
                    _player2.IsAlive = true;
                }
            }

            if (KeyboardState.IsKeyDown(Keys.Escape))
                Close();
        }

        private void UpdateShip(Ship ship, bool forward, bool backward, bool turnLeft, bool turnRight, bool shoot, float deltaTime)
        {
            if (!ship.IsAlive) return;

            if (turnLeft)
                ship.RotationY += _turnSpeed;
            if (turnRight)
                ship.RotationY -= _turnSpeed;

            Vector3 thrust = Vector3.Zero;
            if (forward)
                thrust = ship.GetForward() * _acceleration;
            if (backward)
                thrust = -ship.GetForward() * _acceleration * 0.5f;

            ship.Velocity += thrust * deltaTime * 60.0f;
            
            // Apply drag
            ship.Velocity *= 0.98f;
            
            // Limit speed
            if (ship.Velocity.Length > _maxSpeed)
                ship.Velocity = Vector3.Normalize(ship.Velocity) * _maxSpeed;

            ship.Position += ship.Velocity * deltaTime * 60.0f;

            // Boundaries
            if (ship.Position.X > 50) ship.Position.X = 50;
            if (ship.Position.X < -50) ship.Position.X = -50;
            if (ship.Position.Z > 50) ship.Position.Z = 50;
            if (ship.Position.Z < -50) ship.Position.Z = -50;
            if (ship.Position.Y > 20) ship.Position.Y = 20;
            if (ship.Position.Y < -3) ship.Position.Y = -3;

            // Shooting - FIXED: Solo dispara cuando se presiona por primera vez
            ship.ShootCooldown -= deltaTime;
            
            if (shoot)
            {
                if (!ship.ShootPressed && ship.ShootCooldown <= 0)
                {
                    // Disparar solo cuando se presiona por primera vez
                    Vector3 bulletVel = ship.GetForward() * _bulletSpeed + ship.Velocity;
                    _bullets.Add(new Bullet(ship.Position + ship.GetForward() * 2.0f, bulletVel, ship == _player1 ? 1 : 2));
                    ship.ShootCooldown = _shootCooldownTime;
                }
                ship.ShootPressed = true;
            }
            else
            {
                ship.ShootPressed = false;
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            GL.DeleteBuffer(_vertexBufferObject);
            GL.DeleteBuffer(_elementBufferObject);
            GL.DeleteVertexArray(_vertexArrayObject);
            GL.DeleteBuffer(_sphereVBO);
            GL.DeleteBuffer(_sphereEBO);
            GL.DeleteVertexArray(_sphereVAO);
            GL.DeleteProgram(_shaderProgram);
        }
    }

    class WebServer
    {
        private HttpListener? _listener;
        private Thread? _serverThread;
        private bool _running = true;

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
            {
                ServeFrame(context, 1);
            }
            else if (path == "/frame2")
            {
                ServeFrame(context, 2);
            }
            else if (path == "/input1")
            {
                HandleInput(context, 1);
            }
            else if (path == "/input2")
            {
                HandleInput(context, 2);
            }
            else
            {
                ServeHtml(context);
            }
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
            string html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Space Shooter 3D</title>
    <style>
        body {
            margin: 0;
            background: #000;
            font-family: 'Courier New', monospace;
            color: #fff;
        }
        .container {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            padding: 20px;
            max-width: 1600px;
            margin: 0 auto;
        }
        .player-view {
            text-align: center;
        }
        .player-view img {
            width: 100%;
            border: 3px solid;
            border-radius: 10px;
            image-rendering: crisp-edges;
        }
        .player1 img { border-color: #0f0; }
        .player2 img { border-color: #f00; }
        h1 {
            text-align: center;
            color: #0ff;
            text-shadow: 0 0 20px #0ff;
        }
        .score {
            font-size: 32px;
            font-weight: bold;
            margin: 10px 0;
        }
        .player1 .score { color: #0f0; }
        .player2 .score { color: #f00; }
        .controls {
            background: #111;
            padding: 15px;
            border-radius: 10px;
            margin-top: 10px;
            font-size: 14px;
        }
        .btn-grid {
            display: grid;
            grid-template-columns: repeat(3, 60px);
            gap: 5px;
            justify-content: center;
            margin: 10px 0;
        }
        button {
            padding: 12px;
            font-size: 16px;
            border: 2px solid;
            border-radius: 5px;
            cursor: pointer;
            font-weight: bold;
            user-select: none;
            touch-action: manipulation;
        }
        .p1-btn {
            background: #0f0;
            border-color: #0f0;
            color: #000;
        }
        .p2-btn {
            background: #f00;
            border-color: #f00;
            color: #000;
        }
        .shoot-btn {
            grid-column: 1 / 4;
            font-size: 20px;
        }
        button:active { transform: scale(0.95); }
        .empty { visibility: hidden; }
        #fps {
            text-align: center;
            color: #0ff;
            font-size: 12px;
        }
    </style>
</head>
<body>
    <h1>🚀 SPACE SHOOTER 3D 🚀</h1>
    <div id='fps'>FPS: 0</div>
    <div class='container'>
        <div class='player-view player1'>
            <h2>JUGADOR 1</h2>
            <div class='score'>SCORE: <span id='score1'>0</span></div>
            <img id='img1' width='640' height='480'>
            <div class='controls'>
                <p>WASD: Mover | SPACE: Disparar</p>
                <div class='btn-grid'>
                    <button class='empty'></button>
                    <button class='p1-btn' id='p1-w'>W</button>
                    <button class='empty'></button>
                    <button class='p1-btn' id='p1-a'>A</button>
                    <button class='p1-btn' id='p1-s'>S</button>
                    <button class='p1-btn' id='p1-d'>D</button>
                    <button class='p1-btn shoot-btn' id='p1-space'>DISPARO</button>
                </div>
            </div>
        </div>
        <div class='player-view player2'>
            <h2>JUGADOR 2</h2>
            <div class='score'>SCORE: <span id='score2'>0</span></div>
            <img id='img2' width='640' height='480'>
            <div class='controls'>
                <p>Flechas: Mover | ENTER: Disparar</p>
                <div class='btn-grid'>
                    <button class='empty'></button>
                    <button class='p2-btn' id='p2-up'>↑</button>
                    <button class='empty'></button>
                    <button class='p2-btn' id='p2-left'>←</button>
                    <button class='p2-btn' id='p2-down'>↓</button>
                    <button class='p2-btn' id='p2-right'>→</button>
                    <button class='p2-btn shoot-btn' id='p2-enter'>DISPARO</button>
                </div>
            </div>
        </div>
    </div>
    <script>
        const img1 = document.getElementById('img1');
        const img2 = document.getElementById('img2');
        const score1 = document.getElementById('score1');
        const score2 = document.getElementById('score2');
        const fpsDisplay = document.getElementById('fps');
        
        const keys1 = { w: false, s: false, a: false, d: false, space: false };
        const keys2 = { up: false, down: false, left: false, right: false, enter: false };
        
        let frameCount = 0, lastTime = Date.now();
        
        function sendInput(player, key, pressed) {
            fetch(`/input${player}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ [key]: pressed }),
                keepalive: true
            }).catch(() => {});
        }
        
        document.addEventListener('keydown', (e) => {
            const k = e.key.toLowerCase();
            if (['w','s','a','d'].includes(k) && !keys1[k]) {
                keys1[k] = true;
                sendInput(1, k, true);
            }
            if (e.key === ' ' && !keys1.space) {
                e.preventDefault();
                keys1.space = true;
                sendInput(1, 'space', true);
            }
            if (e.key === 'ArrowUp' && !keys2.up) { e.preventDefault(); keys2.up = true; sendInput(2, 'up', true); }
            if (e.key === 'ArrowDown' && !keys2.down) { e.preventDefault(); keys2.down = true; sendInput(2, 'down', true); }
            if (e.key === 'ArrowLeft' && !keys2.left) { e.preventDefault(); keys2.left = true; sendInput(2, 'left', true); }
            if (e.key === 'ArrowRight' && !keys2.right) { e.preventDefault(); keys2.right = true; sendInput(2, 'right', true); }
            if (e.key === 'Enter' && !keys2.enter) { e.preventDefault(); keys2.enter = true; sendInput(2, 'enter', true); }
        });
        
        document.addEventListener('keyup', (e) => {
            const k = e.key.toLowerCase();
            if (['w','s','a','d'].includes(k)) {
                keys1[k] = false;
                sendInput(1, k, false);
            }
            if (e.key === ' ') { keys1.space = false; sendInput(1, 'space', false); }
            if (e.key === 'ArrowUp') { keys2.up = false; sendInput(2, 'up', false); }
            if (e.key === 'ArrowDown') { keys2.down = false; sendInput(2, 'down', false); }
            if (e.key === 'ArrowLeft') { keys2.left = false; sendInput(2, 'left', false); }
            if (e.key === 'ArrowRight') { keys2.right = false; sendInput(2, 'right', false); }
            if (e.key === 'Enter') { keys2.enter = false; sendInput(2, 'enter', false); }
        });
        
        function setupBtn(id, player, key) {
            const btn = document.getElementById(id);
            btn.addEventListener('mousedown', () => sendInput(player, key, true));
            btn.addEventListener('mouseup', () => sendInput(player, key, false));
            btn.addEventListener('mouseleave', () => sendInput(player, key, false));
            btn.addEventListener('touchstart', (e) => { e.preventDefault(); sendInput(player, key, true); });
            btn.addEventListener('touchend', (e) => { e.preventDefault(); sendInput(player, key, false); });
        }
        
        setupBtn('p1-w', 1, 'w');
        setupBtn('p1-s', 1, 's');
        setupBtn('p1-a', 1, 'a');
        setupBtn('p1-d', 1, 'd');
        setupBtn('p1-space', 1, 'space');
        setupBtn('p2-up', 2, 'up');
        setupBtn('p2-down', 2, 'down');
        setupBtn('p2-left', 2, 'left');
        setupBtn('p2-right', 2, 'right');
        setupBtn('p2-enter', 2, 'enter');
        
        async function update() {
            try {
                const [r1, r2] = await Promise.all([
                    fetch('/frame1?' + Date.now()),
                    fetch('/frame2?' + Date.now())
                ]);
                
                if (r1.ok) {
                    img1.src = URL.createObjectURL(await r1.blob());
                    score1.textContent = r1.headers.get('X-Score1') || '0';
                }
                if (r2.ok) {
                    img2.src = URL.createObjectURL(await r2.blob());
                    score2.textContent = r2.headers.get('X-Score2') || '0';
                }
                
                frameCount++;
                const now = Date.now();
                if (now - lastTime >= 1000) {
                    fpsDisplay.textContent = 'FPS: ' + frameCount;
                    frameCount = 0;
                    lastTime = now;
                }
            } catch (e) {}
            requestAnimationFrame(update);
        }
        
        update();
    </script>
</body>
</html>";

            byte[] buffer = Encoding.UTF8.GetBytes(html);
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

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("INICIANDO SPACE SHOOTER 3D - PUERTO 8080");
            
            WebServer server = new WebServer();
            server.Start();

            var gameWindowSettings = new GameWindowSettings()
            {
                UpdateFrequency = 120
            };
            
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new Vector2i(800, 600),
                Title = "Space Shooter 3D - Puerto 8080",
            };

            using (var game = new Game(gameWindowSettings, nativeWindowSettings))
            {
                game.Run();
            }

            server.Stop();
        }
    }
}