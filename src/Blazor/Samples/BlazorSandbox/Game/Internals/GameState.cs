namespace SpaceShooter.Game;

public enum GameState
{
    Unset,

    /// <summary>
    /// Welcome screen presented
    /// </summary>
    Ready,

    /// <summary>
    /// Game loop is running
    /// </summary>
    Playing,

    Paused,

    /// <summary>
    /// Game ended
    /// </summary>
    Ended
}