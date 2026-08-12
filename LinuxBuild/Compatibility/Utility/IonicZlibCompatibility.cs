using System;
using System.IO;

namespace Ionic.Zlib
{
    public enum CompressionMode
    {
        Compress,
        Decompress
    }

    public enum CompressionLevel
    {
        Default = -1,
        None = 0,
        Level0 = 0,
        BestSpeed = 1,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        Level6 = 6,
        Level7 = 7,
        Level8 = 8,
        Level9 = 9,
        BestCompression = 9
    }

    public enum FlushType
    {
        None,
        Partial,
        Sync,
        Full,
        Finish,
        Block
    }

    public sealed class ZlibStream : Stream
    {
        private readonly Stream baseStream;
        private readonly System.IO.Compression.ZLibStream innerStream;
        private readonly CompressionMode mode;
        private bool compressionFinished;
        private bool disposed;
        private long position;

        public ZlibStream(Stream stream, CompressionMode mode)
            : this(stream, mode, CompressionLevel.Default)
        {
        }

        public ZlibStream(Stream stream, CompressionMode mode, CompressionLevel level)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            this.baseStream = stream;
            this.mode = mode;
            this.innerStream = mode == CompressionMode.Compress
                ? new System.IO.Compression.ZLibStream(stream, MapCompressionLevel(level), true)
                : new System.IO.Compression.ZLibStream(
                    stream,
                    System.IO.Compression.CompressionMode.Decompress,
                    true);
        }

        public FlushType FlushMode { get; set; }

        public override bool CanRead
        {
            get { return !this.disposed && this.mode == CompressionMode.Decompress; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return !this.disposed && !this.compressionFinished && this.mode == CompressionMode.Compress; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { return this.position; }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        public long TotalIn
        {
            get { return this.position; }
        }

        public long TotalOut
        {
            get { return -1L; }
        }

        public override void Flush()
        {
            this.ThrowIfDisposed();
            if (this.mode == CompressionMode.Compress && this.FlushMode == FlushType.Full)
            {
                this.FinishCompression();
                return;
            }

            this.innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed();
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = this.innerStream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            this.position += totalRead;
            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            this.ThrowIfDisposed();
            if (origin == SeekOrigin.Begin && offset == 0L && this.position == 0L)
            {
                return 0L;
            }

            throw new NotSupportedException("Seeking is not supported by a zlib stream.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.ThrowIfDisposed();
            if (this.compressionFinished)
            {
                throw new InvalidOperationException("The zlib stream has already been finalized.");
            }

            this.innerStream.Write(buffer, offset, count);
            this.position += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.FinishCompression();
                this.innerStream.Dispose();
                this.baseStream.Dispose();
                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private static System.IO.Compression.CompressionLevel MapCompressionLevel(CompressionLevel level)
        {
            if (level == CompressionLevel.None || level == CompressionLevel.Level0)
            {
                return System.IO.Compression.CompressionLevel.NoCompression;
            }

            if (level >= CompressionLevel.Level9)
            {
                return System.IO.Compression.CompressionLevel.SmallestSize;
            }

            if (level > CompressionLevel.None && level <= CompressionLevel.Level3)
            {
                return System.IO.Compression.CompressionLevel.Fastest;
            }

            return System.IO.Compression.CompressionLevel.Optimal;
        }

        private void FinishCompression()
        {
            if (this.mode == CompressionMode.Compress && !this.compressionFinished)
            {
                this.innerStream.Dispose();
                this.compressionFinished = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(typeof(ZlibStream).FullName);
            }
        }
    }
}
