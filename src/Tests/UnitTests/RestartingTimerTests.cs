using DrawnUi.Models;
using Xunit;

namespace UnitTests
{
    /// <summary>
    /// Kick() used to Stop()+Start(), cancelling a live Task.Delay on every call. Callers that kick
    /// per gesture frame (inactivity timers) paid a thrown TaskCanceledException per frame.
    /// Kicking must now only move the deadline.
    /// </summary>
    public class RestartingTimerTests
    {
        [Fact]
        public async Task Kicking_PostponesInsteadOfFiring()
        {
            var fired = 0;
            using var timer = new RestartingTimer(TimeSpan.FromMilliseconds(300), () => Interlocked.Increment(ref fired));

            //kick at "gesture rate" for longer than the timeout: must not fire while kicking
            for (var i = 0; i < 40; i++)
            {
                timer.Kick();
                await Task.Delay(10);
            }

            Assert.Equal(0, Volatile.Read(ref fired));
            Assert.True(timer.IsRunning);

            await Task.Delay(600);
            Assert.Equal(1, Volatile.Read(ref fired));
            Assert.False(timer.IsRunning);
        }

        [Fact]
        public async Task Stop_PreventsTheCallback()
        {
            var fired = 0;
            using var timer = new RestartingTimer(TimeSpan.FromMilliseconds(150), () => Interlocked.Increment(ref fired));

            timer.Kick();
            await Task.Delay(30);
            timer.Stop();

            await Task.Delay(400);
            Assert.Equal(0, Volatile.Read(ref fired));

            //still usable after a stop
            timer.Kick();
            await Task.Delay(400);
            Assert.Equal(1, Volatile.Read(ref fired));
        }

        [Fact]
        public async Task StopThenKick_RunsASingleLoop()
        {
            var fired = 0;
            using var timer = new RestartingTimer(TimeSpan.FromMilliseconds(150), () => Interlocked.Increment(ref fired));

            //the stopped loop is still awaiting when the new one starts: it must not fire, nor free the running flag
            timer.Kick();
            timer.Stop();
            timer.Kick();
            timer.Kick();

            await Task.Delay(600);
            Assert.Equal(1, Volatile.Read(ref fired));
        }

        [Fact]
        public async Task Generic_PassesTheLastContext()
        {
            string got = null;
            var timer = new RestartingTimer<string>(TimeSpan.FromMilliseconds(150), s => got = s);

            timer.Kick("first");
            await Task.Delay(30);
            timer.Kick("last");

            await Task.Delay(500);
            Assert.Equal("last", got);
            timer.Dispose();
        }
    }
}
