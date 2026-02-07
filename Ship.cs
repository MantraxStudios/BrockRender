using OpenTK.Mathematics;

namespace SpaceShooter
{
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
}
