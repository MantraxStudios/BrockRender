using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SpaceShooter
{
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

            RenderPlayerView(_player2, 2);
            RenderPlayerView(_player1, 1);

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

            DrawSpaceGrid();

            if (_player1.IsAlive)
                DrawShip(_player1, new Vector3(0.0f, 1.0f, 0.0f), playerNum == 1);

            if (_player2.IsAlive)
                DrawShip(_player2, new Vector3(1.0f, 0.0f, 0.0f), playerNum == 2);

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

            // Shooting - Solo dispara cuando se presiona por primera vez
            ship.ShootCooldown -= deltaTime;

            if (shoot)
            {
                if (!ship.ShootPressed && ship.ShootCooldown <= 0)
                {
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
}
