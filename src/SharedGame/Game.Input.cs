using DrawnUi.Draw;

namespace DrawnUi.Gaming
{
    public partial class Game
    {
        #region KEYS

        /// <summary>
        /// Override this to process game keys
        /// </summary>
        public virtual void OnKeyDown(InputKey key)
        {
        }

        /// <summary>
        /// Override this to process game keys
        /// </summary>
        public virtual void OnKeyUp(InputKey key)
        {
        }

        /// <summary>
        /// Do not use directly. It's public to be able to send keys to game manually if needed.
        /// </summary>
        public void OnKeyboardDownEvent(object sender, InputKey key)
        {
            OnKeyDown(key);
        }

        /// <summary>
        /// Do not use directly. It's public to be able to send keys to game manually if needed.
        /// </summary>
        public void OnKeyboardUpEvent(object sender, InputKey key)
        {
            OnKeyUp(key);
        }

        #endregion
    }
}
