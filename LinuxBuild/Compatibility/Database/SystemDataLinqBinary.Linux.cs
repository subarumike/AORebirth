using System;
using System.Runtime.Serialization;

namespace System.Data.Linq
{
    [DataContract]
    [Serializable]
    public sealed class Binary : IEquatable<Binary>
    {
        [DataMember(Name = "Bytes")]
        private byte[] bytes;

        private int? hashCode;

        public Binary(byte[] value)
        {
            if (value == null)
            {
                value = new byte[0];
            }

            this.bytes = (byte[])value.Clone();
            this.ComputeHash();
        }

        public int Length
        {
            get
            {
                return this.bytes.Length;
            }
        }

        public static implicit operator Binary(byte[] value)
        {
            return new Binary(value);
        }

        public static bool operator ==(Binary left, Binary right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(Binary left, Binary right)
        {
            return !(left == right);
        }

        public bool Equals(Binary other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(other, null)
                || this.bytes.Length != other.bytes.Length
                || this.GetHashCode() != other.GetHashCode())
            {
                return false;
            }

            for (int index = 0; index < this.bytes.Length; index++)
            {
                if (this.bytes[index] != other.bytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Binary);
        }

        public override int GetHashCode()
        {
            if (!this.hashCode.HasValue)
            {
                this.ComputeHash();
            }

            return this.hashCode.Value;
        }

        public byte[] ToArray()
        {
            return (byte[])this.bytes.Clone();
        }

        public override string ToString()
        {
            return "\"" + Convert.ToBase64String(this.bytes, 0, this.bytes.Length) + "\"";
        }

        private void ComputeHash()
        {
            unchecked
            {
                int s = 314;
                const int T = 159;
                int hash = 0;
                for (int index = 0; index < this.bytes.Length; index++)
                {
                    hash = (hash * s) + this.bytes[index];
                    s *= T;
                }

                this.hashCode = hash;
            }
        }
    }
}
