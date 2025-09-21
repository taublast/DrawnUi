using DrawnUi.Controls;

namespace Sandbox
{
    /// <summary>
    /// Warrior-specific mapping from integer states to sprite sheet + layout and mirroring.
    /// Subclassing SkiaSpriteSet ensures Source swaps are atomic per state, while geometry
    /// (Columns/Rows) and mirroring are set here, in one place.
    /// </summary>
    public class WarriorSprite : SkiaSpriteSet
    {
        public enum WarriorAnimState { IdleRight, IdleLeft, WalkRight, WalkLeft, WarRight, WarLeft }

        public WarriorAnimState WState
        {
            get => _wstate;
            set
            {
                if (_wstate == value) return;
                _wstate = value;
                // Map to base model: 0 = idle, 1 = walk, 2 = war
                State = value switch
                {
                    WarriorAnimState.IdleLeft or WarriorAnimState.IdleRight => 0,
                    WarriorAnimState.WalkLeft or WarriorAnimState.WalkRight => 1,
                    _ => 2,
                };
                ApplyMirror();
            }
        }
        private WarriorAnimState _wstate;

        void ApplyMirror()
        {
            if (CurrentSprite != null)
            {
                var mirror = (WState == WarriorAnimState.IdleLeft || WState == WarriorAnimState.WalkLeft || WState == WarriorAnimState.WarLeft);
                CurrentSprite.ScaleX = mirror ? -1 : 1;
            }
        }

        public WarriorSprite()
        {
            Define(0, "Anims/BlueWarrior/Warrior_Idle.png", columns: 8, rows: 1, fps: 15);
            Define(1, "Anims/BlueWarrior/Warrior_Run.png", columns: 6, rows: 1, fps: 15);
            Define(2, "Anims/BlueWarrior/Warrior_Attack1.png", columns: 4, rows: 1, fps: 8);
            WState = WarriorAnimState.IdleRight;
        }

        protected override void OnChangeState(int oldState, int newState)
        {
            base.OnChangeState(oldState, newState);
            ApplyMirror();
        }
    }
}
