using System;
using System.Collections.Generic;
using System.Threading;
using Cell.Util.Collections;

namespace Cell.Core
{
    /// <summary>
    /// Owns reusable fixed-size buffer segments for async networking.
    /// </summary>
    public class BufferManager
    {
        public static readonly List<BufferManager> Managers = new List<BufferManager>();
        public static readonly BufferManager Tiny = new BufferManager(1024, 128);
        public static readonly BufferManager Small = new BufferManager(1024, 1024);
        public static readonly BufferManager Default = new BufferManager(CellDef.PBUF_SEGMENT_COUNT, CellDef.MAX_PBUF_SEGMENT_SIZE);
        public static readonly BufferManager Large = new BufferManager(128, 64 * 1024);
        public static readonly BufferManager ExtraLarge = new BufferManager(32, 512 * 1024);
        public static readonly BufferManager SuperSized = new BufferManager(16, 1024 * 1024);

        public static long GlobalAllocatedMemory;

        private static int segmentId;
        private readonly LockfreeQueue<BufferSegment> availableSegments = new LockfreeQueue<BufferSegment>();
        private readonly List<ArrayBuffer> buffers = new List<ArrayBuffer>();
        private readonly object syncRoot = new object();
        private readonly int segmentCount;
        private readonly int segmentSize;
        private int totalSegmentCount;

        public BufferManager(int segmentCount, int segmentSize)
        {
            if (segmentCount <= 0)
            {
                throw new ArgumentOutOfRangeException("segmentCount");
            }

            if (segmentSize <= 0)
            {
                throw new ArgumentOutOfRangeException("segmentSize");
            }

            this.segmentCount = segmentCount;
            this.segmentSize = segmentSize;

            lock (Managers)
            {
                Managers.Add(this);
            }
        }

        public int AvailableSegmentsCount
        {
            get { return availableSegments.Count; }
        }

        public bool InUse
        {
            get { return availableSegments.Count < totalSegmentCount; }
        }

        public int UsedSegmentCount
        {
            get { return totalSegmentCount - availableSegments.Count; }
        }

        public int TotalBufferCount
        {
            get
            {
                lock (syncRoot)
                {
                    return buffers.Count;
                }
            }
        }

        public int TotalSegmentCount
        {
            get { return totalSegmentCount; }
        }

        public int TotalAllocatedMemory
        {
            get
            {
                lock (syncRoot)
                {
                    return buffers.Count * (segmentCount * segmentSize);
                }
            }
        }

        public int SegmentSize
        {
            get { return segmentSize; }
        }

        public BufferSegment CheckOut()
        {
            BufferSegment segment;
            if (!availableSegments.TryDequeue(out segment))
            {
                lock (syncRoot)
                {
                    while (!availableSegments.TryDequeue(out segment))
                    {
                        CreateBuffer();
                    }
                }
            }

            segment.m_uses = 1;
            return segment;
        }

        public SegmentStream CheckOutStream()
        {
            return new SegmentStream(CheckOut());
        }

        public void CheckIn(BufferSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            availableSegments.Enqueue(segment);
        }

        public static BufferSegment GetSegment(int payloadSize)
        {
            if (payloadSize <= Tiny.SegmentSize)
            {
                return Tiny.CheckOut();
            }

            if (payloadSize <= Small.SegmentSize)
            {
                return Small.CheckOut();
            }

            if (payloadSize <= Default.SegmentSize)
            {
                return Default.CheckOut();
            }

            if (payloadSize <= Large.SegmentSize)
            {
                return Large.CheckOut();
            }

            if (payloadSize <= ExtraLarge.SegmentSize)
            {
                return ExtraLarge.CheckOut();
            }

            throw new ArgumentOutOfRangeException("payloadSize", "Required buffer is too large.");
        }

        public static SegmentStream GetSegmentStream(int payloadSize)
        {
            return new SegmentStream(GetSegment(payloadSize));
        }

        ~BufferManager()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            BufferSegment segment;
            while (availableSegments.TryDequeue(out segment))
            {
            }

            lock (syncRoot)
            {
                buffers.Clear();
                totalSegmentCount = 0;
            }
        }

        private void CreateBuffer()
        {
            ArrayBuffer buffer = new ArrayBuffer(this, segmentCount * segmentSize);
            for (int i = 0; i < segmentCount; i++)
            {
                int id = Interlocked.Increment(ref segmentId);
                availableSegments.Enqueue(new BufferSegment(buffer, i * segmentSize, segmentSize, id));
            }

            buffers.Add(buffer);
            totalSegmentCount += segmentCount;
            Interlocked.Add(ref GlobalAllocatedMemory, segmentCount * segmentSize);
        }
    }
}
