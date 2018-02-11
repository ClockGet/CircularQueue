using System;
using System.Threading;

namespace TaskQueue.Memory
{
    public sealed class CircularQueue<T>
    {
        private volatile int m_readIterator;
        private volatile int m_writeIterator;
        private readonly int m_capacity;
        private volatile T[] m_array;
        private volatile int m_count;
        internal int Low
        {
            get
            {
                return m_readIterator;
            }
        }
        internal int High
        {
            get
            {
                return m_writeIterator;
            }
        }
        internal T[] Data
        {
            get
            {
                return m_array;
            }
        }
        public int Count
        {
            get
            {
                return m_count;
            }
        }

        public CircularQueue(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            var p = Math.Log(capacity) / Math.Log(2);
            if (p > 30.0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            if (p - Math.Floor(p) > 1e-8)
            {
                p = (int)p + 1;
            }
            m_capacity = (int)Math.Pow(2, p);
            m_array = new T[m_capacity];
            m_readIterator = m_writeIterator = -1;
        }
        public bool TryAppend(T item)
        {
            if (Interlocked.Increment(ref m_count) >= m_capacity)
            {
                Interlocked.Decrement(ref m_count);
                return false;
            }
            int num = Interlocked.Increment(ref m_writeIterator);
            int index = num & m_capacity - 1;
            m_array[index] = item;
            return true;
        }
        public bool TryTake(int count, out T[] result)
        {
            result = default(T[]);
            SpinWait spinWait = default(SpinWait);
            int i = m_readIterator;
            int high = m_writeIterator;
            int realCount = Math.Min(count, GetDistance(high, i));
            while (realCount > 0)
            {
                if (Interlocked.CompareExchange(ref m_readIterator, i + realCount, i) == i)
                {
                    ++i;
                    int start = i & m_capacity - 1;
                    result = new T[realCount];
                    for (int j = 0; j < realCount; j++)
                    {
                        int index = (start + j) & m_capacity - 1;
                        result[j] = m_array[index];
                        Interlocked.Decrement(ref m_count);
                    }
                    return true;
                }
                spinWait.SpinOnce();
                i = m_readIterator;
                high = m_writeIterator;
                realCount = Math.Min(count, GetDistance(high, i));
            }
            return false;
        }
        private int GetDistance(int high, int low)
        {
            return (high - low) & m_capacity - 1;
        }
    }
}
