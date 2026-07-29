using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Hubcon
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public readonly struct RequestId : IEquatable<RequestId>
    {
        private readonly ulong _hi;
        [JsonPropertyName("hi")]
        public ulong Hi => _hi;
        
        private readonly ulong _lo;
        [JsonPropertyName("lo")]
        public ulong Lo => _lo;

        public static RequestId Empty => default;

        [JsonConstructor]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestId(ulong hi, ulong lo)
        {
            _hi = hi;
            _lo = lo;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RequestId(Guid guid)
        {
            Unsafe.SkipInit(out this);
            MemoryMarshal.Write(MemoryMarshal.CreateSpan(ref Unsafe.As<RequestId, byte>(ref this), 16), ref guid);
        }

        /// <summary>
        /// Generates a new RequestId combining a timestamp and a random guid (UUIDv7/TraceId).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestId NewId()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, RequestId>(ref g);
        }

        #region Red y Memorias (Span Operations)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RequestId ReadFrom(ReadOnlySpan<byte> source)
        {
            if (source.Length < 16)
                throw new ArgumentOutOfRangeException(nameof(source), "Se requieren al menos 16 bytes.");

            var hi = BinaryPrimitives.ReadUInt64BigEndian(source);
            var lo = BinaryPrimitives.ReadUInt64BigEndian(source[8..]);
            return new RequestId(hi, lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length < 16)
                throw new ArgumentOutOfRangeException(nameof(destination), "El buffer destino requiere 16 bytes.");

            BinaryPrimitives.WriteUInt64BigEndian(destination, _hi);
            BinaryPrimitives.WriteUInt64BigEndian(destination[8..], _lo);
        }

        #endregion

        #region Parsing de Headers HTTP (Handshake)

        public static RequestId ParseHeader(ReadOnlySpan<char> header)
        {
            header = header.Trim();

            // Case W3C Traceparent: 00-4d502c03384abc99124806be91afdfe7-a7e7f36238457189-01 (55 chars)
            if (header.Length >= 55 && header[2] == '-')
            {
                ReadOnlySpan<char> traceIdHex = header.Slice(3, 32);
                if (TryParseHex128(traceIdHex, out RequestId w3cId))
                    return w3cId;
            }

            // Case Guid with lines (36 chars) or plain hex (32 chars)
            if (Guid.TryParse(header, out Guid parsedGuid))
            {
                return new RequestId(parsedGuid);
            }

            // Fallback: 128 bit hash for custom strings
            return FromArbitraryStringHash(header);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseHex128(ReadOnlySpan<char> hex, out RequestId result)
        {
            if (ulong.TryParse(hex[..16], System.Globalization.NumberStyles.HexNumber, null, out var hi) &&
                ulong.TryParse(hex[16..32], System.Globalization.NumberStyles.HexNumber, null, out var lo))
            {
                result = new RequestId(hi, lo);
                return true;
            }

            result = default;
            return false;
        }

        private static RequestId FromArbitraryStringHash(ReadOnlySpan<char> str)
        {
            // simplified FNV-1a 128 bit hash for weird strings in 16 bytes
            ulong hi = 14695981039346656037UL;
            ulong lo = 1099511628211UL;

            foreach (char c in str)
            {
                hi = (hi ^ c) * 1099511628211UL;
                lo = (lo ^ (ulong)c) * 14695981039346656037UL;
            }

            return new RequestId(hi, lo);
        }

        #endregion

        #region Conversion to Guid / ActivityTraceId

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Guid ToGuid() => Unsafe.As<RequestId, Guid>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActivityTraceId ToActivityTraceId()
        {
            Span<byte> bytes = stackalloc byte[16];
            WriteTo(bytes);
            return ActivityTraceId.CreateFromBytes(bytes);
        }

        #endregion

        #region Formatting and equality

        public bool Equals(RequestId other) => _hi == other._hi && _lo == other._lo;
        public override bool Equals(object? obj) => obj is RequestId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_hi, _lo);

        public static bool operator ==(RequestId left, RequestId right) => left.Equals(right);
        public static bool operator !=(RequestId left, RequestId right) => !left.Equals(right);

        public override string ToString() => ToGuid().ToString("N");

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
            IFormatProvider? provider)
        {
            return ToGuid().TryFormat(destination, out charsWritten, format);
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            ToGuid().ToString(format, formatProvider);

        #endregion
    }
}