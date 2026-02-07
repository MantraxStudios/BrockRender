using OpenTK.Mathematics;

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
}
