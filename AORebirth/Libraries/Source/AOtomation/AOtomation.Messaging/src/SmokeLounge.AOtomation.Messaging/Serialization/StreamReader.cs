// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StreamReader.cs" company="SmokeLounge">
//   Copyright © 2013 SmokeLounge.
//   This program is free software. It comes without any warranty, to
//   the extent permitted by applicable law. You can redistribute it
//   and/or modify it under the terms of the Do What The Fuck You Want
//   To Public License, Version 2, as published by Sam Hocevar. See
//   http://www.wtfpl.net/ for more details.
// </copyright>
// <summary>
//   Defines the StreamReader type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SmokeLounge.AOtomation.Messaging.Serialization
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;

    using SmokeLounge.AOtomation.Messaging.GameData;

    public sealed class StreamReader : IDisposable
    {
        #region Fields

        private readonly BinaryReader reader;

        private readonly Stream stream;

        #endregion

        #region Constructors and Destructors

        public StreamReader(Stream stream)
        {
            this.stream = stream;
            this.reader = new BinaryReader(stream);
        }

        #endregion

        #region Public Properties

        public long Position
        {
            get
            {
                return this.stream.Position;
            }

            set
            {
                this.stream.Position = value;
            }
        }

        public long Length
        {
            get
            {
                return this.stream.Length;
            }
        }

        public long Remaining
        {
            get
            {
                return Math.Max(0, this.stream.Length - this.stream.Position);
            }
        }

        #endregion

        #region Public Methods and Operators

        public void Dispose()
        {
            this.reader.Dispose();
            this.stream.Dispose();
        }

        public byte ReadByte()
        {
            return this.reader.ReadByte();
        }

        public SByte ReadSByte()
        {
            return this.reader.ReadSByte();
        }

        /// <summary>
        /// Reads up to <paramref name="count"/> bytes. The buffer is sized by what the message holds,
        /// not by a length the sender declared.
        /// </summary>
        public byte[] ReadBytes(int count)
        {
            return this.reader.ReadBytes((int)Math.Min(count, this.Remaining));
        }

        /// <summary>
        /// A sender-declared element count, rejected before anything is allocated when it is negative or
        /// larger than the bytes left, since every element takes at least one byte.
        /// </summary>
        public int CheckElementCount(int count)
        {
            if (count < 0 || count > this.Remaining)
            {
                throw new InvalidDataException(
                    "Declared element count " + count + " exceeds the " + this.Remaining + " bytes left in the message.");
            }

            return count;
        }

        public short ReadInt16()
        {
            return IPAddress.NetworkToHostOrder(this.reader.ReadInt16());
        }

        public int ReadInt32()
        {
            return IPAddress.NetworkToHostOrder(this.reader.ReadInt32());
        }

        public long ReadInt64()
        {
            return IPAddress.NetworkToHostOrder(this.reader.ReadInt64());
        }

        public float ReadSingle()
        {
            var single = this.reader.ReadBytes(4);
            Array.Reverse(single);
            return BitConverter.ToSingle(single, 0);
        }

        public string ReadString(int length)
        {
            var bytes = this.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes).TrimEnd(char.MinValue);
        }

        public ushort ReadUInt16()
        {
            var littleEndian = this.reader.ReadUInt16() << 16;
            return (ushort)IPAddress.NetworkToHostOrder(littleEndian);
        }

        public uint ReadUInt32()
        {
            var littleEndian = this.reader.ReadUInt32() << 32;
            return (uint)(IPAddress.NetworkToHostOrder(littleEndian) >> 32);
        }

        public Identity ReadIdentity()
        {
            IdentityType type = (IdentityType)this.ReadInt32();
            int instance = this.ReadInt32();
            return new Identity(){Type=type,Instance = instance};
        }

        #endregion
    }
}
