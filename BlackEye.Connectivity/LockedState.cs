namespace BlackEye.Connectivity
{
    public class LockedState
    {
        private int state = 0;

        private object stateLock = new object();

        public LockedState()
        {
        }

        public LockedState(int state)
        {
            this.state = state;
        }

        public void ExchangeExecute(int value, Action action)
        {
            lock (stateLock)
            {
                state = value;
                action();
            }
        }

        public void CompareExecute(int comparand, Action action)
        {
            if (state == comparand)
            {
                lock (stateLock)
                {
                    if (state == comparand)
                    {
                        action();
                    }
                }
            }
        }

        public int CompareExchangeExecute(int comparand, int value, Action action)
        {
            if (state == comparand)
            {
                lock (stateLock)
                {
                    if (state == comparand)
                    {
                        int result = state;
                        state = value;
                        action();
                        return result;
                    }
                    else
                    {
                        return state;
                    }
                }
            }
            else
            {
                return state;
            }
        }
    }
}
