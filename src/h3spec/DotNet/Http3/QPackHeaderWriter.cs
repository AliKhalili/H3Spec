using System.Diagnostics;
using System.Net.Http.QPack;
using System.Text;

namespace H3Spec.DotNet.Http3
{
    internal static class QPackHeaderWriter
    {
        public static bool BeginEncodeHeaders(IDictionary<string, string> headers, Span<byte> buffer, ref int totalHeaderSize, out int length)
        {
            bool hasValue = headers.Count > 0;
            Debug.Assert(hasValue == true);

            buffer[0] = 0;
            buffer[1] = 0;

            bool doneEncode = Encode(headers, buffer.Slice(2), ref totalHeaderSize, out length);

            // Add two for the first two bytes.
            length += 2;
            return doneEncode;
        }


        public static bool Encode(IDictionary<string, string> headers, Span<byte> buffer, ref int totalHeaderSize, out int length) => Encode(headers, buffer, throwIfNoneEncoded: true, ref totalHeaderSize, out length);

        private static bool Encode(IDictionary<string, string> headers, Span<byte> buffer, bool throwIfNoneEncoded, ref int totalHeaderSize, out int length)
        {
            length = 0;

            foreach (var header in headers)
            {
                // Match the current header to the QPACK static table. Possible outcomes:
                // 1. Known header and value. Write index.
                // 2. Known header with custom value. Write name index and full value.
                // 3. Unknown header. Write full name and value.
                var (staticTableId, matchedValue) = GetQPackStaticTableId(header.Key, header.Value);
                var name = header.Key;
                var value = header.Value;

                int headerLength;
                if (matchedValue)
                {
                    if (!QPackEncoder.EncodeStaticIndexedHeaderField(staticTableId, buffer.Slice(length), out headerLength))
                    {
                        if (length == 0 && throwIfNoneEncoded)
                        {
                            throw new QPackEncodingException("TODO sync with corefx" /* CoreStrings.HPackErrorNotEnoughBuffer */);
                        }
                        return false;
                    }
                }
                else
                {
                    var valueEncoding = ASCIIEncoding.ASCII;

                    if (!EncodeHeader(buffer.Slice(length), staticTableId, name, value, valueEncoding, out headerLength))
                    {
                        if (length == 0 && throwIfNoneEncoded)
                        {
                            throw new QPackEncodingException("TODO sync with corefx" /* CoreStrings.HPackErrorNotEnoughBuffer */);
                        }
                        return false;
                    }
                }

                // https://quicwg.org/base-drafts/draft-ietf-quic-http.html#section-4.1.1.3
                totalHeaderSize += HeaderField.GetLength(name.Length, value.Length);
                length += headerLength;
            }

            return true;
        }

        private static bool EncodeHeader(Span<byte> buffer, int staticTableId, string name, string value, Encoding? valueEncoding, out int headerLength) => staticTableId == -1
                ? QPackEncoder.EncodeLiteralHeaderFieldWithoutNameReference(name, value, valueEncoding, buffer, out headerLength)
                : QPackEncoder.EncodeLiteralHeaderFieldWithStaticNameReference(staticTableId, value, valueEncoding, buffer, out headerLength);


        public static (int index, bool matchedValue) GetQPackStaticTableId(string key, string value)
        {
            if (Enum.TryParse<KnownHeaderType>(key, ignoreCase: true, result: out var type))
            {
                return HttpHeadersCompression.MatchKnownHeaderQPack(type, value);
            }
            return (-1, false);
        }
    }

    internal static class HttpHeadersCompression
    {
        internal static (int index, bool matchedValue) MatchKnownHeaderQPack(KnownHeaderType knownHeader, string value)
        {
            switch (knownHeader)
            {
                case KnownHeaderType.Age:
                    switch (value)
                    {
                        case "0":
                            return (2, true);
                        default:
                            return (2, false);
                    }
                case KnownHeaderType.ContentLength:
                    switch (value)
                    {
                        case "0":
                            return (4, true);
                        default:
                            return (4, false);
                    }
                case KnownHeaderType.Date:
                    return (6, false);
                case KnownHeaderType.ETag:
                    return (7, false);
                case KnownHeaderType.LastModified:
                    return (10, false);
                case KnownHeaderType.Location:
                    return (12, false);
                case KnownHeaderType.SetCookie:
                    return (14, false);
                case KnownHeaderType.AcceptRanges:
                    switch (value)
                    {
                        case "bytes":
                            return (32, true);
                        default:
                            return (32, false);
                    }
                case KnownHeaderType.AccessControlAllowHeaders:
                    switch (value)
                    {
                        case "cache-control":
                            return (33, true);
                        case "content-type":
                            return (34, true);
                        case "*":
                            return (75, true);
                        default:
                            return (33, false);
                    }
                case KnownHeaderType.AccessControlAllowOrigin:
                    switch (value)
                    {
                        case "*":
                            return (35, true);
                        default:
                            return (35, false);
                    }
                case KnownHeaderType.CacheControl:
                    switch (value)
                    {
                        case "max-age=0":
                            return (36, true);
                        case "max-age=2592000":
                            return (37, true);
                        case "max-age=604800":
                            return (38, true);
                        case "no-cache":
                            return (39, true);
                        case "no-store":
                            return (40, true);
                        case "public, max-age=31536000":
                            return (41, true);
                        default:
                            return (36, false);
                    }
                case KnownHeaderType.ContentEncoding:
                    switch (value)
                    {
                        case "br":
                            return (42, true);
                        case "gzip":
                            return (43, true);
                        default:
                            return (42, false);
                    }
                case KnownHeaderType.ContentType:
                    switch (value)
                    {
                        case "application/dns-message":
                            return (44, true);
                        case "application/javascript":
                            return (45, true);
                        case "application/json":
                            return (46, true);
                        case "application/x-www-form-urlencoded":
                            return (47, true);
                        case "image/gif":
                            return (48, true);
                        case "image/jpeg":
                            return (49, true);
                        case "image/png":
                            return (50, true);
                        case "text/css":
                            return (51, true);
                        case "text/html; charset=utf-8":
                            return (52, true);
                        case "text/plain":
                            return (53, true);
                        case "text/plain;charset=utf-8":
                            return (54, true);
                        default:
                            return (44, false);
                    }
                case KnownHeaderType.Vary:
                    switch (value)
                    {
                        case "accept-encoding":
                            return (59, true);
                        case "origin":
                            return (60, true);
                        default:
                            return (59, false);
                    }
                case KnownHeaderType.AccessControlAllowCredentials:
                    switch (value)
                    {
                        case "FALSE":
                            return (73, true);
                        case "TRUE":
                            return (74, true);
                        default:
                            return (73, false);
                    }
                case KnownHeaderType.AccessControlAllowMethods:
                    switch (value)
                    {
                        case "get":
                            return (76, true);
                        case "get, post, options":
                            return (77, true);
                        case "options":
                            return (78, true);
                        default:
                            return (76, false);
                    }
                case KnownHeaderType.AccessControlExposeHeaders:
                    switch (value)
                    {
                        case "content-length":
                            return (79, true);
                        default:
                            return (79, false);
                    }
                case KnownHeaderType.AltSvc:
                    switch (value)
                    {
                        case "clear":
                            return (83, true);
                        default:
                            return (83, false);
                    }
                case KnownHeaderType.Server:
                    return (92, false);

                default:
                    return (-1, false);
            }
        }
    }

    internal enum KnownHeaderType
    {
        Unknown,
        Accept,
        AcceptCharset,
        AcceptEncoding,
        AcceptLanguage,
        AcceptRanges,
        AccessControlAllowCredentials,
        AccessControlAllowHeaders,
        AccessControlAllowMethods,
        AccessControlAllowOrigin,
        AccessControlExposeHeaders,
        AccessControlMaxAge,
        AccessControlRequestHeaders,
        AccessControlRequestMethod,
        Age,
        Allow,
        AltSvc,
        AltUsed,
        Authority,
        Authorization,
        Baggage,
        CacheControl,
        Connection,
        ContentEncoding,
        ContentLanguage,
        ContentLength,
        ContentLocation,
        ContentMD5,
        ContentRange,
        ContentType,
        Cookie,
        CorrelationContext,
        Date,
        ETag,
        Expect,
        Expires,
        From,
        GrpcAcceptEncoding,
        GrpcEncoding,
        GrpcMessage,
        GrpcStatus,
        GrpcTimeout,
        Host,
        IfMatch,
        IfModifiedSince,
        IfNoneMatch,
        IfRange,
        IfUnmodifiedSince,
        KeepAlive,
        LastModified,
        Location,
        MaxForwards,
        Method,
        Origin,
        Path,
        Pragma,
        Protocol,
        ProxyAuthenticate,
        ProxyAuthorization,
        ProxyConnection,
        Range,
        Referer,
        RequestId,
        RetryAfter,
        Scheme,
        Server,
        SetCookie,
        TE,
        TraceParent,
        TraceState,
        Trailer,
        TransferEncoding,
        Translate,
        Upgrade,
        UpgradeInsecureRequests,
        UserAgent,
        Vary,
        Via,
        Warning,
        WWWAuthenticate,
    }

}
