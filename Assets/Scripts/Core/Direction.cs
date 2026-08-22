namespace WrongDirection.Core
{
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static Direction Opposite(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:    return Direction.Down;
                case Direction.Down:  return Direction.Up;
                case Direction.Left:  return Direction.Right;
                default:              return Direction.Left;
            }
        }

        /// <summary>Z rotation (degrees) for an arrow sprite that points UP by default.</summary>
        public static float ToRotation(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:    return 0f;
                case Direction.Down:  return 180f;
                case Direction.Left:  return 90f;
                default:              return -90f;
            }
        }
    }
}
