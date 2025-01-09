using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BoatControl.Communication.Helpers
{
    public class FixedSizeQueue
    {
        private readonly object syncObject = new object();
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly HashSet<string> _lookup = new HashSet<string>();
        public int Size { get; private set; }

        public FixedSizeQueue(int size)
        {
            Size = size;
        }


        public bool Contains(string value)
        {
            return _lookup.Contains(value);
        }
        public void Enqueue(string value)
        {
            _queue.Enqueue(value);
            _lookup.Add(value);
            lock (syncObject)
            {
                while (_queue.Count > Size)
                {
                    if (_queue.TryDequeue(out var valToRemove))
                        _lookup.Remove(valToRemove);
                }
            }
        }
    }
}
