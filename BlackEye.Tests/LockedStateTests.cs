namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    /// <summary>
    /// LockedState is the concurrency primitive the whole bridge relies on: the
    /// serial reader thread, the ping timer thread and the UDP receive callback all
    /// mutate state through it.
    /// </summary>
    public class LockedStateTests
    {
        private const int Idle = 0x02;

        private const int Receiving = 0x03;

        private const int Transmitting = 0x04;

        [Fact]
        public void DefaultStateIsZero()
        {
            var state = new LockedState();
            var ran = false;

            state.CompareExecute(0, () => ran = true);

            Assert.True(ran);
        }

        [Fact]
        public void ExchangeExecuteSetsTheStateAndRunsTheAction()
        {
            var state = new LockedState(Idle);
            var ran = false;

            state.ExchangeExecute(Receiving, () => ran = true);

            Assert.True(ran);

            var confirmed = false;
            state.CompareExecute(Receiving, () => confirmed = true);
            Assert.True(confirmed);
        }

        [Fact]
        public void CompareExecuteRunsOnlyOnAMatchingState()
        {
            var state = new LockedState(Idle);
            var matched = false;
            var mismatched = false;

            state.CompareExecute(Idle, () => matched = true);
            state.CompareExecute(Transmitting, () => mismatched = true);

            Assert.True(matched);
            Assert.False(mismatched);
        }

        [Fact]
        public void CompareExchangeExecuteTransitionsAndReturnsThePreviousState()
        {
            var state = new LockedState(Idle);
            var ran = false;

            var previous = state.CompareExchangeExecute(Idle, Transmitting, () => ran = true);

            Assert.Equal(Idle, previous);
            Assert.True(ran);

            var confirmed = false;
            state.CompareExecute(Transmitting, () => confirmed = true);
            Assert.True(confirmed);
        }

        [Fact]
        public void CompareExchangeExecuteOnAMismatchReturnsTheCurrentStateAndDoesNothing()
        {
            var state = new LockedState(Idle);
            var ran = false;

            var current = state.CompareExchangeExecute(Receiving, Transmitting, () => ran = true);

            Assert.Equal(Idle, current);
            Assert.False(ran);

            var stillIdle = false;
            state.CompareExecute(Idle, () => stillIdle = true);
            Assert.True(stillIdle);
        }

        [Fact]
        public void ActionsMayTransitionTheSameStateWithoutDeadlocking()
        {
            // The bridge does exactly this: a frame is written inside a
            // CompareExecute(Receiving) and the end-of-transmission path then
            // flips to Idle from within that same action. Monitor is reentrant on
            // the owning thread, so this must not hang.
            var state = new LockedState(Receiving);
            var inner = false;

            state.CompareExecute(Receiving, () =>
                state.ExchangeExecute(Idle, () => inner = true));

            Assert.True(inner);

            var isIdle = false;
            state.CompareExecute(Idle, () => isIdle = true);
            Assert.True(isIdle);
        }

        [Fact]
        public async Task OnlyOneOfManyRacingTransitionsRuns()
        {
            var state = new LockedState(Idle);
            var winners = new List<int>();
            var winnersLock = new object();
            using var start = new ManualResetEventSlim(false);

            var racers = new Task[16];

            for (int i = 0; i < racers.Length; i++)
            {
                int id = i;
                racers[i] = Task.Run(() =>
                {
                    start.Wait();

                    state.CompareExchangeExecute(Idle, Transmitting, () =>
                    {
                        lock (winnersLock)
                        {
                            winners.Add(id);
                        }
                    });
                });
            }

            start.Set();
            await Task.WhenAll(racers);

            Assert.Single(winners);
        }
    }
}
