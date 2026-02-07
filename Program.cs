using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace SpaceShooter
{
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
