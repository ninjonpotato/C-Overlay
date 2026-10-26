using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace screengrabber_wpf
{
    internal static class VisualEffektek
    {
        //ez fizikailag mozgatni fogja az ablakot
        public static Vector2 Offset { get; private set; } = Vector2.Zero;

        private static Vector2 _speed = new Vector2(150f, 150f);
        private static Vector2 _direction = new Vector2(1, 1);
        private static Vector2 _maxOffset;

        public static void DvdLogo(float deltaTime, Vector2 Size)
        {
           _maxOffset = new Vector2(Size.X/2, Size.Y/2);
           Offset += _speed * _direction * deltaTime;
           if (Offset.X > _maxOffset.X) { _direction.X = -1; }
           if (Offset.X <= 0) { _direction.X = 1; }
            if (Offset.Y > _maxOffset.Y) { _direction.Y = -1; }
            if (Offset.Y <= 0) { _direction.Y = 1; }
       
        }

    }
}
