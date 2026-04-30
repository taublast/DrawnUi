using DrawnUi.Draw;

namespace DrawnUi.Gaming
{
    public interface IGame
    {
        void OnKeyDown(InputKey key);
        void OnKeyUp(InputKey key);

        void Pause();
        void Resume();
        void StopLoop();
        void StartLoop(int delayMs = 0);
    }
}
